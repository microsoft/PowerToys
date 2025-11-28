// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Common.UI;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Modules;

internal sealed class ColorPickerModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var title = SettingsDeepLink.SettingsWindow.ColorPicker.ModuleDisplayName();
        var icon = SettingsDeepLink.SettingsWindow.ColorPicker.ModuleIcon();

        var more = new List<CommandContextItem>
        {
            new CommandContextItem(new CopyColorCommand()),
        };

        var item = new ListItem(new OpenInSettingsCommand(SettingsDeepLink.SettingsWindow.ColorPicker, title))
        {
            Title = title,
            Subtitle = "Open Color Picker settings",
            Icon = icon,
            MoreCommands = more.ToArray(),
        };

        return [item];
    }
}
