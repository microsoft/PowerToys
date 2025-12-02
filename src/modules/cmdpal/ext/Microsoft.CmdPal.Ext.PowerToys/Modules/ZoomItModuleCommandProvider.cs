// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using static Common.UI.SettingsDeepLink;

namespace PowerToysExtension.Modules;

internal sealed class ZoomItModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var title = SettingsWindow.ZoomIt.ModuleDisplayName();
        var icon = SettingsWindow.ZoomIt.ModuleIcon();

        // Action commands via ZoomIt IPC
        yield return new ListItem(new ZoomItActionCommand("zoom", "ZoomIt: Zoom"))
        {
            Title = "ZoomIt: Zoom",
            Subtitle = "Enter zoom mode",
            Icon = icon,
        };
        yield return new ListItem(new ZoomItActionCommand("draw", "ZoomIt: Draw"))
        {
            Title = "ZoomIt: Draw",
            Subtitle = "Enter drawing mode",
            Icon = icon,
        };
        yield return new ListItem(new ZoomItActionCommand("break", "ZoomIt: Break"))
        {
            Title = "ZoomIt: Break",
            Subtitle = "Enter break timer",
            Icon = icon,
        };
        yield return new ListItem(new ZoomItActionCommand("liveZoom", "ZoomIt: Live Zoom"))
        {
            Title = "ZoomIt: Live Zoom",
            Subtitle = "Toggle live zoom",
            Icon = icon,
        };
        yield return new ListItem(new ZoomItActionCommand("snip", "ZoomIt: Snip"))
        {
            Title = "ZoomIt: Snip",
            Subtitle = "Enter snip mode",
            Icon = icon,
        };
        yield return new ListItem(new ZoomItActionCommand("record", "ZoomIt: Record"))
        {
            Title = "ZoomIt: Record",
            Subtitle = "Start recording",
            Icon = icon,
        };

        yield return new ListItem(new OpenInSettingsCommand(SettingsWindow.ZoomIt, title))
        {
            Title = title,
            Subtitle = "Open ZoomIt settings",
            Icon = icon,
        };
    }
}
