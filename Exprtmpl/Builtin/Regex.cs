using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Exprtmpl
{
	[Builtin("regex")]
	internal static class RegexLibrary
	{
		[Builtin("escape")]
		public static Value Escape(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			return Regex.Escape((string)values[0]);
		}

		[Builtin("unescape")]
		public static Value Unescape(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			return Regex.Unescape((string)values[0]);
		}

		[Builtin("remove")]
		public static Value Remove(Value[] values)
		{
			if (values.Length != 2)
				throw new InvalidOperationException();
			return Regex.Replace((string)values[0], (string)values[1], "", RegexOptions.Singleline);
		}

		[Builtin("replace")]
		public static Value Replace(Value[] values)
		{
			if (values.Length != 3)
				throw new InvalidOperationException();
			return Regex.Replace((string)values[0], (string)values[1], (string)values[2], RegexOptions.Singleline);
		}

		[Builtin("split")]
		public static Value Split(Value[] values)
		{
			if (values.Length != 2)
				throw new InvalidOperationException();
			string[] split = Regex.Split((string)values[0], (string)values[1], RegexOptions.Singleline);
			Value[] result = new Value[split.Length];
			for (int i = 0; i < split.Length; i++)
				result[i] = split[i];
			return Array.From(result);
		}

		[Builtin("match")]
		public static Value Match(Value[] values)
		{
			if (values.Length != 2)
				throw new InvalidOperationException();
			Regex regex = new Regex((string)values[1], RegexOptions.Singleline | RegexOptions.ExplicitCapture);
			Match match = regex.Match((string)values[0]);
			return match.Success ? From(regex.GetGroupNames(), match) : null;
		}

		private static readonly Array EmptyMatch = Array.From(new Value[0]);

		[Builtin("matches")]
		public static Value Matches(Value[] values)
		{
			if (values.Length != 2)
				throw new InvalidOperationException();
			Regex regex = new Regex((string)values[1], RegexOptions.Singleline | RegexOptions.ExplicitCapture);
			MatchCollection matches = regex.Matches((string)values[0]);
			if (matches.Count == 0)
				return EmptyMatch;
			Value[] results = new Value[matches.Count];
			string[] groups = regex.GetGroupNames();
			for (int i = 0; i < results.Length; i++)
				results[i] = From(groups, matches[i]);
			return Array.From(results);
		}

		private static Value From(string[] groups, Match match)
		{
			Dictionary<string, Value> table = new Dictionary<string, Value>(groups.Length + 1) {["*"] = match.Value};
			for (int i = 0; i < groups.Length; i++)
				table[groups[i]] = match.Groups[groups[i]].Value;
			return Table.From(table);
		}
	}
}
