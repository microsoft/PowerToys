using System;

namespace Microsoft.Plugin.Uri.Interface
{
	public interface IUriParser
	{
		bool TryParse(string input, out System.Uri result);
	}
}