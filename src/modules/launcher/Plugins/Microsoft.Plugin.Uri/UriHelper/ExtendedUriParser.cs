// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Plugin.Uri.Interfaces;

namespace Microsoft.Plugin.Uri.UriHelper
{
    public class ExtendedUriParser : IUriParser
    {
        // When updating this method, also update the local method IsUri() in Community.PowerToys.Run.Plugin.WebSearch.Main.Query
        public bool TryParse(string input, out System.Uri result, out bool isWebUri)
        {
            if (string.IsNullOrEmpty(input))
            {
                result = default;
                isWebUri = false;
                return false;
            }

            // Handling URL with only scheme, typically mailto or application uri.
            // Do nothing, return the result without urlBuilder
            // And check if scheme match REC3986 (issue #15035)
            const string schemeRegex = @"^([a-z][a-z0-9+\-.]*):";
            if (input.EndsWith(":", StringComparison.OrdinalIgnoreCase)
                && !input.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                && !input.Contains("/", StringComparison.OrdinalIgnoreCase)
                && !input.All(char.IsDigit)
                && Regex.IsMatch(input, schemeRegex))
            {
                result = new System.Uri(input);
                isWebUri = false;
                return true;
            }

            // Handle common cases UriBuilder does not handle
            // Using CurrentCulture since this is a user typed string
            if (input.EndsWith(":", StringComparison.CurrentCulture)
                || input.EndsWith(".", StringComparison.CurrentCulture)
                || input.EndsWith(":/", StringComparison.CurrentCulture)
                || input.EndsWith("://", StringComparison.CurrentCulture)
                || input.All(char.IsDigit))
            {
                result = default;
                isWebUri = false;
                return false;
            }

            try
            {
                var urlBuilder = new UriBuilder(input);
                var hadDefaultPort = urlBuilder.Uri.IsDefaultPort;
                urlBuilder.Port = hadDefaultPort ? -1 : urlBuilder.Port;

                if (input.StartsWith("HTTP://", StringComparison.OrdinalIgnoreCase))
                {
                    urlBuilder.Scheme = System.Uri.UriSchemeHttp;
                    isWebUri = true;
                }
                else if (input.Contains(":", StringComparison.OrdinalIgnoreCase) &&
                        !input.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                        !input.Contains("[", StringComparison.OrdinalIgnoreCase))
                {
                    // Do nothing, leave unchanged
                    isWebUri = false;
                }
                else
                {
                    urlBuilder.Scheme = System.Uri.UriSchemeHttps;
                    isWebUri = true;
                }

                result = urlBuilder.Uri;
                return true;
            }
            catch (UriFormatException)
            {
                result = default;
                isWebUri = false;
                return false;
            }
        }
    }
}
