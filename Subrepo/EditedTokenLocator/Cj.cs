using System.Collections.Generic;

namespace Lucene.Net.Contrib
{
	internal static class Cj
	{
		/// <summary>
		/// chinese japanese
		/// </summary>
		public static bool IsCj(this char c)
		{
			int rangeIndex = _cjCharacterRanges.BinarySearchFirstIndexOf(r => r.Max >= c);
			return rangeIndex >= 0 && _cjCharacterRanges[rangeIndex].Min <= c;
		}

		/// <summary>
		/// https://en.wikipedia.org/wiki/Unicode_block
		/// </summary>
		private static readonly List<CharRange> _cjCharacterRanges = new List<CharRange>
		{
			new CharRange('\u3040', '\u312f'),
			//new CharRange('\u3040', '\u309F'), // Hiragana
			//new CharRange('\u30A0', '\u30FF'), // Katakana
			//new CharRange('\u3100', '\u312f'), // Bopomofo

			new CharRange('\u31F0', '\u31FF'), // Katakana Phonetic Extensions
			new CharRange('\u3300', '\u337f'), // CJK Compatibility (Non Korean)
			new CharRange('\u3400', '\u4dbf'), // CJK Unified Ideographs ExtensionA
			new CharRange('\u4e00', '\u9fff'), // CJK Unified Ideographs
			new CharRange('\uf900', '\ufaff'), // CJK Compatibility Ideographs
			new CharRange('\uff65', '\uff9f') // Halfwidth and Fullwidth Forms (Non Korean)
		};

		private struct CharRange
		{
			public readonly char Min;
			public readonly char Max;

			public CharRange(char min, char max)
			{
				Min = min;
				Max = max;
			}
		}
	}
}