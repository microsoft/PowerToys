// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerScripts;

/// <summary>
/// Provides a browser for compatible PowerScripts and their consumer-owned parameter forms.
/// </summary>
public sealed partial class PowerScriptsCommandsProvider : CommandProvider
{
    private static readonly IconInfo PowerScriptsIcon = new("\uE756");

    private readonly PowerScriptsListPage _page;
    private readonly ICommandItem[] _commands;

    public PowerScriptsCommandsProvider()
        : this(new PowerScriptsService())
    {
    }

    internal PowerScriptsCommandsProvider(IPowerScriptsService service)
    {
        Id = "com.microsoft.cmdpal.builtin.powerscripts";
        DisplayName = "PowerScripts";
        Icon = PowerScriptsIcon;

        _page = new PowerScriptsListPage(service);
        _commands =
        [
            new CommandItem(_page)
            {
                Title = DisplayName,
                Icon = PowerScriptsIcon,
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override void Dispose()
    {
        _page.Dispose();
        base.Dispose();
    }
}
