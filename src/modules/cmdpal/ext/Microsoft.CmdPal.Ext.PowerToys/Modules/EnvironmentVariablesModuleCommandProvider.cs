// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using static Common.UI.SettingsDeepLink;

namespace PowerToysExtension.Modules;

internal sealed class EnvironmentVariablesModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var title = SettingsWindow.EnvironmentVariables.ModuleDisplayName();
        var icon = SettingsWindow.EnvironmentVariables.ModuleIcon();

        yield return new ListItem(new OpenEnvironmentVariablesCommand())
        {
            Title = "Open Environment Variables",
            Subtitle = "Launch Environment Variables editor",
            Icon = icon,
        };

        yield return new ListItem(new OpenEnvironmentVariablesAdminCommand())
        {
            Title = "Open Environment Variables (Admin)",
            Subtitle = "Launch Environment Variables editor as admin",
            Icon = icon,
        };

        yield return new ListItem(new OpenInSettingsCommand(SettingsWindow.EnvironmentVariables, title))
        {
            Title = title,
            Subtitle = "Open Environment Variables settings",
            Icon = icon,
        };
    }
}
