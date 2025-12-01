// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using static Common.UI.SettingsDeepLink;

namespace PowerToysExtension.Modules;

internal sealed class ScreenRulerModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var title = SettingsWindow.MeasureTool.ModuleDisplayName();
        var icon = SettingsWindow.MeasureTool.ModuleIcon();

        yield return new ListItem(new ToggleScreenRulerCommand())
        {
            Title = "Toggle Screen Ruler",
            Subtitle = "Start or close Screen Ruler",
            Icon = icon,
        };

        yield return new ListItem(new OpenInSettingsCommand(SettingsWindow.MeasureTool, title))
        {
            Title = title,
            Subtitle = "Open Screen Ruler settings",
            Icon = icon,
        };
    }
}
