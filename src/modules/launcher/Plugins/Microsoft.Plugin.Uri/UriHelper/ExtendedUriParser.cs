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
            if (string.IsNullOrEmpty(input))
            {
                webUri = default;
                systemUri = null;
                return false;
            }

            // Handling URL with only scheme, typically mailto or application uri.
            // Do nothing, return the result without urlBuilder
            // And check if scheme match REC3986 (issue #15035)
            const string schemeRegex = @"^([a-z][a-z0-9+\-.]*):";
            if (input.EndsWith(":", StringComparison.OrdinalIgnoreCase)
                && !input.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                && !input.Contains('/', StringComparison.OrdinalIgnoreCase)
                && !input.All(char.IsDigit)
                && Regex.IsMatch(input, schemeRegex))
            {
                webUri = null;
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
                webUri = default;
                systemUri = null;
                return false;
            }

            try
            {
                string isDomainPortRegex = @"^[\w\.]+:\d+";
                string isIPv6PortRegex = @"^\[([\w:]+:+)+[\w]+\]:\d+";
                var urlBuilder = new UriBuilder(input);
                var hadDefaultPort = urlBuilder.Uri.IsDefaultPort;
                urlBuilder.Port = hadDefaultPort ? -1 : urlBuilder.Port;

                if (input.StartsWith("HTTP://", StringComparison.OrdinalIgnoreCase))
                {
                    urlBuilder.Scheme = System.Uri.UriSchemeHttp;
                    systemUri = null;
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
                        webUri = null;
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
                    systemUri = null;
                }

                if (urlBuilder.Scheme.Equals(System.Uri.UriSchemeHttp, StringComparison.Ordinal) ||
                    urlBuilder.Scheme.Equals(System.Uri.UriSchemeHttps, StringComparison.Ordinal))
                {
                    webUri = urlBuilder.Uri;
                }
                else
                {
                    webUri = null;
                }

                return true;
            }
            catch (UriFormatException)
            {
                webUri = default;
                systemUri = null;
                return false;
            }
        }
    }
}
