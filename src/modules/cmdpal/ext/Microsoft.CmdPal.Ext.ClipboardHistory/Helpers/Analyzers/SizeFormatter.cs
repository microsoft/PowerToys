// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers.Analyzers;

/// <summary>
/// Utility for formatting byte sizes to a human-readable string.
/// </summary>
internal static class SizeFormatter
{
    private const long KB = 1024;
    private const long MB = 1024 * KB;
    private const long GB = 1024 * MB;

    public static string FormatSize(long bytes)
    {
        return bytes switch
        {
            >= GB => string.Format(CultureInfo.CurrentCulture, "{0:F2} GB", (double)bytes / GB),
            >= MB => string.Format(CultureInfo.CurrentCulture, "{0:F2} MB", (double)bytes / MB),
            >= KB => string.Format(CultureInfo.CurrentCulture, "{0:F2} KB", (double)bytes / KB),
            _ => string.Format(CultureInfo.CurrentCulture, "{0} B", bytes),
        };
    }

    public static string FormatSize(ulong bytes)
    {
        // Use double for division to avoid overflow; thresholds mirror long version
        if (bytes >= (ulong)GB)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0:F2} GB", bytes / (double)GB);
        }

        if (bytes >= (ulong)MB)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0:F2} MB", bytes / (double)MB);
        }

        if (bytes >= (ulong)KB)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0:F2} KB", bytes / (double)KB);
        }

        return string.Format(CultureInfo.CurrentCulture, "{0} B", bytes);
    }
}
