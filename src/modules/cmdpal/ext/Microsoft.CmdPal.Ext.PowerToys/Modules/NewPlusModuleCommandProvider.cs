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

internal sealed class NewPlusModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var title = SettingsWindow.NewPlus.ModuleDisplayName();
        var icon = SettingsWindow.NewPlus.ModuleIcon();

        yield return new ListItem(new OpenInSettingsCommand(SettingsWindow.NewPlus, title) { Id = "com.microsoft.powertoys.newPlus.openSettings" })
        {
            Title = title,
            Subtitle = Resources.NewPlus_Settings_Subtitle,
            Icon = icon,
        };
    }
}
