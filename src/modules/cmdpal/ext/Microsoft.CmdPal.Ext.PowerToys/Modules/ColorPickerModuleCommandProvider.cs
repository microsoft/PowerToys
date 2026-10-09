// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Common.UI;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using PowerToysExtension.Pages;
using PowerToysExtension.Properties;

namespace PowerToysExtension.Modules;

internal sealed class ColorPickerModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var module = SettingsDeepLink.SettingsWindow.ColorPicker;
        var title = module.ModuleDisplayName();
        var icon = module.ModuleIcon();

        var commands = new List<ListItem>();

        commands.Add(new ListItem(new OpenInSettingsCommand(module, title) { Id = "com.microsoft.powertoys.colorPicker.openSettings" })
        {
            Title = title,
            Subtitle = Resources.ColorPicker_Settings_Subtitle,
            Icon = icon,
        });

        if (!ModuleEnablementService.IsModuleEnabled(module))
        {
            return commands;
        }

        // Direct entries in the module list.
        commands.Add(new ListItem(new OpenColorPickerCommand() { Id = "com.microsoft.powertoys.colorPicker.open" })
        {
            Title = Resources.ColorPicker_Open_Title,
            Subtitle = Resources.ColorPicker_Open_Subtitle,
            Icon = icon,
        });

        commands.Add(new ListItem(new CommandItem(new ColorPickerSavedColorsPage() { Id = "com.microsoft.powertoys.colorPicker.savedColors" }))
        {
            Title = Resources.ColorPicker_SavedColors_Title,
            Subtitle = Resources.ColorPicker_SavedColors_Subtitle,
            Icon = icon,
        });

        return commands;
    }
}
