using System;
using System.Collections.Generic;

namespace Exprtmpl
{
	[Builtin("array")]
	internal static class ArrayLibrary
	{
		[Builtin("concat")]
		public static Value Concat(Value[] values)
		{
			if (values.Length == 0)
				throw new InvalidOperationException();
			List<Value> list = new List<Value>();
			for (int i = 0; i < values.Length; i++)
			{
				Array array = (Array)values[i];
				for (int j = 0, k = array.Count; j < k; j++)
					list.Add(array[j]);
			}
			return Array.From(list);
		}

		[Builtin("compact")]
		public static Value Compact(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			Array array = (Array)values[0];
			List<Value> list = new List<Value>(array.Count);
			for (int i = 0, j = array.Count; i < j; i++)
			{
				Value value = array[i];
				if (value == null)
					continue;
				switch (value.Type)
				{
				case ValueType.String:
					if (((string)value).Length == 0)
						continue;
					break;
				case ValueType.Array:
					if (((Array)value).Count == 0)
						continue;
					break;
				}
				list.Add(array[i]);
			}
			return Array.From(list);
		}

		[Builtin("limit")]
		public static Value Limit(Value[] values)
		{
			if (values.Length != 2)
				throw new InvalidOperationException();
			Array array = (Array)values[0];
			int limit = (int)values[1];
			if (limit >= array.Count)
				return values[0];
			Value[] results = new Value[limit];
			for (int i = 0; i < limit; i++)
				results[i] = array[i];
			return Array.From(results);
		}

		[Builtin("map")]
		public static Value Map(Value[] values)
		{
			if (values.Length != 2)
				throw new InvalidOperationException();
			Array array = (Array)values[0];
			if (values[1] != null)
			{
				Value[] results = new Value[array.Count];
				switch (values[1].Type)
				{
				case ValueType.Number:
					int index = (int)values[1];
					for (int i = 0; i < results.Length; i++)
						results[i] = ((Array)array[i])[index];
					return Array.From(results);
				case ValueType.String:
					string member = (string)values[1];
					for (int i = 0; i < results.Length; i++)
						results[i] = ((Table)array[i])[member];
					return Array.From(results);
				}
			}
			throw new ArgumentOutOfRangeException();
		}

		[Builtin("sort")]
		public static Value Sort(Value[] values)
		{
			if (values.Length != 1 && values.Length != 2)
				throw new InvalidOperationException();
			Array array = (Array)values[0];
			List<Value> results = new List<Value>(array.Count);
			for (int i = 0, j = array.Count; i < j; i++)
				results.Add(array[i]);
			if (values.Length == 1)
			{
				results.Sort(Compare);
				return Array.From(results);
			}
			if (values[1] != null)
			{
				switch (values[1].Type)
				{
				case ValueType.Number:
					int index = (int)values[1];
					results.Sort((left, right) => Compare(((Array)left)[index], ((Array)right)[index]));
					return Array.From(results);
				case ValueType.String:
					string member = (string)values[1];
					results.Sort((left, right) => Compare(((Table)left)[member], ((Table)right)[member]));
					return Array.From(results);
				}
			}
			throw new ArgumentOutOfRangeException();
		}

		[Builtin("unique")]
		public static Value Unique(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			Array array = (Array)values[0];
			int count = array.Count;
			List<Value> list = new List<Value>(count);
			HashSet<double> numbers = new HashSet<double>();
			HashSet<string> strings = new HashSet<string>();
			bool hasTrue = false;
			bool hasFalse = false;
			bool hasNull = false;
			for (int i = 0; i < count; i++)
			{
				Value value = array[i];
				if (value == null)
				{
					if (!hasNull)
					{
						hasNull = true;
						list.Add(null);
					}
				}
				else
				{
					switch (value.Type)
					{
					case ValueType.Boolean:
						if ((bool)value)
						{
							if (!hasTrue)
							{
								hasTrue = true;
								list.Add(value);
							}
						}
						else
						{
							if (!hasFalse)
							{
								hasFalse = true;
								list.Add(value);
							}
						}
						break;
					case ValueType.Number:
						if (numbers.Add((double)value))
							list.Add(value);
						break;
					case ValueType.String:
						if (strings.Add((string)value))
							list.Add(value);
						break;
					case ValueType.Table:
						list.Add(value);
						break;
					case ValueType.Array:
						list.Add(value);
						break;
					}
				}
			}
			return Array.From(list);
		}

		[Builtin("contains")]
		public static Value Contains(Value[] values)
		{
			if (values.Length != 2)
				throw new InvalidOperationException();
			Array array = (Array)values[0];
			for (int i = 0, j = array.Count; i < j; i++)
			{
				if (array[i] == values[1])
					return true;
			}
			return false;
		}

		private static int Compare(Value left, Value right)
		{
			if (left == null)
				return right == null ? 0 : 1;
			if (right == null)
				return -1;
			if (left.Type == right.Type)
			{
				switch (left.Type)
				{
				case ValueType.Number:
					return ((double)left).CompareTo((double)right);
				case ValueType.String:
					return string.Compare((string)left, (string)right, StringComparison.Ordinal);
				}
			}
			throw new InvalidOperationException();
		}
	}
}
