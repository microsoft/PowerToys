// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using PowerToysExtension.Pages;
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
                Title = "FancyZones: Layouts",
                Subtitle = "Apply a layout to all monitors or a specific monitor",
                Icon = icon,
            };

            yield return new ListItem(new CommandItem(new FancyZonesMonitorsPage()))
            {
                Title = "FancyZones: Monitors",
                Subtitle = "Identify monitors and apply layouts",
                Icon = icon,
            };

            yield return new ListItem(new OpenFancyZonesEditorCommand())
            {
                Title = "Open FancyZones Editor",
                Subtitle = "Launch layout editor",
                Icon = icon,
            };
        }

        yield return new ListItem(new OpenInSettingsCommand(module, title))
        {
            Title = title,
            Subtitle = "Open FancyZones settings",
            Icon = icon,
        };
    }
}
