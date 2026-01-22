// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowsTerminal;

internal static class Icons
{
    internal static IconInfo TerminalIcon { get; } = IconHelpers.FromRelativePath("Assets\\WindowsTerminal.svg");

    internal static IconInfo AdminIcon { get; } = new IconInfo("\xE7EF"); // Admin icon

    internal static IconInfo FilterIcon { get; } = new IconInfo("\uE71C"); // Funnel icon
}
