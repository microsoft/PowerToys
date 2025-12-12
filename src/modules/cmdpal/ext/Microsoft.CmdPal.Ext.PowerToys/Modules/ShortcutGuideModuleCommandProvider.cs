// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using static Common.UI.SettingsDeepLink;

namespace PowerToysExtension.Modules;

internal sealed class ShortcutGuideModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var module = SettingsWindow.ShortcutGuide;
        var title = module.ModuleDisplayName();
        var icon = module.ModuleIcon();

        if (ModuleEnablementService.IsModuleEnabled(module))
        {
            yield return new ListItem(new ToggleShortcutGuideCommand())
            {
                Title = "Toggle Shortcut Guide",
                Subtitle = "Show or hide Shortcut Guide",
                Icon = icon,
            };
        }

        yield return new ListItem(new OpenInSettingsCommand(module, title))
        {
            Title = title,
            Subtitle = "Open Shortcut Guide settings",
            Icon = icon,
        };
    }
}
