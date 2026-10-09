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

        root.AddCommand(new OpenEditorCommand());
        root.AddCommand(new GetMonitorsCommand());
        root.AddCommand(new GetLayoutsCommand());
        root.AddCommand(new GetActiveLayoutCommand());
        root.AddCommand(new SetLayoutCommand());
        root.AddCommand(new OpenSettingsCommand());
        root.AddCommand(new GetHotkeysCommand());
        root.AddCommand(new SetHotkeyCommand());
        root.AddCommand(new RemoveHotkeyCommand());

        return root;
    }
}
