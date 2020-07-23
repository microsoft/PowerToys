using Microsoft.Plugin.Uri.Interface;

namespace Microsoft.Plugin.Uri.UriHelper
{
    public class UriResolver : IUrlResolver
    {
        public bool IsValidHost(System.Uri uri)
        {
            return true;
        }
    }
}
