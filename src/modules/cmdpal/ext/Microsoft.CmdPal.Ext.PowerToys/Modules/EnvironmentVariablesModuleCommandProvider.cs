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

internal sealed class EnvironmentVariablesModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var module = SettingsWindow.EnvironmentVariables;
        var title = module.ModuleDisplayName();
        var icon = module.ModuleIcon();

        if (ModuleEnablementService.IsModuleEnabled(module))
        {
            yield return new ListItem(new OpenEnvironmentVariablesCommand() { Id = "com.microsoft.powertoys.environmentVariables.open" })
            {
                Title = Resources.EnvironmentVariables_Open_Title,
                Subtitle = Resources.EnvironmentVariables_Open_Subtitle,
                Icon = icon,
            };

            yield return new ListItem(new OpenEnvironmentVariablesAdminCommand() { Id = "com.microsoft.powertoys.environmentVariables.openAdmin" })
            {
                Title = Resources.EnvironmentVariables_OpenAdmin_Title,
                Subtitle = Resources.EnvironmentVariables_OpenAdmin_Subtitle,
                Icon = icon,
            };
        }

        yield return new ListItem(new OpenInSettingsCommand(module, title) { Id = "com.microsoft.powertoys.environmentVariables.openSettings" })
        {
            Title = title,
            Subtitle = Resources.EnvironmentVariables_Settings_Subtitle,
            Icon = icon,
        };
    }
}
