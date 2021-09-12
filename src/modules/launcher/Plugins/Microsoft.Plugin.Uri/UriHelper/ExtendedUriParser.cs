// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Plugin.Uri.Interfaces;

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

            // Handle common cases UriBuilder does not handle
            // Using CurrentCulture since this is a user typed string
            if (input.EndsWith(":", StringComparison.CurrentCulture)
                || input.EndsWith(".", StringComparison.CurrentCulture)
                || input.EndsWith(":/", StringComparison.CurrentCulture))
            {
                result = default;
                return false;
            }

            try
            {
                var urlBuilder = new UriBuilder(input);

                result = urlBuilder.Uri;
                return true;
            }
            catch (System.UriFormatException)
            {
                result = default;
                return false;
            }
        }
    }
}
