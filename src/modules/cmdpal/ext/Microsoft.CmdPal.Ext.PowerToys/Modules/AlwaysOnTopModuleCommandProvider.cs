// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using static Common.UI.SettingsDeepLink;

namespace PowerToysExtension.Modules;

internal sealed class AlwaysOnTopModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var title = SettingsWindow.AlwaysOnTop.ModuleDisplayName();
        var icon = SettingsWindow.AlwaysOnTop.ModuleIcon();

        yield return new ListItem(new OpenInSettingsCommand(SettingsWindow.AlwaysOnTop, title))
        {
            Title = title,
            Subtitle = "Open Always On Top settings",
            Icon = icon,
        };
    }
}
