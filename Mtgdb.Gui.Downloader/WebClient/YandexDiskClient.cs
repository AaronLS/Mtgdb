﻿using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Mtgdb.Downloader
{
	public class YandexDiskClient : WebClientBase
	{
		private readonly HttpClient _http = new HttpClient();
		private const string ApiUrl = "https://cloud-api.yandex.net/v1/disk/public/resources";
		private const int Limit = 1000;

		public async Task<DirectoryWrapperJson> GetRootMetadata()
		{
			const string rootUrl = "https://yadi.sk/d/f1HuKUg7xW2FUQ";
			var metaUrl = $"{ApiUrl}?public_key={rootUrl}&limit={Limit}";
			var response = await GetResponse(_http, HttpMethod.Get, metaUrl, CancellationToken.None);
			response.EnsureSuccessStatusCode();
			var contentStr = await response.Content.ReadAsStringAsync();
			return JsonConvert.DeserializeObject<DirectoryWrapperJson>(contentStr);
		}

		public async Task<DirectoryWrapperJson> GetPathMetadata(string path)
		{
			if (string.IsNullOrEmpty(path))
				throw new ArgumentException("Empty path", nameof(path));

			const string rootUrl = "https://yadi.sk/d/f1HuKUg7xW2FUQ";
			var metaUrl = $"{ApiUrl}?public_key={rootUrl}&path={path}&limit={Limit}";
			var response = await GetResponse(_http, HttpMethod.Get, metaUrl, CancellationToken.None);
			response.EnsureSuccessStatusCode();
			var contentStr = await response.Content.ReadAsStringAsync();
			return JsonConvert.DeserializeObject<DirectoryWrapperJson>(contentStr);
		}

		public Task<string> GetFilelistDownloadUrl(ImageSourcesConfig source, QualityGroupConfig quality, CancellationToken token) =>
			GetDownloadLink(source.YandexKey, string.Format(source.YandexListPath, quality.YandexName), token);

		public async Task<string> GetDownloadLink(string key, string path, CancellationToken token)
		{
			var response = await GetResponse(_http, HttpMethod.Get, $"{ApiUrl}/download?public_key={key}&path={path}", token);
			response.EnsureSuccessStatusCode();
			var linkStr = await response.Content.ReadAsStringAsync();
			var link = JsonConvert.DeserializeObject<LinkJson>(linkStr);
			return link.Href;
		}

		[JsonObject]
		public class DirectoryWrapperJson
		{
			[JsonProperty("public_key")]
			public string PublicKey { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("path")]
			public string Path { get; set; }

			[JsonProperty("_embedded")]
			public DirectoryJson Directory { get; set; }
		}

		[JsonObject]
		public class DirectoryJson
		{
			[JsonProperty("items")]
			public DirectoryEntryJson[] Items { get; set; }
		}

		[JsonObject]
		public class DirectoryEntryJson
		{
			[JsonProperty("public_key")]
			public string PublicKey { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("path")]
			public string Path { get; set; }
		}

		[JsonObject]
		public class LinkJson
		{
			[JsonProperty("href")]
			public string Href { get; set; }

			[JsonProperty("method")]
			public string Method { get; set; }

			[JsonProperty("templated")]
			public bool Templated { get; set; }
		}
	}
}
