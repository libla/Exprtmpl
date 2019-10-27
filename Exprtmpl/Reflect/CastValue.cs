using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Exprtmpl
{
	internal static class CastValue
	{
		public static readonly MethodInfo GetCharToValue;
		public static readonly MethodInfo FromArray;

		static CastValue()
		{
			GetCharToValue = typeof(CastValue).GetMethod("CharToValue",
													BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
			foreach (MethodInfo method in typeof(Array).GetMethods(
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
			{
				if (method.Name == "From" && method.IsGenericMethodDefinition)
				{
					FromArray = method;
					break;
				}
			}
		}

		private static Value CharToValue(char value)
		{
			return new string(value, 1);
		}
	}

	internal static class CastValue<T>
	{
		public static readonly Func<T, Value> Convert;

		static CastValue()
		{
			Type type = typeof(T);
			ParameterExpression parameter = Expression.Parameter(type);
			switch (Type.GetTypeCode(type))
			{
			case TypeCode.Boolean:
			case TypeCode.Double:
			case TypeCode.String:
				Convert = Expression.Lambda<Func<T, Value>>(Expression.Convert(parameter, typeof(Value)), parameter)
								.Compile();
				break;
			case TypeCode.Char:
				Convert = Expression
					.Lambda<Func<T, Value>>(Expression.Call(CastValue.GetCharToValue, parameter), parameter)
					.Compile();
				break;
			case TypeCode.SByte:
			case TypeCode.Byte:
			case TypeCode.Int16:
			case TypeCode.UInt16:
			case TypeCode.Int32:
			case TypeCode.UInt32:
			case TypeCode.Int64:
			case TypeCode.UInt64:
			case TypeCode.Single:
			case TypeCode.Decimal:
				Convert = Expression
					.Lambda<Func<T, Value>>(
							Expression.Convert(Expression.Convert(parameter, typeof(double)), typeof(Value)), parameter)
					.Compile();
				break;
			default:
				if (type == typeof(Value) || type.IsSubclassOf(typeof(Value)) || type == typeof(Array) || type.IsSubclassOf(typeof(Array)) || type == typeof(Table) || type.IsSubclassOf(typeof(Table)))
				{
					Convert = Expression.Lambda<Func<T, Value>>(Expression.Convert(parameter, typeof(Value)), parameter)
									.Compile();
				}
				else
				{
					Type result = type.GetInterfaces()
										.FirstOrDefault(t => t.IsGenericType &&
															t.GetGenericTypeDefinition() == typeof(IReadOnlyList<>));
					if (result == null)
					{
						Convert = value => CastTable<T>.Convert(value);
					}
					else
					{
						Type arg = result.GetGenericArguments()[0];
						if (arg == typeof(Value) || arg.IsSubclassOf(typeof(Value)))
						{
							Convert = value => Array.From((IReadOnlyList<Value>)value);
						}
						else
						{
							Func<T, Array> array = Expression
												.Lambda<Func<T, Array>>(
														Expression.Call(CastValue.FromArray.MakeGenericMethod(arg),
																		parameter), parameter)
												.Compile();
							Convert = value => array(value);
						}
					}
				}
				break;
			}
		}
	}
}
