// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
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
                Title = "Crop and Lock (Reparent)",
                Subtitle = "Create a cropped reparented window",
                Icon = icon,
            };

            yield return new ListItem(new CropAndLockThumbnailCommand())
            {
                Title = "Crop and Lock (Thumbnail)",
                Subtitle = "Create a cropped thumbnail window",
                Icon = icon,
            };
        }

        yield return new ListItem(new OpenInSettingsCommand(module, title))
        {
            Title = title,
            Subtitle = "Open Crop and Lock settings",
            Icon = icon,
        };
    }
}
