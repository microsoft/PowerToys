// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;

internal static class UrlHelper
{
    /// <summary>
    /// Validates if a string is a valid URL
    /// </summary>
    /// <param name="url">The string to validate</param>
    /// <returns>True if the string is a valid URL, false otherwise</returns>
    internal static bool IsValidUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!url.Contains('.', StringComparison.OrdinalIgnoreCase))
        {
            // eg: 'com', 'org'. We don't think it's a valid url.
            // This can simplify the logic of checking if the url is valid.
            return false;
        }

        if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
        {
            return true;
        }

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.IsWellFormedUriString("https://" + url, UriKind.Absolute))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Normalizes a URL by adding https:// if no schema is present
    /// </summary>
    /// <param name="url">The URL to normalize</param>
    /// <returns>Normalized URL with schema</returns>
    internal static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
        {
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }
        }

        return url;
    }
}
