﻿using System;
using System.Text.RegularExpressions;

namespace Mtgdb.Data
{
	public class FieldBuilder<TObj>
	{
		public Field<TObj, TVal> Get<TVal>(string fieldName, Func<TObj, TVal> getter, string alias = null)
		{
			return new Field<TObj, TVal>(getter, fieldName, alias ?? fieldName.ToSnakeCase().ToLower(Str.Culture));
		}
	}

	public static class FieldNameFormatter
	{
		private static readonly Regex _camelPattern = new Regex(@"(\B[A-Z])");

		public static string ToSnakeCase(this string name) =>
			_camelPattern.Replace(name, "_$1").ToLower(Str.Culture);
	}
}
