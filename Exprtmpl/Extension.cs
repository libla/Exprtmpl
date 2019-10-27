using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Exprtmpl
{
	public static class Extension
	{
		public static Task<Func<Table, string>> Compile(this TextReader reader)
		{
			Compiler compiler = new Compiler(async name =>
			{
				if (name == null)
					return await reader.ReadToEndAsync();
				throw new NotImplementedException();
			});
			return compiler.Compile(null);
		}

		public static Task<Func<Table, string>> Compile(this string str)
		{
#pragma warning disable 1998
			Compiler compiler = new Compiler(async name =>
#pragma warning restore 1998
			{
				if (name == null)
					return str;
				throw new NotImplementedException();
			});
			return compiler.Compile(null);
		}

		public static Task<Func<Table, string>> Compile(this IReadOnlyDictionary<string, string> files, string file)
		{
#pragma warning disable 1998
			Compiler compiler = new Compiler(async name => files[name]);
#pragma warning restore 1998
			return compiler.Compile(file);
		}

		public static Task<Func<Table, string>> Compile(this IReadOnlyDictionary<string, Task<string>> files, string file)
		{
			Compiler compiler = new Compiler(name => files[name]);
			return compiler.Compile(file);
		}

		public static Task<Func<Table, string>> Compile(this Func<string, Task<string>> loader, string file)
		{
			Compiler compiler = new Compiler(loader);
			return compiler.Compile(file);
		}

		public static Task<Func<Table, string>> Compile(this Func<string, Task<TextReader>> loader, string file)
		{
			Compiler compiler = new Compiler(async name => await (await loader(name)).ReadToEndAsync());
			return compiler.Compile(file);
		}

		public static Table ToTable<T>(this T value) where T : ITable
		{
			return new InterfaceTable<T>(value);
		}

		public static Array ToArray<T>(this IReadOnlyList<T> values) where T : ITable
		{
			return new InterfaceArray<T>(values);
		}

		private class InterfaceTable<T> : Table where T : ITable
		{
			private readonly T value;

			public InterfaceTable(T value)
			{
				this.value = value;
			}

			public override IEnumerable<string> Keys()
			{
				return value.Keys();
			}

			public override Value this[string key]
			{
				get { return value[key]; }
			}

			public override bool Equals(Table other)
			{
				InterfaceTable<T> rhs = other as InterfaceTable<T>;
				return rhs != null && ReferenceEquals(value, rhs.value);
			}
		}

		private class InterfaceArray<T> : Array where T : ITable
		{
			private readonly IReadOnlyList<T> values;
			private readonly Dictionary<int, Value> caches = new Dictionary<int, Value>();

			public InterfaceArray(IReadOnlyList<T> values)
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
					Value value;
					if (!caches.TryGetValue(index, out value))
					{
						value = values[index].ToTable();
						caches.Add(index, value);
					}
					return value;
				}
			}

			public override bool Equals(Array other)
			{
				InterfaceArray<T> rhs = other as InterfaceArray<T>;
				return rhs != null && ReferenceEquals(values, rhs.values);
			}
		}
	}
}
