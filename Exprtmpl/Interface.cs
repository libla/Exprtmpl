using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Exprtmpl
{
#pragma warning disable 659,660,661
	public abstract class Table : IEnumerable<KeyValuePair<string, Value>>, IEquatable<Table>
#pragma warning restore 659,660,661
	{
		public abstract IEnumerable<string> Keys();
		public abstract Value this[string key] { get; }

		public virtual bool Equals(Table other)
		{
			return ReferenceEquals(this, other);
		}

		public IEnumerator<KeyValuePair<string, Value>> GetEnumerator()
		{
			foreach (var key in Keys())
				yield return new KeyValuePair<string, Value>(key, this[key]);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

#pragma warning disable 659
		public sealed override bool Equals(object obj)
#pragma warning restore 659
		{
			return Equals(obj as Table);
		}

		public static bool operator == (Table left, Table right)
		{
			if (left == null)
				return right == null;
			return !(right == null) && left.Equals(right);
		}

		public static bool operator != (Table left, Table right)
		{
			return !(left == right);
		}

		public static Table From(IDictionary<string, Value> values)
		{
			return new DictionaryTable(values);
		}

		private class DictionaryTable : Table
		{
			private readonly IDictionary<string, Value> values;

			public DictionaryTable(IDictionary<string, Value> values)
			{
				this.values = values;
			}

			public override IEnumerable<string> Keys()
			{
				return values.Keys;
			}

			public override Value this[string key]
			{
				get
				{
					Value value;
					return values.TryGetValue(key, out value) ? value : null;
				}
			}

			public override bool Equals(Table other)
			{
				DictionaryTable rhs = other as DictionaryTable;
				return rhs != null && ReferenceEquals(values, rhs.values);
			}
		}
	}

#pragma warning disable 659,660,661
	public abstract class Array : IEnumerable<Value>, IEquatable<Array>
#pragma warning restore 659,660,661
	{
		public abstract int Count { get; }
		public abstract Value this[int index] { get; }

		public virtual bool Equals(Array other)
		{
			return ReferenceEquals(this, other);
		}

		public IEnumerator<Value> GetEnumerator()
		{
			for (int i = 0, j = Count; i < j; i++)
				yield return this[i];
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

#pragma warning disable 659
		public sealed override bool Equals(object obj)
#pragma warning restore 659
		{
			return Equals(obj as Array);
		}

		public static bool operator == (Array left, Array right)
		{
			if (left == null)
				return right == null;
			return !(right == null) && left.Equals(right);
		}

		public static bool operator != (Array left, Array right)
		{
			return !(left == right);
		}

		public static Array From(IList<Value> values)
		{
			return new ListArray(values);
		}

		private class ListArray : Array
		{
			private readonly IList<Value> values;

			public ListArray(IList<Value> values)
			{
				this.values = values;
			}

			public override int Count
			{
				get { return values.Count; }
			}

			public override Value this[int index]
			{
				get
				{
					if (index < 0 || index >= values.Count)
						return null;
					return values[index];
				}
			}

			public override bool Equals(Array other)
			{
				ListArray rhs = other as ListArray;
				return rhs != null && ReferenceEquals(values, rhs.values);
			}
		}
	}

	public abstract class Value
	{
		internal Value() { }

		public abstract ValueType Type { get; }

		public static explicit operator bool(Value value)
		{
			return value.CastToBoolean();
		}

		public static explicit operator int(Value value)
		{
			double d = value.CastToNumber();
			double r = Math.Round(d);
			if (Math.Abs(d - r) < double.Epsilon && r >= int.MinValue && r <= int.MaxValue)
				return (int)r;
			throw new InvalidCastException();
		}

		public static explicit operator double(Value value)
		{
			return value.CastToNumber();
		}

		public static explicit operator string(Value value)
		{
			return value.CastToString();
		}

		public static explicit operator Table(Value value)
		{
			return value.CastToTable();
		}

		public static explicit operator Array(Value value)
		{
			return value.CastToArray();
		}

		protected virtual bool CastToBoolean()
		{
			throw new InvalidCastException();
		}

		protected virtual double CastToNumber()
		{
			throw new InvalidCastException();
		}

		protected virtual string CastToString()
		{
			throw new InvalidCastException();
		}

		protected virtual Table CastToTable()
		{
			throw new InvalidCastException();
		}

		protected virtual Array CastToArray()
		{
			throw new InvalidCastException();
		}

		public static implicit operator Value(bool value)
		{
			return new BooleanValue(value);
		}

		public static implicit operator Value(double value)
		{
			return new NumberValue(value);
		}

		public static implicit operator Value(string value)
		{
			return new StringValue(value);
		}

		public static implicit operator Value(Table value)
		{
			return new TableValue(value);
		}

		public static implicit operator Value(Array value)
		{
			return new ArrayValue(value);
		}

		private class BooleanValue : Value
		{
			private readonly bool value;

			public BooleanValue(bool value)
			{
				this.value = value;
			}

			public override ValueType Type
			{
				get { return ValueType.Boolean; }
			}

			protected override bool CastToBoolean()
			{
				return value;
			}
		}

		private class NumberValue : Value
		{
			private readonly double value;

			public NumberValue(double value)
			{
				this.value = value;
			}

			public override ValueType Type
			{
				get { return ValueType.Number; }
			}

			protected override double CastToNumber()
			{
				return value;
			}
		}

		private class StringValue : Value
		{
			private readonly string value;

			public StringValue(string value)
			{
				this.value = value;
			}

			public override ValueType Type
			{
				get { return ValueType.String; }
			}

			protected override string CastToString()
			{
				return value;
			}
		}

		private class TableValue : Value
		{
			private readonly Table value;

			public TableValue(Table value)
			{
				this.value = value;
			}

			public override ValueType Type
			{
				get { return ValueType.Table; }
			}

			protected override Table CastToTable()
			{
				return value;
			}
		}

		private class ArrayValue : Value
		{
			private readonly Array value;

			public ArrayValue(Array value)
			{
				this.value = value;
			}

			public override ValueType Type
			{
				get { return ValueType.Array; }
			}

			protected override Array CastToArray()
			{
				return value;
			}
		}
	}

	public interface ILoader
	{
		Task<string> Load(string file);
		Task<string> Load(string file, string old);
	}

	public enum ValueType
	{
		Boolean,
		Number,
		String,
		Table,
		Array,
	}
}
