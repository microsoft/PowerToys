// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using static Common.UI.SettingsDeepLink;

namespace PowerToysExtension.Modules;

internal sealed class MouseWithoutBordersModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var title = SettingsWindow.MouseWithoutBorders.ModuleDisplayName();
        var icon = SettingsWindow.MouseWithoutBorders.ModuleIcon();

        yield return new ListItem(new OpenInSettingsCommand(SettingsWindow.MouseWithoutBorders, title))
        {
            Title = title,
            Subtitle = "Open Mouse Without Borders settings",
            Icon = icon,
        };
    }
}
