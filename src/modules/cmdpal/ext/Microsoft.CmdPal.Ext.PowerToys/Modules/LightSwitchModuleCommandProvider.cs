// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using static Common.UI.SettingsDeepLink;

namespace PowerToysExtension.Modules;

internal sealed class LightSwitchModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var module = SettingsWindow.LightSwitch;
        var title = module.ModuleDisplayName();
        var icon = module.ModuleIcon();

        var items = new List<ListItem>();

        if (ModuleEnablementService.IsModuleEnabled(module))
        {
            items.Add(new ListItem(new ToggleLightSwitchCommand())
            {
                Title = "Toggle Light Switch",
                Subtitle = "Toggle system/apps theme immediately",
                Icon = icon,
            });
        }

        items.Add(new ListItem(new OpenInSettingsCommand(module, title))
        {
            Title = title,
            Subtitle = "Open Light Switch settings",
            Icon = icon,
        });

        return items;
    }
}
