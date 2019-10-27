using System;

namespace Exprtmpl
{
	[Builtin("date")]
	internal static class DateLibrary
	{
		[Builtin("now")]
		public static Value Now(Value[] values)
		{
			return From(DateTime.Now);
		}

		[Builtin("parse")]
		public static Value Parse(Value[] values)
		{
			if (values.Length != 1)
				throw new InvalidOperationException();
			DateTime datetie;
			return DateTime.TryParse((string)values[0], out datetie) ? From(datetie) : null;
		}

		[Builtin("format")]
		public static Value Format(Value[] values)
		{
			if (values.Length != 2)
				throw new InvalidOperationException();
			return From((Table)values[0]).ToString((string)values[1]);
		}

		[Builtin("add")]
		public static Value Add(Value[] values)
		{
			if (values.Length != 2)
				throw new InvalidOperationException();
			DateTime datetime = From((Table)values[0]);
			Table table = (Table)values[1];
			Value value = table["year"];
			if (value != null)
				datetime = datetime.AddYears((int)value);
			value = table["month"];
			if (value != null)
				datetime = datetime.AddMonths((int)value);
			value = table["day"];
			if (value != null)
				datetime = datetime.AddDays((double)value);
			value = table["hour"];
			if (value != null)
				datetime = datetime.AddHours((double)value);
			value = table["minute"];
			if (value != null)
				datetime = datetime.AddMinutes((double)value);
			value = table["second"];
			if (value != null)
				datetime = datetime.AddSeconds((double)value);
			value = table["millisecond"];
			if (value != null)
				datetime = datetime.AddMilliseconds((double)value);
			return From(datetime);
		}

		private static Table From(DateTime datetime)
		{
			return Table.From(new Data {
				year = datetime.Year,
				month = datetime.Month,
				day = datetime.Day,
				hour = datetime.Hour,
				minute = datetime.Minute,
				second = datetime.Second,
				millisecond = datetime.Millisecond,
				dayofyear = datetime.DayOfYear,
				dayofweek = (int)datetime.DayOfWeek,
			});
		}

		private static DateTime From(Table table)
		{
			int year = (int)table["year"];
			int month = (int)table["month"];
			int day = (int)table["day"];

			int hour = 0;
			int minute = 0;
			int second = 0;
			int millisecond = 0;
			GetTime(table, ref hour, ref minute, ref second, ref millisecond);
			return new DateTime(year, month, day, hour, minute, second, millisecond, DateTimeKind.Local);
		}

		private static void GetTime(Table table, ref int hour, ref int minute, ref int second, ref int millisecond)
		{
			do
			{
				Value value = table["hour"];
				if (value == null)
					break;
				hour = (int)value;
				value = table["minute"];
				if (value == null)
					break;
				minute = (int)value;
				value = table["second"];
				if (value == null)
					break;
				second = (int)value;
				value = table["millisecond"];
				if (value == null)
					break;
				millisecond = (int)value;
			} while (false);
		}

		private struct Data : IEquatable<Data>
		{
			public int year;
			public int month;
			public int day;
			public int dayofyear;
			public int hour;
			public int minute;
			public int second;
			public int millisecond;
			public int dayofweek;

			public bool Equals(Data other)
			{
				return year == other.year && month == other.month && day == other.day && hour == other.hour && minute == other.minute && second == other.second && millisecond == other.millisecond;
			}
		}
	}
}
