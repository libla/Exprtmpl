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
			#import test with {title:row.Message..' libla'}
			<li>ID::{row.ID}, Message::{row.Message}</li>
			#end
		#end
		</ul>
	</body>
</html>
";
	private const string include = @"
			test :{title}
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
		});
	}

	[Test]
	public static void Test()
	{
		Console.WriteLine(compile(table));
	}
}