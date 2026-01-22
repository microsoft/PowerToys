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

internal sealed class PowerRenameModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var title = SettingsWindow.PowerRename.ModuleDisplayName();
        var icon = SettingsWindow.PowerRename.ModuleIcon();

        yield return new ListItem(new OpenInSettingsCommand(SettingsWindow.PowerRename, title))
        {
            Title = title,
            Subtitle = Resources.PowerRename_Settings_Subtitle,
            Icon = icon,
        };
    }
}
