using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Exprtmpl
{
	internal static class Entity
	{
		private static readonly ConcurrentDictionary<Type, Define> defines = new ConcurrentDictionary<Type, Define>();
		private static readonly Func<Type, Define> factory;

		private static readonly MethodInfo GetFromChar;
		private static readonly MethodInfo GetFromObject;
		private static readonly MethodInfo GetFromDictionary;
		private static readonly MethodInfo GetFromITable;
		private static readonly MethodInfo GetFromList;
		private static readonly MethodInfo GetFromListDictionary;
		private static readonly MethodInfo GetFromListITable;
		private static readonly MethodInfo GetFromListObject;
		private static readonly MethodInfo GetCallMethod;

		static Entity()
		{
			factory = type =>
			{
				Type basetype = type.BaseType;
				HashSet<string> names = new HashSet<string>();
				Dictionary<string, Expression> bodys = new Dictionary<string, Expression>();
				ParameterExpression value = Expression.Parameter(typeof(object));
				ParameterExpression self = Expression.Variable(type);
				foreach (MemberInfo member in type.GetMembers(BindingFlags.Instance | BindingFlags.Public))
				{
					if ((member.MemberType & (MemberTypes.Field | MemberTypes.Property)) != 0)
					{
						names.Add(member.Name);
						if (member.DeclaringType != type)
							continue;
						if (member.MemberType == MemberTypes.Property && ((PropertyInfo)member).IsSpecialName)
							continue;
						Expression expr = Expression.PropertyOrField(self, member.Name);
						Type mtype = member.MemberType == MemberTypes.Field
							? ((FieldInfo)member).FieldType
							: ((PropertyInfo)member).PropertyType;
						switch (Type.GetTypeCode(mtype))
						{
						case TypeCode.Boolean:
						case TypeCode.Double:
						case TypeCode.String:
							expr = Expression.Convert(expr, typeof(Value));
							break;
						case TypeCode.SByte:
						case TypeCode.Int16:
						case TypeCode.Int32:
						case TypeCode.Int64:
						case TypeCode.Byte:
						case TypeCode.UInt16:
						case TypeCode.UInt32:
						case TypeCode.UInt64:
						case TypeCode.Single:
						case TypeCode.Decimal:
							expr = Expression.Convert(Expression.Convert(expr, typeof(double)), typeof(Value));
							break;
						case TypeCode.Char:
							expr = Expression.Call(GetFromChar, expr);
							break;
						default:
							if (mtype == typeof(Value) || mtype.IsSubclassOf(typeof(Value)))
								break;
							if (mtype == typeof(Table) || mtype.IsSubclassOf(typeof(Table)) || mtype == typeof(Array) || mtype.IsSubclassOf(typeof(Array)))
							{
								expr = Expression.Convert(expr, typeof(Value));
							}
							else
							{
								Type result = mtype.GetInterfaces()
												.FirstOrDefault(
														t => t == typeof(ITable) ||
															t == typeof(IReadOnlyDictionary<string, Value>));
								if (result == typeof(ITable))
								{
									expr = Expression.Call(GetFromITable.MakeGenericMethod(mtype), expr);
								}
								else if (result == typeof(IReadOnlyDictionary<string, Value>))
								{
									expr = Expression.Call(GetFromDictionary, expr);
								}
								else
								{
									result = mtype.GetInterfaces()
											.FirstOrDefault(
													t => t.IsGenericType && t.GetGenericTypeDefinition() ==
														typeof(IReadOnlyList<>));
									if (result == null)
									{
										expr = Expression.Call(GetFromObject, expr);
									}
									else
									{
										Type arg = result.GetGenericArguments()[0];
										if (arg == typeof(Value) || arg.IsSubclassOf(typeof(Value)))
										{
											expr = Expression.Call(GetFromList, expr);
										}
										else
										{
											result = mtype.GetInterfaces()
													.FirstOrDefault(
															t => t == typeof(ITable) ||
																t == typeof(IReadOnlyDictionary<string, Value>));
											if (result == typeof(ITable))
												expr = Expression.Call(GetFromListITable.MakeGenericMethod(mtype), expr);
											else if (result == typeof(IReadOnlyDictionary<string, Value>))
												expr = Expression.Call(GetFromListDictionary, expr);
											else
												expr = Expression.Call(GetFromListObject, expr);
										}
									}
								}
							}
							break;
						}
						bodys.Add(member.Name, expr);
					}
				}
				if (names.Count == 0)
					return null;
				Define parent = basetype == null || basetype == typeof(object)
					? null
					: defines.GetOrAdd(basetype, factory);
				if (bodys.Count == 0)
					return parent == null ? null : new Define(names.ToArray(), parent.Index);
				ParameterExpression name = Expression.Parameter(typeof(string));
				Expression def;
				if (parent == null)
					def = Expression.Constant(null, typeof(Value));
				else
					def = Expression.Call(GetCallMethod, Expression.Constant(parent.Index), self, name);
				return new Define(names.ToArray(),
								Expression.Lambda<Func<object, string, Value>>(
												Expression.Block(new[] {self},
																Expression.Assign(
																	self, Expression.Convert(value, type)),
																Expression.Switch(
																	name, def,
																	bodys.Select(body => Expression.SwitchCase(
																					body.Value,
																					Expression.Constant(body.Key)))
																	.ToArray())), value, name)
										.Compile());
			};

			GetFromChar = typeof(Entity).GetMethod("FromChar", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
			GetFromObject = typeof(Entity).GetMethod("FromObject", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
			GetFromDictionary = typeof(Entity).GetMethod("FromDictionary", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
			GetFromITable = typeof(Entity).GetMethod("FromITable", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
			GetFromList = typeof(Entity).GetMethod("FromList", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
			GetFromListDictionary = typeof(Entity).GetMethod("FromListDictionary", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
			GetFromListITable = typeof(Entity).GetMethod("FromListITable", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
			GetFromListObject = typeof(Entity).GetMethod("FromListObject", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
			GetCallMethod = typeof(Entity).GetMethod("CallMethod", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
		}

		public static Table Create(object o)
		{
			Define define = defines.GetOrAdd(o.GetType(), factory);
			return define == null ? null : new Adapter(o, define);
		}

		private static Value FromChar(char c)
		{
			return new string(c, 1);
		}

		private static Value FromObject(object o)
		{
			return Table.From(o);
		}

		private static Value FromDictionary(IReadOnlyDictionary<string, Value> values)
		{
			return Table.From(values);
		}

		private static Value FromITable<T>(T table) where T : ITable
		{
			return table.ToTable();
		}

		private static Value FromList(IReadOnlyList<Value> values)
		{
			return Array.From(values);
		}

		private static Value FromListDictionary(IReadOnlyList<IReadOnlyDictionary<string, Value>> values)
		{
			return Array.From(values);
		}

		private static Value FromListITable<T>(IReadOnlyList<T> values) where T : ITable
		{
			return Array.From(values);
		}

		private static Value FromListObject(IReadOnlyList<object> values)
		{
			return Array.From(values);
		}

		private static Value CallMethod(Func<object, string, Value> method, object value, string name)
		{
			return method(value, name);
		}

		private class Define
		{
			public readonly IEnumerable<string> Keys;
			public readonly Func<object, string, Value> Index;

			public Define(IEnumerable<string> keys, Func<object, string, Value> index)
			{
				Keys = keys;
				Index = index;
			}
		}

		private class Adapter : Table
		{
			private readonly object o;
			private readonly Define define;

			public Adapter(object o, Define define)
			{
				this.o = o;
				this.define = define;
			}

			public override IEnumerable<string> Keys()
			{
				return define.Keys;
			}

			public override Value this[string key]
			{
				get { return define.Index(o, key); }
			}

			public override bool Equals(Table other)
			{
				Adapter rhs = other as Adapter;
				return rhs != null && o.Equals(rhs.o);
			}
		}
	}
}