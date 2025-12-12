// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Common.UI;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using PowerToysExtension.Pages;

namespace PowerToysExtension.Modules;

internal sealed class ColorPickerModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var module = SettingsDeepLink.SettingsWindow.ColorPicker;
        var title = module.ModuleDisplayName();
        var icon = module.ModuleIcon();

        var commands = new List<ListItem>();

        if (ModuleEnablementService.IsModuleEnabled(module))
        {
            // Quick actions under MoreCommands.
            var more = new List<CommandContextItem>
            {
                new CommandContextItem(new OpenColorPickerCommand()),
                new CommandContextItem(new CopyColorCommand()),
                new CommandContextItem(new ColorPickerSavedColorsPage()),
            };

            commands.Add(new ListItem(new OpenInSettingsCommand(module, title))
            {
                Title = title,
                Subtitle = "Open Color Picker settings",
                Icon = icon,
                MoreCommands = more.ToArray(),
            });

            // Direct entries in the module list.
            commands.Add(new ListItem(new OpenColorPickerCommand())
            {
                Title = "Open Color Picker",
                Subtitle = "Start a color pick session",
                Icon = icon,
            });

            commands.Add(new ListItem(new CommandItem(new ColorPickerSavedColorsPage()))
            {
                Title = "Saved colors",
                Subtitle = "Browse and copy saved colors",
                Icon = icon,
            });
        }
        else
        {
            commands.Add(new ListItem(new OpenInSettingsCommand(module, title))
            {
                Title = title,
                Subtitle = "Open Color Picker settings",
                Icon = icon,
            });
        }

        return commands;
    }
}
