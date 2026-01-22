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
            yield return new ListItem(new ToggleFindMyMouseCommand())
            {
                Title = Resources.MouseUtils_FindMyMouse_Title,
                Subtitle = Resources.MouseUtils_FindMyMouse_Subtitle,
                Icon = icon,
            };
        }

        if (ModuleEnablementService.IsKeyEnabled("MouseHighlighter"))
        {
            yield return new ListItem(new ToggleMouseHighlighterCommand())
            {
                Title = Resources.MouseUtils_Highlighter_Title,
                Subtitle = Resources.MouseUtils_Highlighter_Subtitle,
                Icon = icon,
            };
        }

        if (ModuleEnablementService.IsKeyEnabled("MousePointerCrosshairs"))
        {
            yield return new ListItem(new ToggleMouseCrosshairsCommand())
            {
                Title = Resources.MouseUtils_Crosshairs_Title,
                Subtitle = Resources.MouseUtils_Crosshairs_Subtitle,
                Icon = icon,
            };
        }

        if (ModuleEnablementService.IsKeyEnabled("CursorWrap"))
        {
            yield return new ListItem(new ToggleCursorWrapCommand())
            {
                Title = Resources.MouseUtils_CursorWrap_Title,
                Subtitle = Resources.MouseUtils_CursorWrap_Subtitle,
                Icon = icon,
            };
        }

        if (ModuleEnablementService.IsKeyEnabled("MouseJump"))
        {
            yield return new ListItem(new ShowMouseJumpPreviewCommand())
            {
                Title = Resources.MouseUtils_MouseJump_Title,
                Subtitle = Resources.MouseUtils_MouseJump_Subtitle,
                Icon = icon,
            };
        }

        yield return new ListItem(new OpenInSettingsCommand(module, title))
        {
            Title = title,
            Subtitle = Resources.MouseUtils_Settings_Subtitle,
            Icon = icon,
        };
    }
}
