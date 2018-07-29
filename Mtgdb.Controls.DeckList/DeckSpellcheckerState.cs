﻿using System;
using System.Collections.Generic;
using Mtgdb.Index;

namespace Mtgdb.Controls
{
	public class DeckSpellcheckerState : LuceneSpellcheckerState<long, DeckModel>
	{
		public DeckSpellcheckerState(
			Spellchecker spellchecker,
			DeckSearcherState searcherState,
			DeckDocumentAdapter adapter,
			Func<int> maxCount,
			bool loaded) :
			base(spellchecker, searcherState, adapter, maxCount, loaded)
		{
			_models = searcherState.Models;
		}

		protected override IEnumerable<DeckModel> GetObjectsToIndex() =>
			_models;

		private readonly IReadOnlyList<DeckModel> _models;
	}
}