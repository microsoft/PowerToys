// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;
using PowerToysExtension.Properties;

namespace PowerToysExtension;

public sealed partial class PowerToysCommandsProvider : CommandProvider
{
    public PowerToysCommandsProvider()
    {
        DisplayName = Resources.PowerToys_DisplayName;
        Icon = PowerToysResourcesHelper.IconFromSettingsIcon("PowerToys.png");
    }

    public override ICommandItem[] TopLevelCommands() =>
    [
        new CommandItem(new Pages.PowerToysListPage())
        {
            Title = Resources.PowerToys_DisplayName,
            Subtitle = Resources.PowerToys_Subtitle,
        }
    ];

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
