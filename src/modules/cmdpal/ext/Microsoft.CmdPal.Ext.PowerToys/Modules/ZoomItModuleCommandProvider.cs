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

internal sealed class ZoomItModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var module = SettingsWindow.ZoomIt;
        var title = module.ModuleDisplayName();
        var icon = module.ModuleIcon();

        if (ModuleEnablementService.IsModuleEnabled(module))
        {
            // Action commands via ZoomIt IPC
            yield return new ListItem(new ZoomItActionCommand("zoom", Resources.ZoomIt_Zoom_Title) { Id = "com.microsoft.powertoys.zoomIt.zoom" })
            {
                Title = Resources.ZoomIt_Zoom_Title,
                Subtitle = Resources.ZoomIt_Zoom_Subtitle,
                Icon = icon,
            };
            yield return new ListItem(new ZoomItActionCommand("draw", Resources.ZoomIt_Draw_Title) { Id = "com.microsoft.powertoys.zoomIt.draw" })
            {
                Title = Resources.ZoomIt_Draw_Title,
                Subtitle = Resources.ZoomIt_Draw_Subtitle,
                Icon = icon,
            };
            yield return new ListItem(new ZoomItActionCommand("break", Resources.ZoomIt_Break_Title) { Id = "com.microsoft.powertoys.zoomIt.break" })
            {
                Title = Resources.ZoomIt_Break_Title,
                Subtitle = Resources.ZoomIt_Break_Subtitle,
                Icon = icon,
            };
            yield return new ListItem(new ZoomItActionCommand("liveZoom", Resources.ZoomIt_LiveZoom_Title) { Id = "com.microsoft.powertoys.zoomIt.liveZoom" })
            {
                Title = Resources.ZoomIt_LiveZoom_Title,
                Subtitle = Resources.ZoomIt_LiveZoom_Subtitle,
                Icon = icon,
            };
            yield return new ListItem(new ZoomItActionCommand("snip", Resources.ZoomIt_Snip_Title) { Id = "com.microsoft.powertoys.zoomIt.snip" })
            {
                Title = Resources.ZoomIt_Snip_Title,
                Subtitle = Resources.ZoomIt_Snip_Subtitle,
                Icon = icon,
            };
            yield return new ListItem(new ZoomItActionCommand("record", Resources.ZoomIt_Record_Title) { Id = "com.microsoft.powertoys.zoomIt.record" })
            {
                Title = Resources.ZoomIt_Record_Title,
                Subtitle = Resources.ZoomIt_Record_Subtitle,
                Icon = icon,
            };
        }

        yield return new ListItem(new OpenInSettingsCommand(module, title) { Id = "com.microsoft.powertoys.zoomIt.openSettings" })
        {
            Title = title,
            Subtitle = Resources.ZoomIt_Settings_Subtitle,
            Icon = icon,
        };
    }
}
