﻿using System;
using JetBrains.Annotations;
using Mtgdb.Data.Model;

namespace Mtgdb.Data.Index
{
	public class DeckIndexUpdateSubsystem
	{
		[UsedImplicitly] // in GuiLoader
		public DeckIndexUpdateSubsystem(
			DeckSearcher searcher,
			CardRepository repo,
			DeckListModel listModel,
			IApplication app)
		{
			_searcher = searcher;
			_repo = repo;
			_listModel = listModel;
			_app = app;
		}

		public void SubscribeToEvents()
		{
			_listModel.Changed += modelChanged;
		}

		private void modelChanged()
		{
			if (!_repo.IsLoadingPriceComplete.Signaled)
				throw new InvalidOperationException();

			_modelUpdatedTime = DateTime.UtcNow;
			_app.CancellationToken.Run(token =>
			{
				lock (_sync)
				{
					var modelUpdatedTime = _modelUpdatedTime;
					if (_indexUpdatedTime == modelUpdatedTime)
						return;

					_searcher.LoadIndexes();
					_indexUpdatedTime = modelUpdatedTime;
				}
			});
		}

		private DateTime? _indexUpdatedTime;
		private DateTime _modelUpdatedTime = DateTime.UtcNow;

		private readonly DeckSearcher _searcher;
		private readonly DeckListModel _listModel;
		private readonly IApplication _app;
		private readonly CardRepository _repo;
		private readonly object _sync = new object();
	}
}
