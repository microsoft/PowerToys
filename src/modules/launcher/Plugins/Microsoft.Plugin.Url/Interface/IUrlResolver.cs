using System;
using System.Threading.Tasks;

namespace Microsoft.Plugin.Uri.Interface
{
	public interface IUrlResolver
	{
		bool IsValidHost(System.Uri uri);

		ValueTask<bool> IsValidHostAsync(System.Uri uri);

	}
}
