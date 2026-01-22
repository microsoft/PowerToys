// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps;

internal static class Icons
{
    internal static IconInfo AllAppsIcon { get; } = IconHelpers.FromRelativePath("Assets\\AllApps.svg");

    internal static IconInfo RunAsUserIcon { get; } = new("\uE7EE"); // OtherUser icon

    internal static IconInfo RunAsAdminIcon { get; } = new("\uE7EF"); // Admin icon

    internal static IconInfo OpenPathIcon { get; } = new("\ue838"); // Folder Open icon

    internal static IconInfo CopyIcon { get; } = new("\ue8c8"); // Copy icon

    public static IconInfo UnpinIcon { get; } = new("\uE77A"); // Unpin icon

    public static IconInfo PinIcon { get; } = new("\uE840"); // Pin icon

    public static IconInfo UninstallApplicationIcon { get; } = new("\uE74D"); // Uninstall icon

    public static IconInfo GenericAppIcon { get; } = new("\uE737"); // Favicon
}
