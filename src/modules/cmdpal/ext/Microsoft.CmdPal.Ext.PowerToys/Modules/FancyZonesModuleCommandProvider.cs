// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using PowerToysExtension.Pages;
using PowerToysExtension.Properties;
using static Common.UI.SettingsDeepLink;

namespace PowerToysExtension.Modules;

internal sealed class FancyZonesModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var module = SettingsWindow.FancyZones;
        var title = module.ModuleDisplayName();
        var icon = module.ModuleIcon();

        if (ModuleEnablementService.IsModuleEnabled(module))
        {
            yield return new ListItem(new CommandItem(new FancyZonesLayoutsPage()))
            {
                Title = Resources.FancyZones_Layouts_Title,
                Subtitle = Resources.FancyZones_Layouts_Subtitle,
                Icon = icon,
            };

            yield return new ListItem(new CommandItem(new FancyZonesMonitorsPage()))
            {
                Title = Resources.FancyZones_Monitors_Title,
                Subtitle = Resources.FancyZones_Monitors_Subtitle,
                Icon = icon,
            };

            yield return new ListItem(new OpenFancyZonesEditorCommand())
            {
                Title = Resources.FancyZones_OpenEditor_Title,
                Subtitle = Resources.FancyZones_OpenEditor_Subtitle,
                Icon = icon,
            };
        }

        yield return new ListItem(new OpenInSettingsCommand(module, title))
        {
            Title = title,
            Subtitle = Resources.FancyZones_Settings_Subtitle,
            Icon = icon,
        };
    }
}
