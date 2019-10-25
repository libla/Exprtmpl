using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exprtmpl;

public static class ExprtmplTest
{
	[Test]
	public static async Task Test()
	{
		string str = @"
12345 ={test}678
# for i = 1, 3
 # if i % 2 != 0
={i + 1}
 # end
# end
# for k, v in {name:'libla', age:test}
={k} ={v}
# end
";
		Func<Table, string> func = await str.Compile();
		Dictionary<string, Value> values = new Dictionary<string, Value> {
			{"test", "libla"},
		};
		Console.WriteLine(func(Table.From(values)));
	}
}