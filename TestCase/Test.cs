using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exprtmpl;
using Array = Exprtmpl.Array;

public static class ExprtmplTest
{
	private const string template = @"
<html>
	<head><title>test</title></head>
	<body>
		<ul>
		#for row in rows
			#if row.Print
			<li>ID::{row.ID}, Message::{row.Message}</li>
			#end
		#end
		</ul>
	</body>
</html>
";
	private const string include = @"
			<li>test :{title[1:3]}</li>
";
	private static readonly Dictionary<string, string> files = new Dictionary<string, string> {
		{"start", template},
		{"test", include},
	};
	private static Func<Table, string> compile;
	private static Table table;

	[PreTest("Exprtmpl")]
	public static async Task Prepare()
	{
		compile = await files.Compile("start");
		List<object> rows = new List<object>();
		for (int i = 0; i < 100; i++)
		{
			rows.Add(new {
				ID = i,
				Message = string.Format("message {0}", i),
				Print = i % 2 == 0,
			});
		}
		table = Table.From(new {rows = Array.From(rows), title = "libla"});
	}

	[Test("Exprtmpl")]
	public static void Test()
	{
		var result = compile(table);
		//Console.WriteLine(result);
	}
}

public static class DotLiquidTest
{
	private const string template = @"
<html>
	<head><title>test</title></head>
	<body>
		<ul>
		{% for row in rows -%}
			{% if row.Print -%}
			<li>ID:{{ row.ID }}, Message:{{ row.Message }}</li>
			{% endif -%}
		{% endfor -%}
		</ul>
	</body>
</html>
";
	private static DotLiquid.Template compile;
	private static DotLiquid.Hash hash;

	[PreTest("DotLiquid")]
	public static void Prepare()
	{
		compile = DotLiquid.Template.Parse(template);
		List<object> rows = new List<object>();
		for (int i = 0; i < 100; i++)
		{
			rows.Add(new Dictionary<string, object> {
				{"ID", i},
				{"Message", string.Format("message {0}", i)},
				{"Print", i % 2 == 0},
			});
		}
		hash = DotLiquid.Hash.FromDictionary(new Dictionary<string, object> {
			{"rows", rows}
		});
	}

	[Test("DotLiquid")]
	public static void Test()
	{
		var result = compile.Render(hash);
		//Console.WriteLine(result);
	}
}

public static class ScribanTest
{
	private const string template = @"
<html>
	<head><title>test</title></head>
	<body>
		<ul>
		{% for row in rows -%}
			{% if row.Print -%}
			<li>ID:{{ row.ID }}, Message:{{ row.Message }}</li>
			{% endif -%}
		{% endfor -%}
		</ul>
	</body>
</html>
";
	private static Scriban.Template compile;
	private static Dictionary<string, object> hash;

	[PreTest("Scriban")]
	public static void Prepare()
	{
		compile = Scriban.Template.ParseLiquid(template);
		List<object> rows = new List<object>();
		for (int i = 0; i < 100; i++)
		{
			rows.Add(new Dictionary<string, object> {
				{"ID", i},
				{"Message", string.Format("message {0}", i)},
				{"Print", i % 2 == 0},
			});
		}
		hash = new Dictionary<string, object> {
			{"rows", rows}
		};
	}

	[Test("Scriban")]
	public static void Test()
	{
		var result = compile.Render(hash);
		//Console.WriteLine(result);
	}
}