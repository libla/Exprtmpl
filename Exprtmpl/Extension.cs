using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
	}
}
