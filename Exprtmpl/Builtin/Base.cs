using System;

namespace Exprtmpl
{
	[Builtin]
	internal static class BaseLibrary
	{
		[Builtin("empty")]
		public static Value Empty(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			if (values[0] == null)
				return true;
			switch (values[0].Type)
			{
			case ValueType.String:
				return ((string)values[0]).Length == 0;
			case ValueType.Array:
				return ((Array)values[0]).Count == 0;
			}
			throw new InvalidCastException();
		}

		[Builtin("type")]
		public static Value Type(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			if (values[0] == null)
				return "null";
			switch (values[0].Type)
			{
			case ValueType.Boolean:
				return "boolean";
			case ValueType.Number:
				return "number";
			case ValueType.String:
				return "string";
			case ValueType.Table:
				return "table";
			default:
				return "array";
			}
		}

		[Builtin("number")]
		public static Value ToNumber(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			if (values[0] != null)
			{
				switch (values[0].Type)
				{
				case ValueType.Number:
					return values[0];
				case ValueType.String:
					{
						double d;
						if (double.TryParse((string)values[0], out d))
							return d;
						return null;
					}
				}
			}
			throw new InvalidCastException();
		}

		[Builtin("string")]
		public static Value ToString(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			return values[0].ToString();
		}

		[Builtin("condition")]
		public static Value Condition(Value[] values)
		{
			if (values.Length != 3)
				throw new InvalidOperationException();
			return (bool)values[0] ? values[1] : values[2];
		}
	}
}