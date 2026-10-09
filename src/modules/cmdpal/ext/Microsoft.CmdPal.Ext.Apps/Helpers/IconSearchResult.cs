// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Apps.Programs;

namespace Microsoft.CmdPal.Ext.Apps.Helpers;

/// <summary>
/// Result of an icon search operation.
/// </summary>
internal readonly record struct IconSearchResult(
    string? LogoPath,
    LogoType LogoType,
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
    public bool MeetsMinimumSize(int minimumSize) =>
        IsTargetSizeIcon && KnownSize >= minimumSize;

    /// <summary>
    /// Returns true if we know the icon is undersized.
    /// Returns false if not found, or if size is unknown (scale-based icons).
    /// </summary>
    public bool IsKnownUndersized(int minimumSize) =>
        IsTargetSizeIcon && KnownSize < minimumSize;

    public static IconSearchResult NotFound() => new(null, default, false);

    public static IconSearchResult FoundTargetSize(string path, LogoType logoType, int size)
        => new(path, logoType, IsTargetSizeIcon: true, size);

    public static IconSearchResult FoundScaled(string path, LogoType logoType)
        => new(path, logoType, IsTargetSizeIcon: false);
}
