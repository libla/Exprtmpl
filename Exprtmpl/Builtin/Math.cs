using System;

namespace Exprtmpl
{
	[Builtin("math")]
	internal static class MathLibrary
	{
		[Builtin("format")]
		public static Value Format(Value[] values)
		{
			if (values.Length != 2)
				throw new InvalidOperationException();
			return ((double)values[0]).ToString((string)values[1]);
		}

		[Builtin("abs")]
		public static Value Abs(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			return Math.Abs((double)values[0]);
		}

		[Builtin("floor")]
		public static Value Floor(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			return Math.Floor((double)values[0]);
		}

		[Builtin("ceil")]
		public static Value Ceiling(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			return Math.Ceiling((double)values[0]);
		}

		[Builtin("round")]
		public static Value Round(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			return Math.Round((double)values[0]);
		}

		[Builtin("log")]
		public static Value Log(Value[] values)
		{
			if (values.Length == 1)
				return Math.Log((double)values[0]);
			if (values.Length == 2)
				return Math.Log((double)values[0], (double)values[1]);
			throw new InvalidOperationException();
			
		}

		[Builtin("exp")]
		public static Value Exp(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			return Math.Exp((double)values[0]);
		}

		[Builtin("sin")]
		public static Value Sin(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			return Math.Sin((double)values[0]);
		}

		[Builtin("cos")]
		public static Value Cos(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			return Math.Cos((double)values[0]);
		}

		[Builtin("tan")]
		public static Value Tan(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			return Math.Tan((double)values[0]);
		}

		[Builtin("asin")]
		public static Value Asin(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			return Math.Asin((double)values[0]);
		}

		[Builtin("acos")]
		public static Value Acos(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			return Math.Acos((double)values[0]);
		}

		[Builtin("atan")]
		public static Value Atan(Value[] values)
		{
			if (values.Length == 1)
				return Math.Atan((double)values[0]);
			if (values.Length == 2)
				return Math.Atan2((double)values[0], (double)values[1]);
			throw new InvalidOperationException();
		}
	}
}
