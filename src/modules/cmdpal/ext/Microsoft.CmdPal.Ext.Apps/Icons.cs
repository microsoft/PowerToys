// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps;

internal static class Icons
{
    internal static IconInfo AllAppsIcon { get; } = IconHelpers.FromRelativePath("Assets\\AllApps.svg");

    internal static IconInfo AllAppsFilterIcon { get; } = new("\uE71D"); // AllApps

    internal static IconInfo Win32AppsFilterIcon { get; } = new("\uECAA"); // AppIconDefault

    internal static IconInfo PackagedAppsFilterIcon { get; } = new("\uE7B8"); // Package

    internal static IconInfo WebAppsFilterIcon { get; } = new("\uE774"); // Globe

    internal static IconInfo RunAsUserIcon { get; } = new("\uE7EE"); // OtherUser icon

    internal static IconInfo RunAsAdminIcon { get; } = new("\uE7EF"); // Admin icon

    internal static IconInfo OpenPathIcon { get; } = new("\ue838"); // Folder Open icon

    public static IconInfo UninstallApplicationIcon { get; } = new("\uE74D"); // Uninstall icon

    public static IconInfo GenericAppIcon { get; } = new("\uE737"); // Favicon

    internal static IconInfo Reloading { get; } = new("\uF16A"); // ProgressRing

    internal static IconInfo Refresh { get; } = new("\uE72C"); // Refresh

    internal static IconInfo Hide { get; } = new("\uED1A"); // Hide

    internal static IconInfo Unhide { get; } = new("\uE890"); // View
}
