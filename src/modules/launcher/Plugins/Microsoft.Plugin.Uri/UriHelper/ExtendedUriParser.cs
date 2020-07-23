using System;
using Microsoft.Plugin.Uri.Interface;

namespace Microsoft.Plugin.Uri.UriHelper
{
    public class ExtendedUriParser : IUriParser
    {
        public bool TryParse(string input, out System.Uri result)
        {
            if (string.IsNullOrEmpty(input))
            {
                result = default;
                return false;
            }

            var urlBuilder = new UriBuilder(input);

            result = urlBuilder.Uri;
            return true;
        }
    }
}
