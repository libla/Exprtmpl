using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Exprtmpl
{
	[AttributeUsage(AttributeTargets.Class|AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
	internal class BuiltinAttribute : Attribute
	{
		public readonly string Name;

		public BuiltinAttribute() : this("") { }

		public BuiltinAttribute(string name)
		{
			Name = name;
		}
	}

	internal static class Builtin
	{
		public static readonly Dictionary<string, Func<Value[], Value>> Methods =
			new Dictionary<string, Func<Value[], Value>>();

		static Builtin()
		{
			foreach (Type type in GetAllTypes())
			{
				BuiltinAttribute typeattr = type.GetCustomAttribute<BuiltinAttribute>(false);
				if (typeattr != null)
				{
					foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
					{
						if (method.ReturnType != typeof(Value))
							continue;
						ParameterInfo[] parameters = method.GetParameters();
						if (parameters.Length != 1 || parameters[0].ParameterType != typeof(Value[]))
							continue;
						BuiltinAttribute methodattr = method.GetCustomAttribute<BuiltinAttribute>(false);
						if (methodattr != null)
						{
							string name = (string.IsNullOrEmpty(typeattr.Name) ? "" : typeattr.Name + ".") +
										methodattr.Name;
							Func<Value[], Value> func =
								(Func<Value[], Value>)Delegate.CreateDelegate(typeof(Func<Value[], Value>), method);
							Methods.Add(name, func);
						}
					}
				}
			}
		}

		private static IEnumerable<Type> GetAllTypes()
		{
			Type[] types;
			try
			{
				types = Assembly.GetExecutingAssembly().GetTypes();
			}
			catch (ReflectionTypeLoadException e)
			{
				types = e.Types;
			}
			return types.Where(type => type != null);
		}
	}
}
