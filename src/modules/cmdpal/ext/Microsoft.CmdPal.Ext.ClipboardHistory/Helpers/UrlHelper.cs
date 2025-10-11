// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Core.Common.Helpers;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;

internal static class UrlHelper
{
    /// <summary>
    /// Validates if a string is a valid URL or file path
    /// </summary>
    /// <param name="url">The string to validate</param>
    /// <returns>True if the string is a valid URL or file path, false otherwise</returns>
    internal static bool IsValidUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        // Trim whitespace for validation
        url = url.Trim();

        // URLs should not contain newlines
        if (url.Contains('\n', StringComparison.Ordinal) || url.Contains('\r', StringComparison.Ordinal))
        {
            return false;
        }

        // Check if it's a valid file path (local or network)
        if (PathHelper.IsValidFilePath(url))
        {
            return true;
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
    /// Normalizes a URL or file path by adding appropriate schema if none is present
    /// </summary>
    /// <param name="url">The URL or file path to normalize</param>
    /// <returns>Normalized URL or file path with schema</returns>
    internal static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        // Trim whitespace
        url = url.Trim();

        // If it's a valid file path, convert to file:// URI
        if (!url.StartsWith("file://", StringComparison.OrdinalIgnoreCase) && PathHelper.IsValidFilePath(url))
        {
            try
            {
                // Convert to file URI (path is already absolute since we only accept absolute paths)
                return new Uri(url).ToString();
            }
            catch
            {
                // If conversion fails, return original
                return url;
            }
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
