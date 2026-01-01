// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension;

public partial class PowerToysExtensionCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;

    public PowerToysExtensionCommandsProvider()
    {
        DisplayName = "PowerToys";
        Icon = PowerToysResourcesHelper.IconFromSettingsIcon("PowerToys.png");
        _commands = [
            new CommandItem(new Pages.PowerToysListPage())
            {
                Title = "PowerToys",
                Subtitle = "PowerToys commands and settings",
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

    public override IFallbackCommandItem[] FallbackCommands()
    {
        var items = ModuleCommandCatalog.GetAllItems();
        var fallbacks = new List<IFallbackCommandItem>(items.Length);
        foreach (var item in items)
        {
            if (item?.Command is not ICommand cmd)
            {
                continue;
            }

            fallbacks.Add(new PowerToysFallbackCommandItem(cmd, item.Title, item.Subtitle, item.Icon, item.MoreCommands));
        }

        return fallbacks.ToArray();
    }
}
