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

internal sealed class MouseUtilsModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var module = SettingsWindow.MouseUtils;
        var title = module.ModuleDisplayName();
        var icon = module.ModuleIcon();

        if (ModuleEnablementService.IsKeyEnabled("FindMyMouse"))
        {
            yield return new ListItem(new ToggleFindMyMouseCommand() { Id = "com.microsoft.powertoys.mouseUtils.findMyMouse" })
            {
                Title = Resources.MouseUtils_FindMyMouse_Title,
                Subtitle = Resources.MouseUtils_FindMyMouse_Subtitle,
                Icon = icon,
            };
        }

        if (ModuleEnablementService.IsKeyEnabled("MouseHighlighter"))
        {
            yield return new ListItem(new ToggleMouseHighlighterCommand() { Id = "com.microsoft.powertoys.mouseUtils.highlighter" })
            {
                Title = Resources.MouseUtils_Highlighter_Title,
                Subtitle = Resources.MouseUtils_Highlighter_Subtitle,
                Icon = icon,
            };
        }

        if (ModuleEnablementService.IsKeyEnabled("MousePointerCrosshairs"))
        {
            yield return new ListItem(new ToggleMouseCrosshairsCommand() { Id = "com.microsoft.powertoys.mouseUtils.crosshairs" })
            {
                Title = Resources.MouseUtils_Crosshairs_Title,
                Subtitle = Resources.MouseUtils_Crosshairs_Subtitle,
                Icon = icon,
            };
        }

        if (ModuleEnablementService.IsKeyEnabled("CursorWrap"))
        {
            yield return new ListItem(new ToggleCursorWrapCommand() { Id = "com.microsoft.powertoys.mouseUtils.cursorWrap" })
            {
                Title = Resources.MouseUtils_CursorWrap_Title,
                Subtitle = Resources.MouseUtils_CursorWrap_Subtitle,
                Icon = icon,
            };
        }

        if (ModuleEnablementService.IsKeyEnabled("MouseJump"))
        {
            yield return new ListItem(new ShowMouseJumpPreviewCommand() { Id = "com.microsoft.powertoys.mouseUtils.mouseJump" })
            {
                Title = Resources.MouseUtils_MouseJump_Title,
                Subtitle = Resources.MouseUtils_MouseJump_Subtitle,
                Icon = icon,
            };
        }

        yield return new ListItem(new OpenInSettingsCommand(module, title) { Id = "com.microsoft.powertoys.mouseUtils.openSettings" })
        {
            Title = title,
            Subtitle = Resources.MouseUtils_Settings_Subtitle,
            Icon = icon,
        };
    }
}
