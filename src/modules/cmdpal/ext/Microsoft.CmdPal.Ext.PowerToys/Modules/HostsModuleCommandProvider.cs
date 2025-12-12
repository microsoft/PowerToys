// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
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
            yield return new ListItem(new OpenHostsEditorCommand())
            {
                Title = "Open Hosts File Editor",
                Subtitle = "Launch Hosts File Editor",
                Icon = icon,
            };

            yield return new ListItem(new OpenHostsEditorAdminCommand())
            {
                Title = "Open Hosts File Editor (Admin)",
                Subtitle = "Launch Hosts File Editor as admin",
                Icon = icon,
            };
        }

        yield return new ListItem(new OpenInSettingsCommand(module, title))
        {
            Title = title,
            Subtitle = "Open Hosts File Editor settings",
            Icon = icon,
        };
    }
}
