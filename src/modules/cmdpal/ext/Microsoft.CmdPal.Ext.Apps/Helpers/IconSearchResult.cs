// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Apps.Helpers;

/// <summary>
/// Result of an icon search operation.
/// </summary>
internal readonly record struct IconSearchResult(
    string? LogoPath,
    bool IsHighContrast,
    bool IsTargetSizeIcon,
    int? KnownSize = null)
{
    /// <summary>
    /// Gets a value indicating whether an icon was found.
    /// </summary>
    public bool IsFound => LogoPath is not null;

    /// <summary>
    /// Returns true if we can confirm the icon meets the minimum size.
    /// Only possible for targetsize icons where the size is encoded in the filename.
    /// </summary>
    public bool MeetsMinimumSize(int minimumSize)
    {
        return IsTargetSizeIcon && KnownSize >= minimumSize;
    }

    /// <summary>
    /// Returns true if we know the icon is undersized.
    /// Returns false if not found, or if size is unknown (scale-based icons).
    /// </summary>
    public bool IsKnownUndersized(int minimumSize)
    {
        return IsTargetSizeIcon && KnownSize < minimumSize;
    }

    public static IconSearchResult NotFound()
    {
        return new(null, default, false);
    }

    /// <summary>Records a discovered asset whose pixel size is known from its target-size qualifier.</summary>
    public static IconSearchResult FoundTargetSize(string path, bool isHighContrast, int size)
    {
        return new(path, isHighContrast, IsTargetSizeIcon: true, size);
    }

    /// <summary>Records a discovered scale-qualified asset whose exact pixel size is not established.</summary>
    public static IconSearchResult FoundScaled(string path, bool isHighContrast)
    {
        return new(path, isHighContrast, IsTargetSizeIcon: false);
    }
}
