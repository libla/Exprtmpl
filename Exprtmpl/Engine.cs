using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Exprtmpl
{
	public class Engine
	{
		private static readonly IReadOnlyDictionary<string, Func<Value[], Value>>[] emptymethods =
			new IReadOnlyDictionary<string, Func<Value[], Value>>[0];

		private readonly Func<string, Task<string>> loader;
		private readonly IReadOnlyDictionary<string, Func<Value[], Value>>[] methods;

		public Engine(
			Func<string, Task<string>> loader, params IReadOnlyDictionary<string, Func<Value[], Value>>[] methods)
		{
			this.loader = loader;
			this.methods = methods;
		}

		public Engine(Func<string, Task<string>> loader) : this(loader, emptymethods) { }

		public Engine(
			Func<string, Task<TextReader>> loader, params IReadOnlyDictionary<string, Func<Value[], Value>>[] methods) :
			this(async name => await (await loader(name)).ReadToEndAsync(), methods) { }

		public Engine(Func<string, Task<TextReader>> loader) : this(
			async name => await (await loader(name)).ReadToEndAsync(), emptymethods) { }

		public Task<Func<Table, string>> Compile(string file)
		{
			Compiler compiler = new Compiler(loader, methods);
			return compiler.Compile(file);
		}
	}
}
