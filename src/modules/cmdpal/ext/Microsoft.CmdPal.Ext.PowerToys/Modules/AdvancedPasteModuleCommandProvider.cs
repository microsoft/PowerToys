// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Common.UI;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using PowerToysExtension.Properties;

namespace PowerToysExtension.Modules;

internal sealed class AdvancedPasteModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var module = SettingsDeepLink.SettingsWindow.AdvancedPaste;
        var title = module.ModuleDisplayName();
        var icon = module.ModuleIcon();

        if (ModuleEnablementService.IsModuleEnabled(module))
        {
            yield return new ListItem(new OpenAdvancedPasteCommand())
            {
                Title = Resources.AdvancedPaste_Open_Title,
                Subtitle = Resources.AdvancedPaste_Open_Subtitle,
                Icon = icon,
            };
        }

        yield return new ListItem(new OpenInSettingsCommand(module, title))
        {
            Title = title,
            Subtitle = Resources.AdvancedPaste_Settings_Subtitle,
            Icon = icon,
        };
    }
}
