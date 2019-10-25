using System;
using System.Runtime.CompilerServices;

namespace Exprtmpl
{
	public class Entity
	{
		public static bool IsAnonymousType(Type type)
		{
			return type.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Length > 0;
		}
	}
}