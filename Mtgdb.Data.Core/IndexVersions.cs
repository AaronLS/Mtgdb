namespace Mtgdb.Data
{
	public static class IndexVersions
	{
		// Mtgjson 4.6.2
		private const string Cards = "1.23";
		public const string CardSearcher = Cards;
		public const string CardSpellchecker = Cards;
		public const string KeywordSearcher = Cards;

		// fixed deck list re-indexing, bump version to force update obsolete indexes
		private const string Decks = "1.06";
		public const string DeckSearcher = Decks;
		public const string DeckSpellchecker = Decks;
	}
}
