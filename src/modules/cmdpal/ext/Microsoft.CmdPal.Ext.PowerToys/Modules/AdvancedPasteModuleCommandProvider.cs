// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Common.UI;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Modules;

internal sealed class AdvancedPasteModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var title = SettingsDeepLink.SettingsWindow.AdvancedPaste.ModuleDisplayName();
        var icon = SettingsDeepLink.SettingsWindow.AdvancedPaste.ModuleIcon();

        yield return new ListItem(new OpenAdvancedPasteCommand())
        {
            Title = "Open Advanced Paste",
            Subtitle = "Launch the Advanced Paste UI",
            Icon = icon,
        };

        yield return new ListItem(new OpenInSettingsCommand(SettingsDeepLink.SettingsWindow.AdvancedPaste, title))
        {
            Title = title,
            Subtitle = "Open Advanced Paste settings",
            Icon = icon,
        };
    }
}
