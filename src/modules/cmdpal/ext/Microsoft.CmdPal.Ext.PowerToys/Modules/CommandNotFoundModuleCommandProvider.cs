// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using static Common.UI.SettingsDeepLink;

namespace PowerToysExtension.Modules;

internal sealed class CommandNotFoundModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var title = SettingsWindow.CmdNotFound.ModuleDisplayName();
        var icon = SettingsWindow.CmdNotFound.ModuleIcon();

        yield return new ListItem(new OpenInSettingsCommand(SettingsWindow.CmdNotFound, title))
        {
            Title = title,
            Subtitle = "Open Command Not Found settings",
            Icon = icon,
        };
    }
}
