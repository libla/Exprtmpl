using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace Exprtmpl
{
	internal class Compiler
	{
		private static readonly ThreadLocal<StringBuilder> localbuilder =
			new ThreadLocal<StringBuilder>(() => new StringBuilder());
		private static readonly ThreadLocal<BaseStack> localbasestack =
			new ThreadLocal<BaseStack>(() => new BaseStack());
		private static readonly ThreadLocal<Stack<InnerStack>> localstacks =
			new ThreadLocal<Stack<InnerStack>>(() => new Stack<InnerStack>());
		private static readonly ConcurrentQueue<InnerStack> freestacks = new ConcurrentQueue<InnerStack>();

		private static readonly IReadOnlyDictionary<string, Func<Value[], Value>>[] emptymethods =
			new IReadOnlyDictionary<string, Func<Value[], Value>>[0];

		private readonly Func<string, Task<string>> loader;
		private readonly IReadOnlyDictionary<string, Func<Value[], Value>>[] methods;
		private readonly Dictionary<string, Expression<Action<StringBuilder, ControlStack>>> includes;
		private ControlFlow flow;

		public Compiler(Func<string, Task<string>> loader) :
			this(loader, emptymethods, new Dictionary<string, Expression<Action<StringBuilder, ControlStack>>>()) { }

		public Compiler(
			Func<string, Task<string>> loader, params IReadOnlyDictionary<string, Func<Value[], Value>>[] methods) :
			this(loader, methods, new Dictionary<string, Expression<Action<StringBuilder, ControlStack>>>()) { }

		private Compiler(
			Func<string, Task<string>> loader, IReadOnlyDictionary<string, Func<Value[], Value>>[] methods,
			Dictionary<string, Expression<Action<StringBuilder, ControlStack>>> includes)
		{
			this.loader = loader;
			this.methods = methods;
			this.includes = includes;
			flow = new BaseControlFlow(this);
		}

		public async Task<Func<Table, string>> Compile(string file)
		{
			Expression<Action<StringBuilder, ControlStack>> lambda = await Load(file);
			Action<StringBuilder, ControlStack> format = lambda.Compile();
			return table =>
			{
				StringBuilder builder = localbuilder.Value;
				builder.Clear();
				BaseStack basestack = localbasestack.Value;
				basestack.Table = table;
				format(builder, basestack);
				basestack.Table = null;
				return builder.ToString();
			};
		}

		private async Task<Expression<Action<StringBuilder, ControlStack>>> Load(string file)
		{
			string template = await loader(file);
			int market = 0;
			int start = 0;
			int length = template.Length;
			int index = start;
			bool cr = false;
			while (index < length)
			{
				char c = template[index++];
				if (c == '\r')
				{
					if (cr)
					{
						market = await Readline(file, template, market, start, index - 2);
						start = index - 1;
					}
					cr = true;
				}
				else if (c == '\n')
				{
					market = await Readline(file, template, market, start, index - 1);
					start = index;
					cr = false;
				}
				else
				{
					if (cr)
					{
						market = await Readline(file, template, market, start, index - 2);
						cr = false;
					}
				}
			}
			if (start < length)
				market = await Readline(file, template, market, start, length - 1);
			if (market < length)
				ParseContent(file, market, template.Substring(market, length - market));
			return flow.EOF();
		}

		private async Task<int> Readline(string file, string template, int market, int start, int end)
		{
			for (int i = start; i <= end; i++)
			{
				char c = template[i];
				switch (c)
				{
				case ' ':
				case '\t':
					continue;
				case '#':
					if (market < start)
						ParseContent(file, market, template.Substring(market, start - market));
					await ParseControl(file, i + 1, template.Substring(i + 1, end - i));
					return end + 1;
				}
				break;
			}
			return market;
		}

		private void ParseContent(string file, int offset, string str)
		{
			AntlrInputStream stream = new AntlrInputStream(str);
			ExprtmplLexer lexer = new ExprtmplLexer(stream);
			lexer.PushMode(ExprtmplLexer.Content);
			ITokenStream tokens = new CommonTokenStream(lexer);
			ExprtmplParser parser = new ExprtmplParser(tokens) {BuildParseTree = true, ErrorHandler = new ErrorStrategy(offset)};
			ExprtmplParser.ContentContext context = parser.content();
			ContentListener listener = new ContentListener(this, file, offset, str);
			ParseTreeWalker.Default.Walk(listener, context);
			if (listener.Index < str.Length)
				flow.AddOutput(str.Substring(listener.Index, str.Length - listener.Index));
		}

		private Task ParseControl(string file, int offset, string str)
		{
			AntlrInputStream stream = new AntlrInputStream(str);
			ExprtmplLexer lexer = new ExprtmplLexer(stream);
			lexer.PushMode(ExprtmplLexer.DefaultMode);
			ITokenStream tokens = new CommonTokenStream(lexer);
			ExprtmplParser parser = new ExprtmplParser(tokens) {BuildParseTree = true, ErrorHandler = new ErrorStrategy(offset)};
			ExprtmplParser.ControlContext context = parser.control();
			ControlListener listener = new ControlListener(this, file, offset);
			ParseTreeWalker.Default.Walk(listener, context);
			return listener.Task;
		}

		private class ContentListener : ExprtmplParserBaseListener
		{
			private readonly Compiler compiler;
			private readonly string file;
			private readonly int offset;
			private readonly string str;
			public int Index = 0;

			public ContentListener(Compiler compiler, string file, int offset, string str)
			{
				this.compiler = compiler;
				this.file = file;
				this.offset = offset;
				this.str = str;
			}
			public override void EnterExpr(ExprtmplParser.ExprContext context)
			{
				if (Index < context.Start.StartIndex)
					compiler.flow.AddOutput(str.Substring(Index, context.Start.StartIndex - Index));
				Index = context.Stop.StopIndex + 1;
				compiler.flow.AddOutput(compiler.flow.CompileValue(context.value()));
			}
		}

		private class ControlListener : ExprtmplParserBaseListener
		{
			private static readonly Task completed;
			private Task task;
			private readonly Compiler compiler;
			private readonly string file;
			private readonly int offset;

			static ControlListener()
			{
				var builder = AsyncTaskMethodBuilder.Create();
				completed = builder.Task;
				builder.SetResult();
			}

			public ControlListener(Compiler compiler, string file, int offset)
			{
				this.compiler = compiler;
				this.offset = offset;
				this.file = file;
				this.task = completed;
			}

			public Task Task
			{
				get { return task; }
			}

			public override void EnterInclude(ExprtmplParser.IncludeContext context)
			{
				task = LoadInclude(context);
			}

			private async Task LoadInclude(ExprtmplParser.IncludeContext context)
			{
				string include = context.INCLUDE().GetText();
				bool keyword = true;
				for (int i = 0; i < include.Length; i++)
				{
					char c = include[i];
					if (keyword)
					{
						if (c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z')
							continue;
						keyword = false;
					}
					if (c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-' || c == '_')
					{
						include = include.Substring(i);
						break;
					}
				}
				Expression<Action<StringBuilder, ControlStack>> lambda;
				if (!compiler.includes.TryGetValue(include, out lambda))
				{
					Compiler includecompiler = new Compiler(compiler.loader, compiler.methods, compiler.includes);
					lambda = await includecompiler.Load(include);
					compiler.includes[include] = lambda;
				}
				ExprtmplParser.ValueContext value = context.value();
				if (value == null)
				{
					compiler.flow.Blocks.Add(Expression.Call(CallInclude, compiler.flow.Builder,compiler.flow.Parameter, lambda));
				}
				else
				{
					ParameterExpression stack = Expression.Variable(typeof(InnerStack));
					compiler.flow.Blocks.Add(Expression.Block(new ParameterExpression[] {stack},
															Expression.Assign(
																stack,
																Expression.Call(
																	GetPushStack, compiler.flow.CompileValue(value))),
															Expression.Call(
																CallInclude, compiler.flow.Builder, stack, lambda),
															Expression.Call(GetPopStack, stack)));
				}
				ITerminalNode newline = context.NEWLINE();
				if (newline != null)
					compiler.flow.AddOutput(newline.GetText());
			}

			public override void EnterEnd(ExprtmplParser.EndContext context)
			{
				compiler.flow = compiler.flow.End(file, offset, context);
			}

			public override void EnterForloop1(ExprtmplParser.Forloop1Context context)
			{
				compiler.flow = compiler.flow.ForLoop1(file, offset, context);
			}

			public override void EnterForloop2(ExprtmplParser.Forloop2Context context)
			{
				compiler.flow = compiler.flow.ForLoop2(file, offset, context);
			}

			public override void EnterForrange(ExprtmplParser.ForrangeContext context)
			{
				compiler.flow = compiler.flow.ForRange(file, offset, context);
			}

			public override void EnterIf(ExprtmplParser.IfContext context)
			{
				compiler.flow = compiler.flow.If(file, offset, context);
			}

			public override void EnterElseif(ExprtmplParser.ElseifContext context)
			{
				compiler.flow = compiler.flow.ElseIf(file, offset, context);
			}

			public override void EnterElse(ExprtmplParser.ElseContext context)
			{
				compiler.flow = compiler.flow.Else(file, offset, context);
			}
		}

		private class ParseException : Exception
		{
			public readonly Parser recognizer;
			public readonly RecognitionException exception;

			public ParseException(Parser recognizer, RecognitionException exception)
			{
				this.recognizer = recognizer;
				this.exception = exception;
			}
		}

		private class ErrorStrategy : DefaultErrorStrategy
		{
			private readonly int offset;

			public ErrorStrategy(int offset)
			{
				this.offset = offset;
			}

			public override void Recover(Parser recognizer, RecognitionException e)
			{
				throw new ParseException(recognizer, e);
			}

			public override void ReportError(Parser recognizer, RecognitionException e)
			{
				Console.WriteLine(recognizer.CurrentToken.Text);
				foreach (var token in e.GetExpectedTokens().ToList())
				{
					string name = e.Recognizer.Vocabulary.GetDisplayName(token);
					if (name == "DONE")
						name = "'<EOF>'";
					Console.WriteLine(name);
				}
				throw new ParseException(recognizer, e);
			}

			public override bool InErrorRecoveryMode(Parser recognizer)
			{
				return true;
			}

			public override IToken RecoverInline(Parser recognizer)
			{
				Console.WriteLine(recognizer.CurrentToken.Text);
				foreach (var token in recognizer.GetExpectedTokens().ToList())
				{
					string name = recognizer.Vocabulary.GetDisplayName(token);
					if (name == "DONE")
						name = "'<EOF>'";
					Console.WriteLine(name);
				}
				if (recognizer.GetExpectedTokens().Contains(recognizer.CurrentToken.Type))
					return recognizer.CurrentToken;
				throw new ParseException(recognizer, new InputMismatchException(recognizer));
			}
		}

		static Compiler()
		{
			GetOutput = typeof(Compiler).GetMethod(
				"Output", BindingFlags.Public |
								BindingFlags.NonPublic | BindingFlags.Static);
			GetConditionTest = typeof(Compiler).GetMethod(
				"ConditionTest", BindingFlags.Public |
						BindingFlags.NonPublic | BindingFlags.Static);
			GetConcat = typeof(Compiler).GetMethod(
				"Concat", BindingFlags.Public |
						BindingFlags.NonPublic | BindingFlags.Static);
			GetAdd = typeof(Compiler).GetMethod(
				"Add", BindingFlags.Public |
						BindingFlags.NonPublic | BindingFlags.Static);
			GetSubtract = typeof(Compiler).GetMethod(
				"Subtract", BindingFlags.Public |
							BindingFlags.NonPublic | BindingFlags.Static);
			GetMultiply = typeof(Compiler).GetMethod(
				"Multiply", BindingFlags.Public |
							BindingFlags.NonPublic | BindingFlags.Static);
			GetDivide = typeof(Compiler).GetMethod(
				"Divide", BindingFlags.Public |
						BindingFlags.NonPublic | BindingFlags.Static);
			GetModulo = typeof(Compiler).GetMethod(
				"Modulo", BindingFlags.Public |
						BindingFlags.NonPublic | BindingFlags.Static);
			GetPower = typeof(Compiler).GetMethod(
				"Power", BindingFlags.Public |
						BindingFlags.NonPublic | BindingFlags.Static);
			GetEqual = typeof(Compiler).GetMethod(
				"Equal", BindingFlags.Public |
						BindingFlags.NonPublic | BindingFlags.Static);
			GetNotEqual = typeof(Compiler).GetMethod(
				"NotEqual", BindingFlags.Public |
							BindingFlags.NonPublic | BindingFlags.Static);
			GetGreaterThan = typeof(Compiler).GetMethod(
				"GreaterThan", BindingFlags.Public |
								BindingFlags.NonPublic | BindingFlags.Static);
			GetGreaterThanOrEqual = typeof(Compiler).GetMethod(
				"GreaterThanOrEqual", BindingFlags.Public |
									BindingFlags.NonPublic | BindingFlags.Static);
			GetLessThan = typeof(Compiler).GetMethod(
				"LessThan", BindingFlags.Public |
							BindingFlags.NonPublic | BindingFlags.Static);
			GetLessThanOrEqual = typeof(Compiler).GetMethod(
				"LessThanOrEqual", BindingFlags.Public |
									BindingFlags.NonPublic | BindingFlags.Static);
			GetNot = typeof(Compiler).GetMethod(
				"Not", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
			GetNegate = typeof(Compiler).GetMethod(
				"Negate", BindingFlags.Public |
						BindingFlags.NonPublic | BindingFlags.Static);
			GetSubstring1 = typeof(Compiler).GetMethod(
				"Substring1", BindingFlags.Public |
							BindingFlags.NonPublic | BindingFlags.Static);
			GetSubstring2 = typeof(Compiler).GetMethod(
				"Substring2", BindingFlags.Public |
							BindingFlags.NonPublic | BindingFlags.Static);
			GetIndex1 = typeof(Compiler).GetMethod(
				"Index1", BindingFlags.Public |
						BindingFlags.NonPublic | BindingFlags.Static);
			GetIndex2 = typeof(Compiler).GetMethod(
				"Index2", BindingFlags.Public |
						BindingFlags.NonPublic | BindingFlags.Static);
			GetMember = typeof(Compiler).GetMethod(
				"Member", BindingFlags.Public |
						BindingFlags.NonPublic | BindingFlags.Static);

			CreateNewTable = typeof(Compiler).GetMethod(
				"NewTable", BindingFlags.Public |
						BindingFlags.NonPublic | BindingFlags.Static);
			CreateNewArray = typeof(Compiler).GetMethod(
				"NewArray", BindingFlags.Public |
							BindingFlags.NonPublic | BindingFlags.Static);

			CallForLoop1 = typeof(Compiler).GetMethod(
				"ForLoop1", BindingFlags.Public |
						BindingFlags.NonPublic | BindingFlags.Static);
			CallForLoop2 = typeof(Compiler).GetMethod(
				"ForLoop2", BindingFlags.Public |
						BindingFlags.NonPublic | BindingFlags.Static);
			CallForRange1 = typeof(Compiler).GetMethod(
				"ForRange1", BindingFlags.Public |
						BindingFlags.NonPublic | BindingFlags.Static);
			CallForRange2 = typeof(Compiler).GetMethod(
				"ForRange2", BindingFlags.Public |
							BindingFlags.NonPublic | BindingFlags.Static);
			CallMethodInvoke = typeof(Compiler).GetMethod(
				"MethodInvoke", BindingFlags.Public |
							BindingFlags.NonPublic | BindingFlags.Static);
			CallInclude = typeof(Compiler).GetMethod(
				"Include", BindingFlags.Public |
							BindingFlags.NonPublic | BindingFlags.Static);

			GetPushStack = typeof(Compiler).GetMethod(
				"PushStack", BindingFlags.Public |
							BindingFlags.NonPublic | BindingFlags.Static);
			GetPopStack = typeof(Compiler).GetMethod(
				"PopStack", BindingFlags.Public |
							BindingFlags.NonPublic | BindingFlags.Static);
			GetClearStack = typeof(Compiler).GetMethod(
				"ClearStack", BindingFlags.Public |
							BindingFlags.NonPublic | BindingFlags.Static);
		}

		private static readonly MethodInfo GetOutput;
		private static readonly MethodInfo GetConditionTest;
		private static readonly MethodInfo GetConcat;
		private static readonly MethodInfo GetAdd;
		private static readonly MethodInfo GetSubtract;
		private static readonly MethodInfo GetMultiply;
		private static readonly MethodInfo GetDivide;
		private static readonly MethodInfo GetModulo;
		private static readonly MethodInfo GetPower;
		private static readonly MethodInfo GetEqual;
		private static readonly MethodInfo GetNotEqual;
		private static readonly MethodInfo GetGreaterThan;
		private static readonly MethodInfo GetGreaterThanOrEqual;
		private static readonly MethodInfo GetLessThan;
		private static readonly MethodInfo GetLessThanOrEqual;
		private static readonly MethodInfo GetNot;
		private static readonly MethodInfo GetNegate;
		private static readonly MethodInfo GetSubstring1;
		private static readonly MethodInfo GetSubstring2;
		private static readonly MethodInfo GetIndex1;
		private static readonly MethodInfo GetIndex2;
		private static readonly MethodInfo GetMember;
		private static readonly MethodInfo CreateNewTable;
		private static readonly MethodInfo CreateNewArray;
		private static readonly MethodInfo CallForLoop1;
		private static readonly MethodInfo CallForLoop2;
		private static readonly MethodInfo CallForRange1;
		private static readonly MethodInfo CallForRange2;
		private static readonly MethodInfo CallMethodInvoke;
		private static readonly MethodInfo CallInclude;
		private static readonly MethodInfo GetPushStack;
		private static readonly MethodInfo GetPopStack;
		private static readonly MethodInfo GetClearStack;

		private static void Output(StringBuilder builder, Value text)
		{
			builder.Append(text.ToString());
		}

		private static bool ConditionTest(Value value)
		{
			return (bool)value;
		}

		private static Value Concat(Value left, Value right)
		{
			if (left == null)
				throw new ArgumentNullException("left");
			if (right == null)
				throw new ArgumentNullException("right");
			return (string)left + (string)right;
		}

		private static Value Add(Value left, Value right)
		{
			if (left == null)
				throw new ArgumentNullException("left");
			if (right == null)
				throw new ArgumentNullException("right");
			return (double)left + (double)right;
		}

		private static Value Subtract(Value left, Value right)
		{
			if (left == null)
				throw new ArgumentNullException("left");
			if (right == null)
				throw new ArgumentNullException("right");
			return (double)left - (double)right;
		}

		private static Value Multiply(Value left, Value right)
		{
			if (left == null)
				throw new ArgumentNullException("left");
			if (right == null)
				throw new ArgumentNullException("right");
			return (double)left * (double)right;
		}

		private static Value Divide(Value left, Value right)
		{
			if (left == null)
				throw new ArgumentNullException("left");
			if (right == null)
				throw new ArgumentNullException("right");
			return (double)left / (double)right;
		}

		private static Value Modulo(Value left, Value right)
		{
			if (left == null)
				throw new ArgumentNullException("left");
			if (right == null)
				throw new ArgumentNullException("right");
			return (double)left % (double)right;
		}

		private static Value Power(Value left, Value right)
		{
			if (left == null)
				throw new ArgumentNullException("left");
			if (right == null)
				throw new ArgumentNullException("right");
			return Math.Pow((double)left, (double)right);
		}

		private static Value Equal(Value left, Value right)
		{
			if (left == null)
				return right == null;
			if (right == null)
				return false;
			if (left.Type != right.Type)
				return false;
			switch (left.Type)
			{
			case ValueType.Boolean:
				return (bool)left == (bool)right;
			case ValueType.Number:
				return Math.Abs((double)left - (double)right) < double.Epsilon;
			case ValueType.String:
				return (string)left == (string)right;
			case ValueType.Table:
				return (Table)left == (Table)right;
			case ValueType.Array:
				return (Array)left == (Array)right;
			default:
				throw new InvalidOperationException();
			}
		}

		private static Value NotEqual(Value left, Value right)
		{
			if (left == null)
				return right != null;
			if (right == null)
				return true;
			if (left.Type != right.Type)
				return true;
			switch (left.Type)
			{
			case ValueType.Boolean:
				return (bool)left != (bool)right;
			case ValueType.Number:
				return Math.Abs((double)left - (double)right) >= double.Epsilon;
			case ValueType.String:
				return (string)left != (string)right;
			case ValueType.Table:
				return (Table)left != (Table)right;
			case ValueType.Array:
				return (Array)left != (Array)right;
			default:
				throw new InvalidOperationException();
			}
		}

		private static Value GreaterThan(Value left, Value right)
		{
			if (left == null)
				throw new ArgumentNullException("left");
			if (right == null)
				throw new ArgumentNullException("right");
			return (double)left > (double)right;
		}

		private static Value GreaterThanOrEqual(Value left, Value right)
		{
			if (left == null)
				throw new ArgumentNullException("left");
			if (right == null)
				throw new ArgumentNullException("right");
			return (double)left >= (double)right;
		}

		private static Value LessThan(Value left, Value right)
		{
			if (left == null)
				throw new ArgumentNullException("left");
			if (right == null)
				throw new ArgumentNullException("right");
			return (double)left < (double)right;
		}

		private static Value LessThanOrEqual(Value left, Value right)
		{
			if (left == null)
				throw new ArgumentNullException("left");
			if (right == null)
				throw new ArgumentNullException("right");
			return (double)left <= (double)right;
		}

		private static Value Not(Value value)
		{
			if (value == null)
				throw new ArgumentNullException("value");
			return !(bool)value;
		}

		private static Value Negate(Value value)
		{
			if (value == null)
				throw new ArgumentNullException("value");
			return -(double)value;
		}

		private static Value Substring1(Value value, Value index)
		{
			if (value == null)
				throw new ArgumentNullException("value");
			if (index == null)
				throw new ArgumentNullException("index");
			string str = (string)value;
			int where = (int)index;
			if (where < 0)
				where = str.Length + where + 1;
			return str.Substring(where, 1);
		}

		private static Value Substring2(Value value, Value from, Value to)
		{
			if (value == null)
				throw new ArgumentNullException("value");
			if (from == null)
				throw new ArgumentNullException("from");
			if (to == null)
				throw new ArgumentNullException("to");
			string str = (string)value;
			int fromIndex = (int)from;
			int toIndex = (int)to;
			if (fromIndex < 0)
				fromIndex = str.Length + fromIndex + 1;
			if (toIndex < 0)
				toIndex = str.Length + toIndex;
			return str.Substring(fromIndex, toIndex - fromIndex + 1);
		}

		private static Value Index1(Value value, Value index)
		{
			if (value == null)
				throw new ArgumentNullException("value");
			if (index == null)
				throw new ArgumentNullException("index");
			switch (value.Type)
			{
			case ValueType.Table:
				return ((Table)value)[(string)index];
			case ValueType.Array:
				return ((Array)value)[(int)index];
			case ValueType.String:
				return Substring1(value, index);
			}
			throw new InvalidOperationException();
		}

		private static Value Index2(Value value, Value from, Value to)
		{
			if (value == null)
				throw new ArgumentNullException("value");
			if (from == null)
				throw new ArgumentNullException("from");
			if (to == null)
				throw new ArgumentNullException("to");
			switch (value.Type)
			{
			case ValueType.Array:
				{
					Array array = (Array)value;
					int fromIndex = (int)from;
					int toIndex = (int)to;
					if (fromIndex < 0)
						fromIndex = array.Count + fromIndex + 1;
					if (toIndex < 0)
						toIndex = array.Count + toIndex;
					Value[] values = new Value[Math.Max(0, toIndex - fromIndex + 1)];
					for (int i = 0; i <= toIndex - fromIndex; i++)
						values[i] = array[i + fromIndex];
					return NewArray(values);
				}
			case ValueType.String:
				return Substring2(value, from, to);
			}
			throw new InvalidOperationException();
		}

		private static Value Member(Value value, string name)
		{
			Contract.Requires(name != null);
			if (value == null)
				throw new ArgumentNullException("value");
			switch (value.Type)
			{
			case ValueType.String:
				if (name == "length")
					return ((string)value).Length;
				break;
			case ValueType.Table:
				return ((Table)value)[name];
			case ValueType.Array:
				if (name == "length")
					return ((Array)value).Count;
				break;
			}
			throw new InvalidOperationException();
		}

		private static Value NewTable(string[] names, Value[] values)
		{
			if (names == null)
				throw new ArgumentNullException("names");
			if (values == null)
				throw new ArgumentNullException("values");
			Dictionary<string, Value> dict = new Dictionary<string, Value>(names.Length);
			for (int i = 0; i < names.Length; i++)
				dict[names[i]] = values[i];
			return Table.From(dict);
		}

		private static Value NewArray(Value[] values)
		{
			if (values == null)
				throw new ArgumentNullException("values");
			return Array.From(values);
		}

		private static void ForLoop1(
			StringBuilder builder, InnerStack stack, Value value, string name,
			Action<StringBuilder, ControlStack> action)
		{
			Contract.Requires(action != null);
			if (value == null)
				throw new ArgumentNullException("value");
			switch (value.Type)
			{
			case ValueType.Table:
				foreach (string key in ((Table)value).Keys())
				{
					stack.Values[name] = key;
					action(builder, stack);
				}
				break;
			case ValueType.Array:
				foreach (Value entry in (Array)value)
				{
					stack.Values[name] = entry;
					action(builder, stack);
				}
				break;
			default:
				throw new InvalidOperationException();
			}
		}

		private static void ForLoop2(
			StringBuilder builder, InnerStack stack, Value value, string name1, string name2,
			Action<StringBuilder, ControlStack> action)
		{
			Contract.Requires(action != null);
			if (value == null)
				throw new ArgumentNullException("value");
			switch (value.Type)
			{
			case ValueType.Table:
				foreach (var kv in (Table)value)
				{
					stack.Values[name1] = kv.Key;
					stack.Values[name2] = kv.Value;
					action(builder, stack);
				}
				break;
			case ValueType.Array:
				{
					Array array = (Array)value;
					for (int i = 0, j = array.Count; i < j; i++)
					{
						stack.Values[name1] = i;
						stack.Values[name2] = array[i];
						action(builder, stack);
					}
				}
				break;
			default:
				throw new InvalidOperationException();
			}
		}

		private static void ForRange1(
			StringBuilder builder, InnerStack stack, Value from, Value to, string name,
			Action<StringBuilder, ControlStack> action)
		{
			Contract.Requires(action != null);
			if (from == null)
				throw new ArgumentNullException("from");
			if (to == null)
				throw new ArgumentNullException("to");
			double formNumber = (double)from;
			double toNumber = (double)to;
			double stepNumber = formNumber > toNumber ? -1 : 1;
			if (Math.Abs(stepNumber) < double.Epsilon)
				throw new DivideByZeroException();
			if (stepNumber > 0)
			{
				double value = formNumber;
				while (value - toNumber < double.Epsilon)
				{
					stack.Values[name] = value;
					action(builder, stack);
					value += stepNumber;
				}
			}
			else
			{
				double value = formNumber;
				while (value - toNumber > -double.Epsilon)
				{
					stack.Values[name] = value;
					action(builder, stack);
					value += stepNumber;
				}
			}
		}

		private static void ForRange2(
			StringBuilder builder, InnerStack stack, Value from, Value to, Value step, string name,
			Action<StringBuilder, ControlStack> action)
		{
			Contract.Requires(action != null);
			if (from == null)
				throw new ArgumentNullException("from");
			if (to == null)
				throw new ArgumentNullException("to");
			if (step == null)
				throw new ArgumentNullException("step");
			double formNumber = (double)from;
			double toNumber = (double)to;
			double stepNumber = (double)step;
			if (Math.Abs(stepNumber) < double.Epsilon)
				throw new DivideByZeroException();
			if (stepNumber > 0)
			{
				double value = formNumber;
				while (value - toNumber < double.Epsilon)
				{
					stack.Values[name] = value;
					action(builder, stack);
					value += stepNumber;
				}
			}
			else
			{
				double value = formNumber;
				while (value - toNumber > -double.Epsilon)
				{
					stack.Values[name] = value;
					action(builder, stack);
					value += stepNumber;
				}
			}
		}

		private static Value MethodInvoke(Func<Value[], Value> method, Value[] values)
		{
			return method(values);
		}

		private static void Include(
			StringBuilder builder, ControlStack stack, Action<StringBuilder, ControlStack> action)
		{
			Contract.Requires(action != null);
			action(builder, stack);
		}

		private static InnerStack PushStack(Value value)
		{
			var stacks = localstacks.Value;
			InnerStack stack;
			if (stacks.Count != 0)
			{
				stack = stacks.Pop();
			}
			else
			{
				if (!freestacks.TryDequeue(out stack))
					stack = new InnerStack();
			}
			stack.SetPrevious(value);
			return stack;
		}

		private static void PopStack(InnerStack stack)
		{
			stack.Clear();
			localstacks.Value.Push(stack);
		}

		private static void ClearStack()
		{
			var stacks = localstacks.Value;
			foreach (InnerStack stack in stacks)
				freestacks.Enqueue(stack);
			stacks.Clear();
		}

		private abstract class ControlFlow
		{
			protected readonly Compiler Compiler;
			protected readonly ControlFlow Previous;
			public abstract ParameterExpression Builder { get; }
			public abstract ParameterExpression Parameter { get; }
			public abstract List<Expression> Blocks { get; }

			protected ControlFlow(Compiler compiler, ControlFlow previous)
			{
				Compiler = compiler;
				Previous = previous;
			}

			public void AddOutput(string str)
			{
				AddOutput(Expression.Constant((Value)str, typeof(Value)));
			}

			public void AddOutput(Expression expr)
			{
				Blocks.Add(Expression.Call(GetOutput, Builder, expr));
			}

			public virtual Expression<Action<StringBuilder, ControlStack>> EOF()
			{
				throw new NotImplementedException();
			}

			public virtual ControlFlow End(string file, int offset, ExprtmplParser.EndContext context)
			{
				throw new NotImplementedException();
			}

			public virtual ControlFlow If(string file, int offset, ExprtmplParser.IfContext context)
			{
				return new IfControlFlow(Compiler, this, context);
			}

			public virtual ControlFlow ElseIf(string file, int offset, ExprtmplParser.ElseifContext context)
			{
				throw new NotImplementedException();
			}

			public virtual ControlFlow Else(string file, int offset, ExprtmplParser.ElseContext context)
			{
				throw new NotImplementedException();
			}

			public virtual ControlFlow ForLoop1(string file, int offset, ExprtmplParser.Forloop1Context context)
			{
				return new ForLoop1ControlFlow(Compiler, this, context);
			}

			public virtual ControlFlow ForLoop2(string file, int offset, ExprtmplParser.Forloop2Context context)
			{
				return new ForLoop2ControlFlow(Compiler, this, context);
			}

			public virtual ControlFlow ForRange(string file, int offset, ExprtmplParser.ForrangeContext context)
			{
				return new ForRangeControlFlow(Compiler, this, context);
			}

			#region 解析表达式
			public Expression CompileValue(ExprtmplParser.ValueContext context)
			{
				ExprtmplParser.MemberContext member = context.member();
				if (member != null)
					return CompileValue(member);
				ExprtmplParser.ConcatContext concat = context.concat();
				if (concat != null)
					return CompileValue(concat);
				ExprtmplParser.OrContext or = context.or();
				if (or != null)
					return CompileValue(or);
				ExprtmplParser.NumericContext numeric = context.numeric();
				if (numeric != null)
					return CompileValue(numeric);
				ExprtmplParser.ArrayContext array = context.array();
				if (array != null)
				{
					return Expression.Call(CreateNewArray,
											Expression.NewArrayInit(typeof(Value), array.value().Select(CompileValue)));
				}
				ExprtmplParser.TableContext table = context.table();
				if (table != null)
				{
					return Expression.Call(CreateNewTable,
											Expression.NewArrayInit(typeof(string),
																	table
																	.NAME()
																	.Select(name => Expression.Constant(
																				name.GetText(), typeof(string)))),
											Expression.NewArrayInit(typeof(Value), table.value().Select(CompileValue)));
				}
				ExprtmplParser.CallContext call = context.call();
				if (call != null)
				{
					string name = string.Join(".", call.NAME().Select(item => item.GetText()));
					Func<Value[], Value> method;
					for (int i = 0; i < Compiler.methods.Length; i++)
					{
						if (Compiler.methods[i].TryGetValue(name, out method))
						{
							return Expression.Call(CallMethodInvoke, Expression.Constant(method),
													Expression.NewArrayInit(
														typeof(Value), call.value().Select(CompileValue)));
						}
					}
					if (Builtin.Methods.TryGetValue(name, out method))
					{
						return Expression.Call(CallMethodInvoke, Expression.Constant(method),
												Expression.NewArrayInit(
													typeof(Value), call.value().Select(CompileValue)));
					}
					throw new MissingMethodException("Compiler", name);
				}
				return Expression.Constant(null, typeof(Value));
			}

			public Expression CompileValue(ExprtmplParser.MemberContext context)
			{
				int index = 0;
				Expression parent = Expression.Call(GetMember, Parameter,
													Expression.Constant(context.NAME().GetText(), typeof(string)));
				ExprtmplParser.SuffixContext[] suffixs = context.suffix();
				while (true)
				{
					if (index == suffixs.Length)
						return parent;
					parent = CompileValue(suffixs[index], parent);
					index = index + 1;
				}
			}

			public Expression CompileValue(ExprtmplParser.SuffixContext context, Expression parent)
			{
				ITerminalNode name = context.NAME();
				if (name != null)
					return Expression.Call(GetMember, parent, Expression.Constant(name.GetText(), typeof(string)));
				ExprtmplParser.IndexContext index = context.index();
				if (index != null)
					return Expression.Call(GetIndex1, parent, CompileValue(index));
				return Expression.Call(GetIndex2, parent, CompileValue(context.subindex(0)),
										CompileValue(context.subindex(1)));
			}

			public Expression CompileValue(ExprtmplParser.IndexContext context)
			{
				ExprtmplParser.ConcatContext concat = context.concat();
				if (concat != null)
					return CompileValue(concat);
				return CompileValue(context.numeric());
			}

			public Expression CompileValue(ExprtmplParser.SubindexContext context)
			{
				return CompileValue(context.numeric());
			}

			public Expression CompileValue(ExprtmplParser.ConcatContext context)
			{
				ExprtmplParser.StringContext[] strings = context.@string();
				Expression expr = CompileValue(strings[0]);
				for (int i = 1; i < strings.Length; ++i)
					expr = Expression.Call(GetConcat, expr, CompileValue(strings[i]));
				return expr;
			}

			public Expression CompileValue(ExprtmplParser.StringContext context)
			{
				Expression expr;
				ITerminalNode node = context.STRING();
				if (node != null)
				{
					expr = Expression.Constant((Value)Escape(node.GetText()), typeof(Value));
				}
				else
				{
					ExprtmplParser.MemberContext member = context.member();
					expr = member != null ? CompileValue(member) : CompileValue(context.concat());
				}
				ExprtmplParser.SubstringContext[] substring = context.substring();
				if (substring != null && substring.Length != 0)
				{
					expr = substring.Length == 1
						? Expression.Call(GetSubstring1, expr, CompileValue(substring[0]))
						: Expression.Call(GetSubstring2, expr, CompileValue(substring[0]), CompileValue(substring[1]));
				}
				return expr;
			}

			public Expression CompileValue(ExprtmplParser.SubstringContext context)
			{
				return CompileValue(context.numeric());
			}

			public Expression CompileValue(ExprtmplParser.OrContext context)
			{
				ExprtmplParser.AndContext[] ands = context.and();
				Expression expr = CompileValue(ands[0]);
				for (int i = 1; i < ands.Length; ++i)
				{
					expr = Expression.OrElse(Expression.Call(GetConditionTest, expr),
											Expression.Call(GetConditionTest, CompileValue(ands[i])));
				}
				return Expression.Convert(expr, typeof(Value));
			}

			public Expression CompileValue(ExprtmplParser.AndContext context)
			{
				ExprtmplParser.BooleanContext[] booleans = context.boolean();
				Expression expr = CompileValue(booleans[0]);
				for (int i = 1; i < booleans.Length; ++i)
				{
					expr = Expression.AndAlso(Expression.Call(GetConditionTest, expr),
											Expression.Call(GetConditionTest, CompileValue(booleans[i])));
				}
				return Expression.Convert(expr, typeof(Value));
			}

			public Expression CompileValue(ExprtmplParser.BooleanContext context)
			{
				bool not = context.NOT() != null;
				if (context.K_TRUE() != null)
					return Expression.Constant((Value)!not, typeof(Value));
				if (context.K_FALSE() != null)
					return Expression.Constant((Value)not, typeof(Value));
				Expression expr;
				ExprtmplParser.MemberContext member = context.member();
				if (member != null)
				{
					if (context.K_NULL() == null)
						return not ? Expression.Call(GetNot, CompileValue(member)) : CompileValue(member);
					expr = Expression.Call(context.EQ().GetText() == "==" ? GetEqual : GetNotEqual,
											CompileValue(member), Expression.Constant(null, typeof(Value)));
					return not ? Expression.Call(GetNot, expr) : expr;
				}
				ExprtmplParser.NumericContext[] numbers = context.numeric();
				if (numbers != null && numbers.Length != 0)
				{
					ITerminalNode op = context.EQ() ?? context.CMP();
					MethodInfo method;
					switch (op.GetText())
					{
					case "<":
						method = GetLessThan;
						break;
					case ">":
						method = GetGreaterThan;
						break;
					case "<=":
						method = GetLessThanOrEqual;
						break;
					case ">=":
						method = GetGreaterThanOrEqual;
						break;
					case "==":
						method = GetEqual;
						break;
					default:
						method = GetNotEqual;
						break;
					}
					expr = Expression.Call(method, CompileValue(numbers[0]), CompileValue(numbers[1]));
				}
				else
				{
					ExprtmplParser.ConcatContext[] concat = context.concat();
					if (concat != null && concat.Length != 0)
					{
						expr = Expression.Call(context.EQ().GetText() == "==" ? GetEqual : GetNotEqual,
												CompileValue(concat[0]), CompileValue(concat[1]));
					}
					else
					{
						expr = CompileValue(context.or());
					}
				}
				return not ? Expression.Call(GetNot, expr) : expr;
			}

			public Expression CompileValue(ExprtmplParser.NumericContext context)
			{
				ExprtmplParser.MulexpContext[] muls = context.mulexp();
				Expression expr = CompileValue(muls[0]);
				for (int i = 1; i < muls.Length; ++i)
				{
					ITerminalNode add = context.ADD(i - 1);
					expr = Expression.Call(add.GetText() == "+" ? GetAdd : GetSubtract, expr, CompileValue(muls[i]));
				}
				return expr;
			}

			public Expression CompileValue(ExprtmplParser.MulexpContext context)
			{
				ExprtmplParser.PowexpContext[] powers = context.powexp();
				Expression expr = CompileValue(powers[0]);
				for (int i = 1; i < powers.Length; ++i)
				{
					MethodInfo method;
					switch (context.MUL(i - 1).GetText())
					{
					case "*":
						method = GetMultiply;
						break;
					case "/":
						method = GetDivide;
						break;
					default:
						method = GetModulo;
						break;
					}
					expr = Expression.Call(method, expr, CompileValue(powers[i]));
				}
				return expr;
			}

			public Expression CompileValue(ExprtmplParser.PowexpContext context)
			{
				ExprtmplParser.AtomContext[] atoms = context.atom();
				Expression expr = CompileValue(atoms[atoms.Length - 1]);
				for (int i = atoms.Length - 2; i >= 0; --i)
					expr = Expression.Call(GetPower, CompileValue(atoms[i]), expr);
				return expr;
			}

			public Expression CompileValue(ExprtmplParser.AtomContext context)
			{
				Expression expr;
				ITerminalNode node;
				node = context.HEX();
				if (node != null)
				{
					expr = Expression.Constant((Value)Convert.ToInt32(node.GetText().Substring(2), 16),
												typeof(Value));
				}
				else
				{
					node = context.NUMBER();
					if (node != null)
					{
						expr = Expression.Constant((Value)Convert.ToDouble(node.GetText()), typeof(Value));
					}
					else
					{
						ExprtmplParser.MemberContext member = context.member();
						expr = member != null ? CompileValue(member) : CompileValue(context.numeric());
					}
				}
				return context.NEGATE() == null ? expr : Expression.Call(GetNegate, expr);
			}

			private static string Escape(string text)
			{
				StringBuilder builder = new StringBuilder(text.Length);
				for (int i = 1; i < text.Length - 1; ++i)
				{
					char c = text[i];
					if (c == '\\')
					{
						switch (text[++i])
						{
						case 'f':
							builder.Append('\f');
							break;
						case 'n':
							builder.Append('\n');
							break;
						case 'r':
							builder.Append('\r');
							break;
						case 't':
							builder.Append('\t');
							break;
						case '"':
							builder.Append('"');
							break;
						case '\'':
							builder.Append('"');
							break;
						case '\\':
							builder.Append('\\');
							break;
						case 'u':
							{
								int unicode = 0;
								for (int j = 1; j <= 4; ++j)
								{
									byte b = (byte)text[i + j];
									if (b >= 'a')
										b -= (byte)'a' - 10;
									else if (c >= 'A')
										b -= (byte)'A' - 10;
									else
										b -= (byte)'0';
									unicode <<= 4;
									unicode |= b;
								}
								i += 4;
								builder.Append((char)unicode);
							}
							break;
						}
					}
					else
					{
						builder.Append(c);
					}
				}
				return builder.ToString();
			}
			#endregion
		}

		private class BaseControlFlow : ControlFlow
		{
			private readonly ParameterExpression builder = Expression.Variable(typeof(StringBuilder));
			private readonly ParameterExpression parameter = Expression.Parameter(typeof(ControlStack));
			private readonly List<Expression> blocks = new List<Expression>();

			public override ParameterExpression Builder
			{
				get { return builder; }
			}

			public override ParameterExpression Parameter
			{
				get { return parameter; }
			}

			public override List<Expression> Blocks
			{
				get { return blocks; }
			}

			public BaseControlFlow(Compiler compiler) : base(compiler, null) { }

			public override Expression<Action<StringBuilder, ControlStack>> EOF()
			{
				Blocks.Add(Expression.Call(GetClearStack));
				return Expression.Lambda<Action<StringBuilder, ControlStack>>(
					Expression.Block(Blocks), true, builder, parameter);
			}
		}

		private class IfControlFlow : ControlFlow
		{
			private readonly List<Expression> thenblocks = new List<Expression>();
			private readonly List<Expression> elseblocks = new List<Expression>();
			private readonly bool autoend;

			private bool then;
			private readonly Expression condition;

			public override ParameterExpression Builder
			{
				get { return Previous.Builder; }
			}

			public override ParameterExpression Parameter
			{
				get { return Previous.Parameter; }
			}

			public override List<Expression> Blocks
			{
				get { return then ? thenblocks : elseblocks; }
			}

			public IfControlFlow(Compiler compiler, ControlFlow previous, ExprtmplParser.IfContext context) : base(compiler, previous)
			{
				then = true;
				autoend = false;
				condition = CompileValue(context.or());
			}

			public IfControlFlow(Compiler compiler, ControlFlow previous, ExprtmplParser.ElseifContext context) : base(compiler, previous)
			{
				then = true;
				autoend = true;
				condition = CompileValue(context.or());
			}

			public override ControlFlow End(string file, int offset, ExprtmplParser.EndContext context)
			{
				Previous.Blocks.Add(then
										? Expression.IfThen(
											Expression.Call(GetConditionTest, condition),
											Expression.Block(thenblocks))
										: Expression.IfThenElse(
											Expression.Call(GetConditionTest, condition),
											Expression.Block(thenblocks), Expression.Block(elseblocks)));
				return autoend ? Previous.End(file, offset, context) : Previous;
			}

			public override ControlFlow ElseIf(string file, int offset, ExprtmplParser.ElseifContext context)
			{
				then = false;
				return new IfControlFlow(Compiler, this, context);
			}

			public override ControlFlow Else(string file, int offset, ExprtmplParser.ElseContext context)
			{
				then = false;
				return this;
			}
		}

		private abstract class ForControlFlow : ControlFlow
		{
			private readonly ParameterExpression builder = Expression.Variable(typeof(StringBuilder));
			private readonly ParameterExpression parameter = Expression.Parameter(typeof(ControlStack));
			private readonly List<Expression> blocks = new List<Expression>();

			public override ParameterExpression Builder
			{
				get { return builder; }
			}

			public override ParameterExpression Parameter
			{
				get { return parameter; }
			}

			public override List<Expression> Blocks
			{
				get { return blocks; }
			}

			protected ForControlFlow(Compiler compiler, ControlFlow previous) : base(compiler, previous) { }

			protected abstract Expression End(Expression stack);

			public override ControlFlow End(string file, int offset, ExprtmplParser.EndContext context)
			{
				ParameterExpression stack = Expression.Variable(typeof(InnerStack));
				Previous.Blocks.Add(Expression.Block(new ParameterExpression[] {stack},
													Expression.Assign(
														stack, Expression.Call(GetPushStack, Previous.Parameter)),
													End(stack), Expression.Call(GetPopStack, stack)));
				return Previous;
			}
		}

		private class ForLoop1ControlFlow : ForControlFlow
		{
			private readonly ExprtmplParser.Forloop1Context forcontext;

			public ForLoop1ControlFlow(Compiler compiler, ControlFlow previous, ExprtmplParser.Forloop1Context context) : base(compiler, previous)
			{
				forcontext = context;
			}

			protected override Expression End(Expression stack)
			{
				return Expression.Call(CallForLoop1, Previous.Builder, stack, Previous.CompileValue(forcontext.value()),
										Expression.Constant(forcontext.NAME().GetText(), typeof(string)),
										Expression.Lambda<Action<StringBuilder, ControlStack>>(
											Expression.Block(Blocks), true, Builder, Parameter));
			}
		}

		private class ForLoop2ControlFlow : ForControlFlow
		{
			private readonly ExprtmplParser.Forloop2Context forcontext;

			public ForLoop2ControlFlow(Compiler compiler, ControlFlow previous, ExprtmplParser.Forloop2Context context) : base(compiler, previous)
			{
				forcontext = context;
			}

			protected override Expression End(Expression stack)
			{
				return Expression.Call(CallForLoop2, Previous.Builder, stack, Previous.CompileValue(forcontext.value()),
										Expression.Constant(forcontext.NAME(0).GetText(), typeof(string)),
										Expression.Constant(forcontext.NAME(1).GetText(), typeof(string)),
										Expression.Lambda<Action<StringBuilder, ControlStack>>(
											Expression.Block(Blocks), true, Builder, Parameter));
			}
		}

		private class ForRangeControlFlow : ForControlFlow
		{
			private readonly ExprtmplParser.ForrangeContext forcontext;

			public ForRangeControlFlow(Compiler compiler, ControlFlow previous, ExprtmplParser.ForrangeContext context) : base(compiler, previous)
			{
				forcontext = context;
			}

			protected override Expression End(Expression stack)
			{
				ExprtmplParser.NumericContext[] values = forcontext.numeric();
				Expression from = Previous.CompileValue(values[0]);
				Expression to = Previous.CompileValue(values[1]);
				if (values.Length == 3)
				{
					return Expression.Call(CallForRange2, Previous.Builder, stack, from, to, Previous.CompileValue(values[2]),
											Expression.Constant(forcontext.NAME().GetText(), typeof(string)),
											Expression.Lambda<Action<StringBuilder, ControlStack>>(
												Expression.Block(Blocks), true, Builder, Parameter));
				}
				return Expression.Call(CallForRange1, Previous.Builder, stack, from, to,
										Expression.Constant(forcontext.NAME().GetText(), typeof(string)),
										Expression.Lambda<Action<StringBuilder, ControlStack>>(
											Expression.Block(Blocks), true, Builder, Parameter));
			}
		}

		private abstract class ControlStack : Value
		{
			public sealed override ValueType Type
			{
				get { return ValueType.Table; }
			}
		}

		private class BaseStack : ControlStack
		{
			public Table Table;

			protected override Table CastToTable()
			{
				return Table;
			}
		}

		private class InnerStack : ControlStack
		{
			public readonly Dictionary<string, Value> Values;
			private readonly StackTable table;
			private Table previous;

			public InnerStack()
			{
				Values = new Dictionary<string, Value>();
				table = new StackTable(this);
			}

			public void SetPrevious(Value value)
			{
				previous = value == null ? null : (Table)value;
			}

			public void Clear()
			{
				previous = null;
			}

			protected override Table CastToTable()
			{
				return table;
			}

			public class StackTable : Table
			{
				private readonly InnerStack stack;

				public StackTable(InnerStack stack)
				{
					this.stack = stack;
				}

				public override IEnumerable<string> Keys()
				{
					foreach (string key in stack.Values.Keys)
					{
						yield return key;
					}
					foreach (string key in stack.previous.Keys())
					{
						if (!stack.Values.ContainsKey(key))
							yield return key;
					}
				}

				public override Value this[string key]
				{
					get
					{
						Value value;
						return stack.Values.TryGetValue(key, out value) ? value : stack.previous[key];
					}
				}
			}
		}
	}
}
