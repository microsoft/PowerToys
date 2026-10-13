// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using FancyZonesCLI.CommandLine.Commands;

namespace FancyZonesCLI.CommandLine;

internal static class FancyZonesCliCommandFactory
{
    public static RootCommand CreateRootCommand()
    {
        var root = new RootCommand("FancyZones CLI - Command line interface for FancyZones");

        root.Subcommands.Add(new OpenEditorCommand());
        root.Subcommands.Add(new GetMonitorsCommand());
        root.Subcommands.Add(new GetLayoutsCommand());
        root.Subcommands.Add(new GetActiveLayoutCommand());
        root.Subcommands.Add(new SetLayoutCommand());
        root.Subcommands.Add(new OpenSettingsCommand());
        root.Subcommands.Add(new GetHotkeysCommand());
        root.Subcommands.Add(new SetHotkeyCommand());
        root.Subcommands.Add(new RemoveHotkeyCommand());

        return root;
    }
}
