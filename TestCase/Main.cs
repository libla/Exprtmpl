using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;

[AttributeUsage(AttributeTargets.Method)]
public class TestAttribute : Attribute
{
	public string Name { get; private set; }

	public TestAttribute() : this("") { }

	public TestAttribute(string s)
	{
		Name = s;
	}
}

[AttributeUsage(AttributeTargets.Method)]
public class PreTestAttribute : Attribute
{
	public string Name { get; private set; }
	public int Priority;

	public PreTestAttribute() : this("") { }

	public PreTestAttribute(string s)
	{
		Name = s;
		Priority = 0;
	}
}

public static class TestHelper
{
	private static int WaitCount = 0;
	private static readonly ManualResetEvent WaitEvent = new ManualResetEvent(true);

	public sealed class Waiter : IDisposable
	{
		public void End()
		{
			((IDisposable)this).Dispose();
		}

		void IDisposable.Dispose()
		{
			EndWait();
			GC.SuppressFinalize(this);
		}

		~Waiter()
		{
			EndWait();
		}
	}

	public static Waiter StartWait()
	{
		if (Interlocked.Increment(ref WaitCount) == 1)
			WaitEvent.Reset();
		return new Waiter();
	}

	private static void EndWait()
	{
		if (Interlocked.Decrement(ref WaitCount) == 0)
			WaitEvent.Set();
	}

	public static void WaitAll()
	{
		WaitEvent.WaitOne();
	}
}

static class TestCase
{
	class Options
	{
		[ValueList(typeof(List<string>))]
		public IList<string> Filters { get; set; }

		[Option('n', "count", DefaultValue = 1, HelpText = "number of times to repeat.")]
		public int Count { get; set; }

		[Option('p', "performance", DefaultValue = null, HelpText = "display cost time.")]
		public bool? Performance { get; set; }

		[Option('f', "fatal", DefaultValue = false, HelpText = "stop when catch a exception.")]
		public bool StopOnError { get; set; }

		[HelpOption]
		public string GetUsage()
		{
			Assembly assembly = Assembly.GetExecutingAssembly();
			AssemblyProductAttribute product =
				(AssemblyProductAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyProductAttribute));
			HelpText help =
				new HelpText(string.Format("{0} v{1}", product.Product, assembly.GetName().Version)) {
					AddDashesToOption = true,
				};
			help.AddOptions(this);
			return help.ToString();
		}

		public bool Parse(string[] args)
		{
			Parser parser = new Parser(settings =>
			{
				settings.CaseSensitive = false;
				settings.IgnoreUnknownArguments = false;
				settings.HelpWriter = Console.Out;
			});
			return parser.ParseArguments(args, this);
		}
	}

	struct Testing
	{
		public string Name;
		public string Path;
		public Func<int, Task> Action;
		public Func<Task>[] Prepares;
	}

	abstract class Matcher
	{
		public abstract bool IsMatch(string str);

		public static Matcher Create(string s)
		{
			if (s.StartsWith("~"))
				return new RegexMatch(s.Substring(1));
			return new StrEqual(s);
		}

		private class StrEqual : Matcher
		{
			private readonly string templet;

			public StrEqual(string s)
			{
				templet = s;
			}

			public override bool IsMatch(string str)
			{
				return templet == str;
			}
		}

		private class RegexMatch : Matcher
		{
			private readonly Regex expr;

			public RegexMatch(string s)
			{
				expr = new Regex(string.Format("^{0}$", s),
								RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);
			}

			public override bool IsMatch(string str)
			{
				return expr.IsMatch(str);
			}
		}
	}

	static Testing[] FindTesting(IList<string> filters)
	{
		Regex expr;
		if (filters.Count == 0)
		{
			expr = new Regex("^.*$", RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);
		}
		else
		{
			string patterns = "^(" + string.Join(")|(", filters) + ")$";
			expr = new Regex(patterns, RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);
		}
		List<Testing> actions = new List<Testing>();
		Dictionary<string, List<Func<Task>>> prepares = new Dictionary<string, List<Func<Task>>>();
		Dictionary<Func<Task>, int> preparespri = new Dictionary<Func<Task>, int>();
		foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			foreach (Type type in assembly.GetTypes())
			{
				MethodInfo[] methods = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.InvokeMethod |
														BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
				for (int i = 0; i < methods.Length; ++i)
				{
					MethodInfo method = methods[i];
					object[] attrs = method.GetCustomAttributes(false);
					for (int j = 0; j < attrs.Length; ++j)
					{
						TestAttribute attr = attrs[j] as TestAttribute;
						if (attr != null)
						{
							if (expr.IsMatch(attr.Name))
							{
								Func<int, Task> action;
								try
								{
									action = Delegate.CreateDelegate(typeof(Func<int, Task>), method) as Func<int, Task>;
								}
								catch (Exception)
								{
									try
									{
										Func<Task> action_ = Delegate.CreateDelegate(typeof(Func<Task>), method) as Func<Task>;
										action = async m =>
										{
											for (int n = 0; n < m; ++n)
												await action_();
										};
									}
									catch (Exception)
									{
										try
										{
											Action<int> action_ = Delegate.CreateDelegate(typeof(Action<int>), method) as Action<int>;
#pragma warning disable 1998
											action = async m =>
#pragma warning restore 1998
											{
												action_(m);
											};
										}
										catch (Exception)
										{
											try
											{
												Action action_ = Delegate.CreateDelegate(typeof(Action), method) as Action;
#pragma warning disable 1998
												action = async m =>
#pragma warning restore 1998
												{
													for (int n = 0; n < m; ++n)
														action_();
												};
											}
											catch (Exception e)
											{
												throw new ApplicationException(string.Format("{0}.{1}:{2}", type.FullName, method.Name, e.Message));
											}
										}
									}
								}
								actions.Add(new Testing {
									Name = attr.Name,
									Path = string.Format("{0}.{1}", type.FullName, method.Name),
									Action = action,
								});
								break;
							}
						}
						PreTestAttribute attr_ = attrs[j] as PreTestAttribute;
						if (attr_ != null)
						{
							Func<Task> action;
							try
							{
								action = Delegate.CreateDelegate(typeof(Func<Task>), method) as Func<Task>;
							}
							catch (Exception)
							{
								try
								{
									Action action_ = Delegate.CreateDelegate(typeof(Action), method) as Action;
#pragma warning disable 1998
									action = async () => action_();
#pragma warning restore 1998
								}
								catch (Exception e)
								{
									throw new ApplicationException(string.Format("{0}.{1}:{2}", type.FullName, method.Name, e.Message));
								}
							}
							List<Func<Task>> actions_;
							if (!prepares.TryGetValue(attr_.Name, out actions_))
							{
								actions_ = new List<Func<Task>>();
								prepares.Add(attr_.Name, actions_);
							}
							actions_.Add(action);
							preparespri[action] = attr_.Priority;
							break;
						}
					}
				}
			}
		}
		List<Tuple<Matcher, Func<Task>[]>> prepares_ = new List<Tuple<Matcher, Func<Task>[]>>();
		Func<Task>[] empty_ = new Func<Task>[0];
		foreach (var prepare in prepares)
		{
			prepares_.Add(Tuple.Create(Matcher.Create(prepare.Key),
										prepare.Value == null ? empty_ : prepare.Value.ToArray()));
		}
		List<Func<Task>> list_ = new List<Func<Task>>();
		Comparison<Func<Task>> compare_ = (action1, action2) =>
		{
			return preparespri[action2] - preparespri[action1];
		};
		for (int i = 0; i < actions.Count; ++i)
		{
			Testing testing = actions[i];
			for (int j = 0; j < prepares_.Count; ++j)
			{
				var tuple = prepares_[j];
				if (tuple.Item1.IsMatch(testing.Name))
				{
					list_.AddRange(tuple.Item2);
				}
			}
			list_.Sort(compare_);
			testing.Prepares = list_.ToArray();
			list_.Clear();
			actions[i] = testing;
		}
		return actions.ToArray();
	}

	static async Task Run(Testing[] testings, Options options)
	{
		Stopwatch timer = new Stopwatch();
		for (int i = 0; i < testings.Length; ++i)
		{
			if (i != 0)
				Console.WriteLine("================================================================");
			Testing testing = testings[i];
			Console.WriteLine("--------TestCase {0} Enter", string.IsNullOrEmpty(testing.Name) ? testing.Path : testing.Name);
			try
			{
				for (int j = 0; j < testing.Prepares.Length; ++j)
				{
					Func<Task> action = testing.Prepares[j];
					await action();
				}
				Thread.Sleep(10);
				GC.Collect();
				timer.Restart();
				await testing.Action(options.Count);
				timer.Stop();
			}
			catch (Exception e)
			{
				Console.WriteLine("Catch Exception : {0}\n{1}", e.Message, e.StackTrace);
				if (options.StopOnError)
					return;
				continue;
			}
			if ((options.Performance.HasValue && options.Performance.Value) || (!options.Performance.HasValue && options.Count > 1))
			{
				Console.WriteLine("--------TestCase {0} Exit, Cost {1} ms", string.IsNullOrEmpty(testing.Name) ? testing.Path : testing.Name, timer.ElapsedMilliseconds);
			}
			else
			{
				Console.WriteLine("--------TestCase {0} Exit", string.IsNullOrEmpty(testing.Name) ? testing.Path : testing.Name);
			}
		}
	}

	static void Main(string[] args)
	{
		Options options = new Options();
		if (!options.Parse(args))
			return;
		Testing[] testings;
		try
		{
			testings = FindTesting(options.Filters);
		}
		catch (Exception e)
		{
			Console.WriteLine(e.Message);
			return;
		}
		GC.Collect();
		TestHelper.Waiter waiter = TestHelper.StartWait();
		Run(testings, options).GetAwaiter().OnCompleted(() => waiter.End());
		TestHelper.WaitAll();
		if (Debugger.IsAttached)
			Console.ReadKey(true);
	}
}