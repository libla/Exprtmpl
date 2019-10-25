using System;

namespace Exprtmpl
{
	public class CompileException : Exception
	{
		internal CompileException(int row, int column) { }
	}
}
