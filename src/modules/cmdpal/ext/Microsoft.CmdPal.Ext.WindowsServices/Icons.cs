// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowsServices;

internal sealed class Icons
{
    internal static IconInfo ServicesIcon { get; } = IconHelpers.FromRelativePath("Assets\\Services.svg");

    internal static IconInfo StopIcon { get; } = new IconInfo("\xE71A"); // Stop icon

    internal static IconInfo PlayIcon { get; } = new IconInfo("\xEDB5"); // PlayBadge12 icon

    internal static IconInfo RefreshIcon { get; } = new IconInfo("\xE72C"); // Refresh icon

    internal static IconInfo OpenIcon { get; } = new IconInfo("\xE8A7"); // OpenInNewWindow icon

    internal static IconInfo PauseIcon { get; } = new("\u23F8"); // unicode DOUBLE VERTICAL BAR, aka, "Pause"
}
