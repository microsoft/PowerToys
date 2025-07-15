// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WinGet;

internal sealed class Icons
{
    internal static IconInfo WinGetIcon { get; } = IconHelpers.FromRelativePath("Assets\\WinGet.svg");

    internal static IconInfo ExtensionsIcon { get; } = IconHelpers.FromRelativePath("Assets\\Extension.svg");

    internal static IconInfo StoreIcon { get; } = IconHelpers.FromRelativePaths("Assets\\Store.light.svg", "Assets\\Store.dark.svg");

    internal static IconInfo CompletedIcon { get; } = new("\uE930"); // Completed

    internal static IconInfo UpdateIcon { get; } = new("\uE74A"); // Up

    internal static IconInfo DownloadIcon { get; } = new("\uE896"); // Download

    internal static IconInfo DeleteIcon { get; } = new("\uE74D"); // Delete
}
