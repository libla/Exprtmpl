using System;
using System.Text;
using System.Threading;

namespace Exprtmpl
{
	[Builtin("string")]
	internal static class StringLibrary
	{
		private static readonly ThreadLocal<StringBuilder> localbuilder =
			new ThreadLocal<StringBuilder>(() => new StringBuilder());

		[Builtin("upper")]
		public static Value Upper(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			return ((string)values[0]).ToUpperInvariant();
		}

		[Builtin("lower")]
		public static Value Lower(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			return ((string)values[0]).ToLowerInvariant();
		}

		[Builtin("repeat")]
		public static Value Repeat(Value[] values)
		{
			if (values.Length != 2)
				throw new InvalidOperationException();
			string str = (string)values[0];
			int count = (int)values[1];
			StringBuilder builder = localbuilder.Value;
			builder.Clear();
			builder.Capacity = Math.Max(builder.Capacity, str.Length * count);
			for (int i = 0; i < count; i++)
				builder.Append(str);
			return builder.ToString();
		}

		[Builtin("reverse")]
		public static Value Reverse(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			string str = (string)values[0];
			StringBuilder builder = localbuilder.Value;
			builder.Clear();
			builder.Capacity = Math.Max(builder.Capacity, str.Length);
			for (int i = str.Length - 1; i >= 0; i--)
				builder.Append(str[i]);
			return builder.ToString();
		}

		[Builtin("contains")]
		public static Value Contains(Value[] values)
		{
			if (values.Length != 2)
				throw new InvalidOperationException();
			return ((string)values[0]).Contains((string)values[1]);
		}

		[Builtin("starts")]
		public static Value StartsWith(Value[] values)
		{
			if (values.Length != 2)
				throw new InvalidOperationException();
			return ((string)values[0]).StartsWith((string)values[1]);
		}

		[Builtin("ends")]
		public static Value EndsWith(Value[] values)
		{
			if (values.Length != 2)
				throw new InvalidOperationException();
			return ((string)values[0]).EndsWith((string)values[1]);
		}

		[Builtin("strip")]
		public static Value Trim(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			return ((string)values[0]).Trim();
		}

		[Builtin("lstrip")]
		public static Value TrimStart(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			return ((string)values[0]).TrimStart();
		}

		[Builtin("rstrip")]
		public static Value TrimEnd(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			return ((string)values[0]).TrimEnd();
		}

		[Builtin("remove")]
		public static Value Remove(Value[] values)
		{
			if (values.Length != 2)
				throw new InvalidOperationException();
			return ((string)values[0]).Replace((string)values[1], "");
		}

		[Builtin("replace")]
		public static Value Replace(Value[] values)
		{
			if (values.Length != 3)
				throw new InvalidOperationException();
			string search = (string)values[1];
			string replace = (string)values[2];
			if (search.Length == 1 && replace.Length == 1)
				return ((string)values[0]).Replace(search[0], replace[0]);
			return ((string)values[0]).Replace(search, replace);
		}

		[Builtin("split")]
		public static Value Split(Value[] values)
		{
			if (values.Length < 2)
				throw new InvalidOperationException();
			string[] separator = new string[values.Length - 1];
			for (int i = 1; i < values.Length; i++)
				separator[i - 1] = (string)values[i];
			string[] split = ((string)values[0]).Split(separator, StringSplitOptions.None);
			Value[] result = new Value[split.Length];
			for (int i = 0; i < split.Length; i++)
				result[i] = split[i];
			return Array.From(result);
		}

		[Builtin("left")]
		public static Value PadLeft(Value[] values)
		{
			if (values.Length != 2)
				throw new InvalidOperationException();
			return ((string)values[0]).PadLeft((int)values[1]);
		}

		[Builtin("right")]
		public static Value PadRight(Value[] values)
		{
			if (values.Length != 2)
				throw new InvalidOperationException();
			return ((string)values[0]).PadRight((int)values[1]);
		}

		[Builtin("find")]
		public static Value IndexOf(Value[] values)
		{
			if (values.Length < 2 || values.Length > 4)
				throw new InvalidOperationException();
			string search = (string)values[1];
			int index;
			if (search.Length == 1)
			{
				switch (values.Length)
				{
				case 2:
					index = ((string)values[0]).IndexOf(search[0]);
					break;
				case 3:
					index = ((string)values[0]).IndexOf(search[0], (int)values[1]);
					break;
				default:
					int start = (int)values[1];
					index = ((string)values[0]).IndexOf(search[0], start, (int)values[2] - start + 1);
					break;
				}
			}
			else
			{
				switch (values.Length)
				{
				case 2:
					index = ((string)values[0]).IndexOf(search, StringComparison.Ordinal);
					break;
				case 3:
					index = ((string)values[0]).IndexOf(search, (int)values[1], StringComparison.Ordinal);
					break;
				default:
					int start = (int)values[1];
					index = ((string)values[0]).IndexOf(search, start, (int)values[2] - start + 1, StringComparison.Ordinal);
					break;
				}
			}
			if (index == -1)
				return null;
			return index;
		}

		[Builtin("findlast")]
		public static Value LastIndexOf(Value[] values)
		{
			if (values.Length < 2 || values.Length > 4)
				throw new InvalidOperationException();
			string search = (string)values[1];
			int index;
			if (search.Length == 1)
			{
				switch (values.Length)
				{
				case 2:
					index = ((string)values[0]).LastIndexOf(search[0]);
					break;
				case 3:
					index = ((string)values[0]).LastIndexOf(search[0], (int)values[1]);
					break;
				default:
					int start = (int)values[2];
					index = ((string)values[0]).LastIndexOf(search[0], start, start - (int)values[1] + 1);
					break;
				}
			}
			else
			{
				switch (values.Length)
				{
				case 2:
					index = ((string)values[0]).LastIndexOf(search, StringComparison.Ordinal);
					break;
				case 3:
					index = ((string)values[0]).LastIndexOf(search, (int)values[1], StringComparison.Ordinal);
					break;
				default:
					int start = (int)values[2];
					index = ((string)values[0]).LastIndexOf(search, start, start - (int)values[1] + 1, StringComparison.Ordinal);
					break;
				}
			}
			if (index == -1)
				return null;
			return index;
		}

		[Builtin("concat")]
		public static Value Concat(Value[] values)
		{
			if (values.Length == 0)
				throw new InvalidOperationException();
			StringBuilder builder = localbuilder.Value;
			builder.Clear();
			if (values.Length == 1)
			{
				Array array = (Array)values[0];
				for (int i = 0, j = array.Count; i < j; i++)
				{
					Value value = array[i];
					if (value == null)
						throw new InvalidCastException();
					builder.Append(value.ToString());
				}
			}
			else
			{
				for (int i = 0; i < values.Length; i++)
				{
					Value value = values[i];
					if (value == null)
						throw new InvalidCastException();
					builder.Append(value.ToString());
				}
			}
			return builder.ToString();
		}

		[Builtin("join")]
		public static Value Join(Value[] values)
		{
			if (values.Length < 2)
				throw new InvalidOperationException();
			StringBuilder builder = localbuilder.Value;
			builder.Clear();
			string separator = (string)values[0];
			if (values.Length == 2)
			{
				Array array = (Array)values[1];
				for (int i = 0, j = array.Count; i < j; i++)
				{
					if (i != 0)
						builder.Append(separator);
					Value value = array[i];
					if (value == null)
						throw new InvalidCastException();
					builder.Append(value.ToString());
				}
			}
			else
			{
				for (int i = 1; i < values.Length; i++)
				{
					if (i != 1)
						builder.Append(separator);
					Value value = values[i];
					if (value == null)
						throw new InvalidCastException();
					builder.Append(value.ToString());
				}
			}
			return builder.ToString();
		}

		[Builtin("format")]
		public static Value Format(Value[] values)
		{
			if (values.Length < 1)
				throw new InvalidOperationException();
			string format = (string)values[0];
			StringBuilder builder = localbuilder.Value;
			builder.Clear();
			int? escape = null;
			for (int i = 0; i < format.Length; i++)
			{
				char c = format[i];
				if (escape.HasValue)
				{
					char start = format[escape.Value];
					if (start == '{')
					{
						switch (c)
						{
						case '{':
							escape = null;
							builder.Append('{');
							break;
						case '}':
							Value value =
								values[int.Parse(format.Substring(escape.Value + 1, c - escape.Value - 1)) + 1];
							if (value == null)
								throw new InvalidCastException();
							builder.Append(value.ToString());
							break;
						}
					}
					else if (c == '}')
					{
						escape = null;
						builder.Append('}');
					}
					else
					{
						throw new FormatException();
					}
				}
				else
				{
					switch (c)
					{
					case '{':
					case '}':
						escape = i;
						break;
					}
				}
			}
			if (escape.HasValue)
				throw new FormatException();
			return builder.ToString();
		}
	}
}
