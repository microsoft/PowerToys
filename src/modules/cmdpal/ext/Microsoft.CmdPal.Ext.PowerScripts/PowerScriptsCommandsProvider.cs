// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerScripts;

/// <summary>
/// Surfaces every PowerScript that declares the <c>commandPalette</c> surface as a top-level command.
/// This is a built-in provider, so the Command Palette shows it in Settings → Extensions with the
/// standard per-provider enable toggle — no extra settings UI is required. Selecting an entry runs the
/// script through <c>PowerScripts.Host.exe</c>, which also drives the optional parameter prompt.
/// </summary>
public sealed partial class PowerScriptsCommandsProvider : CommandProvider
{
    private static readonly IconInfo PowerScriptsIcon = new("\uE756");

    private readonly ICommandItem[] _commands;

    public PowerScriptsCommandsProvider()
    {
        Id = "com.microsoft.cmdpal.builtin.powerscripts";
        DisplayName = "PowerScripts";
        Icon = PowerScriptsIcon;

        _commands = BuildCommands();
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    private static ICommandItem[] BuildCommands()
    {
        var items = new List<ICommandItem>();

        foreach (var script in PowerScriptHostClient.ListCommandPaletteScripts())
        {
            var command = new RunPowerScriptCommand(script.Id, script.Name, PowerScriptsIcon);

            items.Add(new CommandItem(command)
            {
                Title = script.Name,
                Subtitle = script.Description,
                Icon = PowerScriptsIcon,
            });
        }

        return items.ToArray();
    }
}
