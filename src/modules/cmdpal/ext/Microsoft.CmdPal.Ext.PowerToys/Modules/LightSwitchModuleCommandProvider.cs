// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using PowerToysExtension.Properties;
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
            items.Add(new ListItem(new ToggleLightSwitchCommand() { Id = "com.microsoft.powertoys.lightSwitch.toggle" })
            {
                Title = Resources.LightSwitch_Toggle_Title,
                Subtitle = Resources.LightSwitch_Toggle_Subtitle,
                Icon = icon,
            });
        }

        items.Add(new ListItem(new OpenInSettingsCommand(module, title) { Id = "com.microsoft.powertoys.lightSwitch.openSettings" })
        {
            Title = title,
            Subtitle = Resources.LightSwitch_Settings_Subtitle,
            Icon = icon,
        });

        return items;
    }
}
