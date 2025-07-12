// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Helpers;

public static partial class Icons
{
    public static IconInfo UnpinIcon { get; } = new("\uE77A");

    public static IconInfo PinIcon { get; } = new("\uE840");

    public static IconInfo RunAsIcon { get; } = new("\uE7EF");

    public static IconInfo RunAsUserIcon { get; } = new("\uE7EE");

    public static IconInfo CopyIcon { get; } = new("\ue8c8");

    public static IconInfo OpenConsoleIcon { get; } = new("\ue838");

    public static IconInfo OpenPathIcon { get; } = new("\ue838");

    public static IconInfo AllAppsIcon { get; } = IconHelpers.FromRelativePath("Assets\\AllApps.svg");
}
