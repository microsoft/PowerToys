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

internal sealed class PowerToysRunModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var title = SettingsWindow.PowerLauncher.ModuleDisplayName();
        var icon = SettingsWindow.PowerLauncher.ModuleIcon();

        yield return new ListItem(new OpenInSettingsCommand(SettingsWindow.PowerLauncher, title) { Id = "com.microsoft.powertoys.powerToysRun.openSettings" })
        {
            Title = title,
            Subtitle = Resources.PowerToysRun_Settings_Subtitle,
            Icon = icon,
        };
    }
}
