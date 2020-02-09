using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Mtgdb.Data;

namespace Mtgdb.Downloader
{
	public class ImageDownloader
	{
		[UsedImplicitly]
		public ImageDownloader(CardRepository repository, Megatools megatools)
		{
			_repository = repository;
			_megatools = megatools;
		}

		public async Task Download(string quality, IReadOnlyList<ImageDownloadProgress> allProgress, CancellationToken token)
		{
			var megaDownloader = new MegaDownloader(_megatools, _syncOutput);
			var yandexDownloader =new YandexDownloader(_syncOutput, new YandexDiskClient());
			yandexDownloader.ProgressChanged += handleProgress;

			var downloaders = new List<IDownloader>
			{
				megaDownloader,
				yandexDownloader,
			};

			var queue = new ImageDownloadQueue(_repository, downloaders, allProgress.Where(_ => Str.Equals(_.QualityGroup.Quality, quality)));

			Console.WriteLine("Found {0} directories for quality '{1}' in configuration", queue.Count, quality);
			TotalCount = queue.TotalOnlineFilesCount;

			_countInDownloadedDirs = 0;
			ProgressChanged?.Invoke();

			void megaFileDownloaded()
			{
				Interlocked.Increment(ref _countInDownloadedDirs);
				ProgressChanged?.Invoke();
			}

			_megatools.FileDownloaded += megaFileDownloaded;

			await Task.WhenAll(downloaders.Select(d => token.Run(tkn => downloadAll(queue, d, tkn))));

			_megatools.FileDownloaded -= megaFileDownloaded;
			yandexDownloader.ProgressChanged += handleProgress;

			void handleProgress(ImageDownloadProgress task)
			{
				Interlocked.Add(ref _countInDownloadedDirs, task.FilesOnline.Count - task.FilesDownloaded.Count);
				ProgressChanged?.Invoke();
			}
		}

		private async Task downloadAll(ImageDownloadQueue queue, IDownloader downloader, CancellationToken token)
		{
			while (true)
			{
				if (_abort)
					return;

				var task = queue.PopTaskFor(downloader);
				if (task == null)
					return;

				if (isAlreadyDownloaded(task))
				{
					Console.WriteLine("[Skip] {0}", task.Dir.Subdir);
					Interlocked.Add(ref _countInDownloadedDirs, task.FilesOnline.Count);
					ProgressChanged?.Invoke();
				}
				else
				{
					Interlocked.Add(ref _countInDownloadedDirs, task.FilesDownloaded.Count);
					ProgressChanged?.Invoke();

					bool success = await downloader.Download(task, token);
					if (success)
						ImageDownloadProgressReader.WriteExistingSignatures(task);
					else
					{
						if (queue.PushFailedTaskBack(downloader, task))
							Console.WriteLine("Other download source available for {0}", task.Dir.Subdir);
						else
							Console.WriteLine("No other download source available for {0}", task.Dir.Subdir);
					}
				}
			}
		}

		private static bool isAlreadyDownloaded(ImageDownloadProgress progress)
		{
			string targetSubdirectory = progress.TargetSubdirectory;
			Directory.CreateDirectory(targetSubdirectory);

			if (progress.FilesOnline == null)
				return false;

			bool alreadyDownloaded = true;

			var existingFiles = new HashSet<string>(
				Directory.GetFiles(targetSubdirectory, "*", SearchOption.AllDirectories),
				Str.Comparer);

			var existingSignatures = new Dictionary<string, FileSignature>(Str.Comparer);

			foreach (var fileOnline in progress.FilesOnline.Values)
			{
				string filePath = Path.Combine(targetSubdirectory, fileOnline.Path);

				if (!existingFiles.Contains(filePath))
				{
					alreadyDownloaded = false;
					continue;
				}

				var existingSignature =
					progress.FilesCorrupted.TryGet(fileOnline.Path) ??
					progress.FilesDownloaded.TryGet(fileOnline.Path) ??
					Signer.CreateSignature(filePath, useAbsolutePath: true).AsRelativeTo(targetSubdirectory, internPath: true);

				if (existingSignature.Md5Hash != fileOnline.Md5Hash)
				{
					alreadyDownloaded = false;
					Console.WriteLine("Deleting modified or corrupted file {0}", filePath);

					lock (ImageLoader.SyncIo)
					{
						try
						{
							File.Delete(filePath);
						}
						catch (IOException ex)
						{
							Console.WriteLine($"Failed to remove {filePath}. {ex.Message}");
						}
					}
				}
				else
				{
					existingSignatures.Add(existingSignature.Path, existingSignature);
				}
			}

			foreach (string file in existingFiles)
			{
				var relativePath = file.Substring(targetSubdirectory.Length + 1);
				if (!progress.FilesOnline.ContainsKey(relativePath) && !Str.Equals(relativePath, Signer.SignaturesFile))
				{
					Console.WriteLine("Deleting {0}", file);
					File.Delete(file);
				}
			}

			if (alreadyDownloaded)
				ImageDownloadProgressReader.WriteExistingSignatures(progress, existingSignatures.Values);

			return alreadyDownloaded;
		}

		public void Abort()
		{
			_abort = true;
			_megatools.Abort();
		}

		public event Action ProgressChanged;

		private int CountInDownloadedDirs => _countInDownloadedDirs;

		public int DownloadedCount => CountInDownloadedDirs + _megatools.DownloadedCount;
		public int TotalCount { get; private set; }

		private readonly object _syncOutput = new object();

		private readonly CardRepository _repository;
		private readonly Megatools _megatools;
		private bool _abort;
		private int _countInDownloadedDirs;
	}
}
