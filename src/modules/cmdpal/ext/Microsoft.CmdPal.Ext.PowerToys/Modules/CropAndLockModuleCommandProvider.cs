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

internal sealed class CropAndLockModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var module = SettingsWindow.CropAndLock;
        var title = module.ModuleDisplayName();
        var icon = module.ModuleIcon();

        if (ModuleEnablementService.IsModuleEnabled(module))
        {
            yield return new ListItem(new CropAndLockReparentCommand())
            {
                Title = Resources.CropAndLock_Reparent_Title,
                Subtitle = Resources.CropAndLock_Reparent_Subtitle,
                Icon = icon,
            };

            yield return new ListItem(new CropAndLockThumbnailCommand())
            {
                Title = Resources.CropAndLock_Thumbnail_Title,
                Subtitle = Resources.CropAndLock_Thumbnail_Subtitle,
                Icon = icon,
            };
        }

        yield return new ListItem(new OpenInSettingsCommand(module, title))
        {
            Title = title,
            Subtitle = Resources.CropAndLock_Settings_Subtitle,
            Icon = icon,
        };
    }
}
