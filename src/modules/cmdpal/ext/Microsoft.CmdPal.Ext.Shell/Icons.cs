// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell;

internal sealed class Icons
{
    internal static IconInfo RunV2Icon { get; } = IconHelpers.FromRelativePath("Assets\\Run.svg");

    internal static IconInfo FolderIcon { get; } = new IconInfo("📁");

    internal static IconInfo AdminIcon { get; } = new IconInfo("\xE7EF"); // Admin Icon

    internal static IconInfo UserIcon { get; } = new IconInfo("\xE7EE"); // User Icon

    internal static IconInfo HistoryIcon { get; } = new IconInfo("\uE81C"); // HistoryIcon

    internal static IconInfo OpenUrlIcon { get; } = new IconInfo("\ue8a7"); // Open
}
