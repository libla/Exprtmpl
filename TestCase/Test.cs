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
		#for row in rows[20:30]
			#if row.Print
			#import test with {title:row.Message..' import'}
			<li>ID::{row.ID}, Message::{row.Message}</li>
			#end
		#end
		#for key, value in rows[20]
			:{key} :{value}
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

	[PreTest]
	public static async Task Prepare()
	{
		compile = await files.Compile("start");
		List<Value> rows = new List<Value>();
		for (int i = 0; i < 100; i++)
		{
			rows.Add(Table.From(new Dictionary<string, Value> {
				{"ID", i},
				{"Message", string.Format("message {0}", i)},
				{"Print", (i & 1) == 0},
			}));
		}
		table = Table.From(new Dictionary<string, Value> {
			{"rows", Array.From(rows)},
			{"title", "libla"}
		});
	}

	[Test]
	public static void Test()
	{
		Console.WriteLine(compile(table));
	}
}