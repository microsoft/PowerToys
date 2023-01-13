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
        public bool TryParse(string input, out System.Uri webUri, out System.Uri systemUri)
        {
            webUri = default;
            systemUri = default;

            if (string.IsNullOrEmpty(input))
            {
                return false;
            }

            // Handling URL with only scheme, typically mailto or application uri.
            // Do nothing, return the result without urlBuilder
            // And check if scheme match RFC3986 (issue #15035)
            const string schemeRegex = @"^([a-z][a-z0-9+\-.]*):";
            if (input.EndsWith(":", StringComparison.OrdinalIgnoreCase)
                && !input.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                && !input.Contains('/', StringComparison.OrdinalIgnoreCase)
                && !input.All(char.IsDigit)
                && Regex.IsMatch(input, schemeRegex))
            {
                systemUri = new System.Uri(input);
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
                return false;
            }

            try
            {
                string isDomainPortRegex = @"^[\w\.]+:\d+";
                string isIPv6PortRegex = @"^\[([\w:]+:+)+[\w]+\]:\d+";
                var urlBuilder = new UriBuilder(input);
                urlBuilder.Port = urlBuilder.Uri.IsDefaultPort ? -1 : urlBuilder.Port;

                if (input.StartsWith("HTTP://", StringComparison.OrdinalIgnoreCase))
                {
                    urlBuilder.Scheme = System.Uri.UriSchemeHttp;
                }
                else if (Regex.IsMatch(input, isDomainPortRegex) ||
                         Regex.IsMatch(input, isIPv6PortRegex))
                {
                    var secondUrlBuilder = urlBuilder;

                    try
                    {
                        urlBuilder = new UriBuilder("https://" + input);

                        if (urlBuilder.Port == 80)
                        {
                            urlBuilder.Scheme = System.Uri.UriSchemeHttp;
                        }
                    }
                    catch (UriFormatException)
                    {
                        // This handles the situation in tel:xxxx and others
                        // When xxxx > 65535, it will throw UriFormatException
                        // The catch ensures it will at least still try to return a systemUri
                    }

                    string singleLabelRegex = @"[\.:]+|^http$|^https$|^localhost$";
                    systemUri = Regex.IsMatch(urlBuilder.Host, singleLabelRegex) ? null : secondUrlBuilder.Uri;
                }
                else if (input.Contains(':', StringComparison.OrdinalIgnoreCase) &&
                        !input.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                        !input.Contains('[', StringComparison.OrdinalIgnoreCase))
                {
                    // Do nothing, leave unchanged
                    systemUri = urlBuilder.Uri;
                }
                else
                {
                    urlBuilder.Scheme = System.Uri.UriSchemeHttps;
                }

                if (urlBuilder.Scheme.Equals(System.Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                    urlBuilder.Scheme.Equals(System.Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    webUri = urlBuilder.Uri;
                }

                return true;
            }
            catch (UriFormatException)
            {
                return false;
            }
        }
    }
}
