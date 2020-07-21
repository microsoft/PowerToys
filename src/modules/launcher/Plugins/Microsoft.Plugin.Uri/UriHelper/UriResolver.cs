using System.Net;
using System.Threading.Tasks;
using Microsoft.Plugin.Uri.Interface;

namespace Microsoft.Plugin.Uri.UriHelper
{
	public class UriResolver : IUrlResolver
	{
		public bool IsValidHost(System.Uri uri)
		{
			try
			{
				Dns.GetHostEntry(uri.Host);
				return true;
			}
			catch
			{
				//No valid url
			}

			return false;
		}

		public async ValueTask<bool> IsValidHostAsync(System.Uri uri)
		{
			try
			{
				await Dns.GetHostEntryAsync(uri.Host).ConfigureAwait(false);
				return true;
			}
			catch
			{
				//No valid url
			}

			return false;
		}
	}
}
