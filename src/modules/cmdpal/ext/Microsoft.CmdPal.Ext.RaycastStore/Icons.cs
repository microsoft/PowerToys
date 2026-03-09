// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RaycastStore;

internal sealed class Icons
{
    internal static IconInfo RaycastIcon { get; } = new("\ue74c");

    internal static IconInfo ExtensionIcon { get; } = new("\ue8f1");

    internal static IconInfo SearchIcon { get; } = new("\ue721");

    internal static IconInfo InfoIcon { get; } = new("\ue946");

    internal static IconInfo DownloadIcon { get; } = new("\ue896");

    internal static IconInfo WarningIcon { get; } = new("\ue7ba");

    internal static IconInfo NodeJsIcon { get; } = new("\ue943");

    internal static IconInfo InstalledIcon { get; } = new("\ue73e");
}
