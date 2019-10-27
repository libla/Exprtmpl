using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Exprtmpl
{
	internal static class CastTable
	{
		public static readonly MethodInfo From1;
		public static readonly MethodInfo From2;
		public static readonly MethodInfo Convert;
		public static readonly MethodInfo Call;

		static CastTable()
		{
			From1 = typeof(CastTable).GetMethod("FromDictionary1",
											BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
			From2 = typeof(CastTable).GetMethod("FromDictionary2",
											BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
			Convert = typeof(CastTable).GetMethod("ConvertType",
												BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
			Call = typeof(CastTable).GetMethod("CallMethod",
												BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
		}

		private static Table FromDictionary1<T>(IReadOnlyDictionary<string, T> values)
		{
			return new DictionaryTable<T>(values);
		}

		private static Table FromDictionary2<T1, T2>(T1 values) where T1 : IReadOnlyDictionary<string, T2>
		{
			return new DictionaryTable<T1, T2>(values);
		}

		private static Value ConvertType<T>(T value)
		{
			return CastValue<T>.Convert(value);
		}

		private static Value CallMethod<T>(T value, string name)
		{
			return CastTable<T>.GetValue != null ? CastTable<T>.GetValue(value, name) : null;
		}

		private class DictionaryTable<T> : Table
		{
			private readonly IReadOnlyDictionary<string, T> values;

			public DictionaryTable(IReadOnlyDictionary<string, T> values)
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
					T value;
					return values.TryGetValue(key, out value) ? CastValue<T>.Convert(value) : null;
				}
			}

			public override bool Equals(Table other)
			{
				DictionaryTable<T> rhs = other as DictionaryTable<T>;
				return rhs != null && ReferenceEquals(values, rhs.values);
			}
		}

		private class DictionaryTable<T1, T2> : Table where T1 : IReadOnlyDictionary<string, T2>
		{
			private readonly T1 values;

			public DictionaryTable(T1 values)
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
					T2 value;
					return values.TryGetValue(key, out value) ? CastValue<T2>.Convert(value) : null;
				}
			}

			public override bool Equals(Table other)
			{
				DictionaryTable<T1, T2> rhs = other as DictionaryTable<T1, T2>;
				return rhs != null && ReferenceEquals(values, rhs.values);
			}
		}
	}

	internal static class CastTable<T>
	{
		public static readonly Func<T, Table> Convert;
		public static readonly Func<T, string, Value> GetValue;

		static CastTable()
		{
			Type type = typeof(T);
			Type dictionary = type.GetInterfaces()
								.FirstOrDefault(t => t.IsGenericType &&
													t.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>) &&
													t.GetGenericArguments()[0] == typeof(string));
			if (dictionary == null)
			{
				HashSet<string> names = new HashSet<string>();
				Dictionary<string, Expression> bodys = new Dictionary<string, Expression>();
				ParameterExpression paramValue = Expression.Parameter(type);
				foreach (MemberInfo member in type.GetMembers(BindingFlags.Instance | BindingFlags.Public))
				{
					if ((member.MemberType & (MemberTypes.Field | MemberTypes.Property)) != 0)
					{
						string membername = member.Name;
						names.Add(membername);
						if (member.DeclaringType != type)
							continue;
						if (member.MemberType == MemberTypes.Property && ((PropertyInfo)member).IsSpecialName)
							continue;
						Type membertype = member.MemberType == MemberTypes.Field
							? ((FieldInfo)member).FieldType
							: ((PropertyInfo)member).PropertyType;
						bodys.Add(membername,
								Expression.Call(CastTable.Convert.MakeGenericMethod(membertype),
												Expression.PropertyOrField(paramValue, membername)));
					}
				}
				if (names.Count == 0)
				{
					Convert = value => null;
				}
				else
				{
					ParameterExpression paramName = Expression.Parameter(typeof(string));
					Type basetype = type.BaseType;
					if (bodys.Count == 0)
					{
						GetValue = Expression
								.Lambda<Func<T, string, Value>>(
										Expression.Call(CastTable.Call.MakeGenericMethod(basetype), paramValue, paramName), paramValue,
										paramName)
								.Compile();
					}
					else
					{
						Expression notmatch;
						if (basetype == null || basetype == typeof(object) || basetype == typeof(System.ValueType))
							notmatch = Expression.Constant(null, typeof(Value));
						else
							notmatch = Expression.Call(CastTable.Call.MakeGenericMethod(basetype), paramValue, paramName);
						GetValue = Expression
								.Lambda<Func<T, string, Value>>(
										Expression.Switch(
											paramName, notmatch,
											bodys.Select(body => Expression.SwitchCase(
															body.Value, Expression.Constant(body.Key)))
											.ToArray()), paramValue, paramName)
								.Compile();
					}
					string[] namearray = names.ToArray();
					Convert = value => new Adapter(value, namearray, GetValue);
				}
			}
			else
			{
				if (type.IsValueType)
				{
					Convert = (Func<T, Table>)Delegate.CreateDelegate(
						typeof(Func<T, Table>), CastTable.From2.MakeGenericMethod(type, dictionary.GetGenericArguments()[1]));
				}
				else
				{
					ParameterExpression parameter = Expression.Parameter(type);
					Convert = Expression
						.Lambda<Func<T, Table>>(
								Expression.Call(CastTable.From1.MakeGenericMethod(dictionary.GetGenericArguments()[1]),
												parameter), parameter)
						.Compile();
				}
			}
		}

		private class Adapter : Table
		{
			private readonly T value;
			private readonly string[] keys;
			private readonly Func<T, string, Value> getValue;

			public Adapter(T value, string[] keys, Func<T, string, Value> getValue)
			{
				this.value = value;
				this.keys = keys;
				this.getValue = getValue;
			}

			public override IEnumerable<string> Keys()
			{
				for (int i = 0; i < keys.Length; ++i)
					yield return keys[i];
			}

			public override Value this[string key]
			{
				get { return getValue(value, key); }
			}

			public override bool Equals(Table other)
			{
				Adapter rhs = other as Adapter;
				return rhs != null && EqualityComparer<T>.Default.Equals(value, rhs.value);
			}
		}
	}
}
