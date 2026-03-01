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

internal sealed class HostsModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var module = SettingsWindow.Hosts;
        var title = module.ModuleDisplayName();
        var icon = module.ModuleIcon();

        if (ModuleEnablementService.IsModuleEnabled(module))
        {
            yield return new ListItem(new OpenHostsEditorCommand() { Id = "com.microsoft.powertoys.hosts.open" })
            {
                Title = Resources.Hosts_Open_Title,
                Subtitle = Resources.Hosts_Open_Subtitle,
                Icon = icon,
            };

            yield return new ListItem(new OpenHostsEditorAdminCommand() { Id = "com.microsoft.powertoys.hosts.openAdmin" })
            {
                Title = Resources.Hosts_OpenAdmin_Title,
                Subtitle = Resources.Hosts_OpenAdmin_Subtitle,
                Icon = icon,
            };
        }

        yield return new ListItem(new OpenInSettingsCommand(module, title) { Id = "com.microsoft.powertoys.hosts.openSettings" })
        {
            Title = title,
            Subtitle = Resources.Hosts_Settings_Subtitle,
            Icon = icon,
        };
    }
}
