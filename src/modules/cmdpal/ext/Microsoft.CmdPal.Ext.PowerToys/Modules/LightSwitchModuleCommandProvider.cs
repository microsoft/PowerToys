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
        var title = SettingsWindow.LightSwitch.ModuleDisplayName();
        var icon = SettingsWindow.LightSwitch.ModuleIcon();

        var items = new List<ListItem>
        {
            new ListItem(new ToggleLightSwitchCommand())
            {
                Title = "Toggle Light Switch",
                Subtitle = "Toggle system/apps theme immediately",
                Icon = icon,
            },
            new ListItem(new OpenInSettingsCommand(SettingsWindow.LightSwitch, title))
            {
                Title = title,
                Subtitle = "Open Light Switch settings",
                Icon = icon,
            },
        };

        return items;
    }
}
