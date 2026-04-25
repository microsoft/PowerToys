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

internal sealed class FileExplorerAddonsModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var title = SettingsWindow.FileExplorer.ModuleDisplayName();
        var icon = SettingsWindow.FileExplorer.ModuleIcon();

        yield return new ListItem(new OpenInSettingsCommand(SettingsWindow.FileExplorer, title) { Id = "com.microsoft.powertoys.fileExplorerAddons.openSettings" })
        {
            Title = title,
            Subtitle = Resources.FileExplorerAddons_Settings_Subtitle,
            Icon = icon,
        };
    }
}
