// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using static Common.UI.SettingsDeepLink;

namespace PowerToysExtension.Modules;

internal sealed class MouseUtilsModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var title = SettingsWindow.MouseUtils.ModuleDisplayName();
        var icon = SettingsWindow.MouseUtils.ModuleIcon();

        yield return new ListItem(new ToggleFindMyMouseCommand())
        {
            Title = "Trigger Find My Mouse",
            Subtitle = "Focus the mouse pointer",
            Icon = icon,
        };

        yield return new ListItem(new ToggleMouseHighlighterCommand())
        {
            Title = "Toggle Mouse Highlighter",
            Subtitle = "Highlight mouse clicks",
            Icon = icon,
        };

        yield return new ListItem(new ToggleMouseCrosshairsCommand())
        {
            Title = "Toggle Mouse Crosshairs",
            Subtitle = "Enable or disable pointer crosshairs",
            Icon = icon,
        };

        yield return new ListItem(new ShowMouseJumpPreviewCommand())
        {
            Title = "Show Mouse Jump Preview",
            Subtitle = "Jump the pointer to a target",
            Icon = icon,
        };

        yield return new ListItem(new OpenInSettingsCommand(SettingsWindow.MouseUtils, title))
        {
            Title = title,
            Subtitle = "Open Mouse Utilities settings",
            Icon = icon,
        };
    }
}
