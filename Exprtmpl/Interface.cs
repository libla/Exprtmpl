using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

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

		public sealed override string ToString()
		{
			throw new NotImplementedException();
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

		public static Table From<T>(T value)
		{
			return value.GetType() == typeof(T) ? CastTable<T>.Convert(value) : From((object)value);
		}

		public static Table From(object value)
		{
			Func<object, Table> creator = creators.GetOrAdd(value.GetType(), factory);
			return creator(value);
		}

		private static readonly ConcurrentDictionary<Type, Func<object, Table>> creators =
			new ConcurrentDictionary<Type, Func<object, Table>>();
		private static readonly Func<Type, Func<object, Table>> factory;

		static Table()
		{
			factory = type =>
			{
				return typeof(Table)
					.GetMethod("GetCreator", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
					.MakeGenericMethod(type)
					.Invoke(null, null) as Func<object, Table>;
			};
		}

		private static Func<object, Table> GetCreator<T>()
		{
			return value => CastTable<T>.Convert((T)value);
		}
	}

#pragma warning disable 659,660,661
	public abstract class Array : IEnumerable<Value>, IEquatable<Array>
#pragma warning restore 659,660,661
	{
		private static readonly MethodInfo from;

		static Array()
		{
			foreach (MethodInfo method in typeof(Array).GetMethods(
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
			{
				if (method.Name == "From" && method.IsGenericMethodDefinition)
				{
					from = method;
					break;
				}
			}
		}

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

		public sealed override string ToString()
		{
			throw new NotImplementedException();
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

		public static Array From(IReadOnlyList<Value> values)
		{
			return new ListArray(values);
		}

		public static Array From<T>(IReadOnlyList<T> values)
		{
			return new ListArray<T>(values);
		}

		private class ListArray : Array
		{
			private readonly IReadOnlyList<Value> values;

			public ListArray(IReadOnlyList<Value> values)
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

		private class ListArray<T> : Array
		{
			private static readonly Func<IReadOnlyList<T>, int, Value> getValue;

			static ListArray()
			{
				switch (Type.GetTypeCode(typeof(T)))
				{
				case TypeCode.Boolean:
					getValue = (list, index) => ((IReadOnlyList<bool>)list)[index];
					break;
				case TypeCode.Char:
					getValue = (list, index) => new string(((IReadOnlyList<char>)list)[index], 1);
					break;
				case TypeCode.SByte:
					getValue = (list, index) => ((IReadOnlyList<sbyte>)list)[index];
					break;
				case TypeCode.Byte:
					getValue = (list, index) => ((IReadOnlyList<byte>)list)[index];
					break;
				case TypeCode.Int16:
					getValue = (list, index) => ((IReadOnlyList<short>)list)[index];
					break;
				case TypeCode.UInt16:
					getValue = (list, index) => ((IReadOnlyList<ushort>)list)[index];
					break;
				case TypeCode.Int32:
					getValue = (list, index) => ((IReadOnlyList<int>)list)[index];
					break;
				case TypeCode.UInt32:
					getValue = (list, index) => ((IReadOnlyList<uint>)list)[index];
					break;
				case TypeCode.Int64:
					getValue = (list, index) => ((IReadOnlyList<long>)list)[index];
					break;
				case TypeCode.UInt64:
					getValue = (list, index) => ((IReadOnlyList<ulong>)list)[index];
					break;
				case TypeCode.Single:
					getValue = (list, index) => ((IReadOnlyList<float>)list)[index];
					break;
				case TypeCode.Double:
					getValue = (list, index) => ((IReadOnlyList<double>)list)[index];
					break;
				case TypeCode.Decimal:
					getValue = (list, index) => (double)((IReadOnlyList<decimal>)list)[index];
					break;
				case TypeCode.String:
					getValue = (list, index) => ((IReadOnlyList<string>)list)[index];
					break;
				default:
					Type result = typeof(T).GetInterfaces()
										.FirstOrDefault(t => t.IsGenericType &&
															t.GetGenericTypeDefinition() == typeof(IReadOnlyList<>));
					if (result == null)
					{
						getValue = (list, index) => Table.From(list[index]);
					}
					else
					{
						Type arg = result.GetGenericArguments()[0];
						if (arg == typeof(Value) || arg.IsSubclassOf(typeof(Value)))
						{
							getValue = (list, index) => From((IReadOnlyList<Value>)list[index]);
						}
						else
						{
							var parameter = Expression.Parameter(typeof(T));
							var convert = Expression
								.Lambda<Func<T, Value>>(
										Expression.Convert(Expression.Call(from.MakeGenericMethod(arg), parameter),
															typeof(Value)), parameter)
								.Compile();
							getValue = (list, index) => convert(list[index]);
						}
					}
					break;
				}
			}

			private readonly IReadOnlyList<T> values;
			private readonly Dictionary<int, Value> caches = new Dictionary<int, Value>();

			public ListArray(IReadOnlyList<T> values)
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
					Value value;
					if (!caches.TryGetValue(index, out value))
					{
						value = getValue(values, index);
						caches.Add(index, value);
					}
					return value;
				}
			}

			public override bool Equals(Array other)
			{
				ListArray<T> rhs = other as ListArray<T>;
				return rhs != null && ReferenceEquals(values, rhs.values);
			}
		}
	}

	public abstract class Value
	{
		internal Value() { }

		public abstract ValueType Type { get; }

		public override string ToString()
		{
			throw new NotImplementedException();
		}

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
			return value ? BooleanValue.True : BooleanValue.False;
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
			public static BooleanValue True = new BooleanValue(true);
			public static BooleanValue False = new BooleanValue(false);

			private readonly bool value;

			private BooleanValue(bool value)
			{
				this.value = value;
			}

			public override ValueType Type
			{
				get { return ValueType.Boolean; }
			}

			public override string ToString()
			{
				return value ? "true" : "false";
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

			public override string ToString()
			{
				return value.ToString("G17");
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

			public override string ToString()
			{
				return value;
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

			public override string ToString()
			{
				return value.ToString();
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

			public override string ToString()
			{
				return value.ToString();
			}

			protected override Array CastToArray()
			{
				return value;
			}
		}
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
