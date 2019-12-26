﻿using System.Collections.Generic;
using System.Linq;
using Mtgdb.Data;
using NUnit.Framework;

namespace Mtgdb.Test
{
	public class CardTextAlphabetTests : TestsBase
	{
		[OneTimeSetUp]
		public static void Setup()
		{
			LoadTranslations();
		}

		[Test]
		public void All_symbols_in_card_texts_are_considered_in_code()
		{
			// ReSharper disable StringLiteralTypo
			var latin = new HashSet<char>("abcdefghijklmnopqrstuvwxyz");
			var cyrillic = new HashSet<char>("абвгдежзийклмнопрстуфхцчшщьыъэюя");
			var numbers = new HashSet<char>("01234567890");
			var ideographicArtistNames =
				"林泰玄 コーヘー"
					.Except(" ")
					.ToArray();
			var knownSpecialChars = new HashSet<char>("ºß"); // artist name
			// ReSharper restore StringLiteralTypo

			var alphabet = new HashSet<char>();

			var languages = new HashSet<string>(CardLocalization.GetAllLanguages(), Str.Comparer);
			languages.Remove("cn");
			languages.Remove("tw");
			languages.Remove("jp");
			languages.Remove("kr");

			var empty = Enumerable.Empty<char>();

			foreach (var set in Repo.SetsByCode.Values)
			{
				alphabet.UnionWith(set.Name);
				alphabet.UnionWith(set.Code);
			}

			var failedCards = new List<(char[], Card)>();
			foreach (var card in Repo.Cards)
			{
				var cardChars =new HashSet<char>();
				cardChars.UnionWith(card.NameEn ?? empty);
				cardChars.UnionWith(card.TypeEn ?? empty);
				cardChars.UnionWith(card.FlavorEn ?? empty);
				cardChars.UnionWith(card.TextEn ?? empty);
				cardChars.UnionWith(card.OriginalText ?? empty);
				cardChars.UnionWith(card.OriginalType ?? empty);
				cardChars.UnionWith(card.Artist?.Except(ideographicArtistNames) ?? empty);

				foreach (string lang in languages)
				{
					cardChars.UnionWith(card.GetName(lang) ?? empty);
					cardChars.UnionWith(card.GetType(lang) ?? empty);
					cardChars.UnionWith(card.GetFlavor(lang) ?? empty);
					cardChars.UnionWith(card.GetText(lang) ?? empty);
				}

				var badChars = cardChars.Where(isUnknownChar).Where(shouldBeConsidered).ToArray();
				if (badChars.Length > 0)
					failedCards.Add((badChars, card));

				alphabet.UnionWith(cardChars);
			}

			var chars = alphabet.Select(c=> char.ToLower(c, Str.Culture)).Distinct().OrderBy(c => c).ToArray();
			Log.Debug(() => new string(chars));

			var unknownChars = chars.Where(isUnknownChar).ToArray();
			Log.Debug(new string(unknownChars.ToArray()));

			var notConsideredChars = new string(unknownChars.Where(shouldBeConsidered).ToArray());

			Assert.That(failedCards, Is.Empty);

			Assert.That(notConsideredChars, Is.Empty);

			static bool shouldBeConsidered(char c) =>
				char.IsLetterOrDigit(c);

			bool isUnknownChar(char c)
			{
				c = char.ToLower(c);

				if (latin.Contains(c))
					return false;

				if (cyrillic.Contains(c))
					return false;

				if (numbers.Contains(c))
					return false;

				if (c == '\n')
					return false;

				if (c == '\r')
					return false;

				if (MtgAlphabet.Replacements.ContainsKey(c))
					return false;

				if (MtgAlphabet.WordCharsSet.Contains(c))
					return false;

				if (MtgAlphabet.LeftDelimitersSet.Contains(c))
					return false;

				if (MtgAlphabet.RightDelimitersSet.Contains(c))
					return false;

				if (MtgAlphabet.SingletoneWordChars.Contains(c))
					return false;

				if (knownSpecialChars.Contains(c))
					return false;

				return true;
			}
		}
	}
}
