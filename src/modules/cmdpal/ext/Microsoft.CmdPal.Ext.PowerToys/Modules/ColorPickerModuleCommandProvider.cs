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

        commands.Add(new ListItem(new OpenInSettingsCommand(module, title))
        {
            Title = title,
            Subtitle = "Open Color Picker settings",
            Icon = icon,
        });

        if (!ModuleEnablementService.IsModuleEnabled(module))
        {
            return commands;
        }

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

        return commands;
    }
}
