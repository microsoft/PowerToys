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

internal sealed class MouseWithoutBordersModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var title = SettingsWindow.MouseWithoutBorders.ModuleDisplayName();
        var icon = SettingsWindow.MouseWithoutBorders.ModuleIcon();
        var easyMouseIcon = new IconInfo("\uE962");
        var reconnectIcon = new IconInfo("\uE72C");

        yield return new ListItem(new OpenInSettingsCommand(SettingsWindow.MouseWithoutBorders, title) { Id = "com.microsoft.powertoys.mouseWithoutBorders.openSettings" })
        {
            Title = title,
            Subtitle = Resources.MouseWithoutBorders_Settings_Subtitle,
            Icon = icon,
        };

        yield return new ListItem(new ToggleMWBEasyMouseCommand())
        {
            Title = Resources.MouseWithoutBorders_ToggleEasyMouse_Title,
            Subtitle = Resources.MouseWithoutBorders_ToggleEasyMouse_Subtitle,
            Icon = easyMouseIcon,
        };

        yield return new ListItem(new MWBReconnectCommand())
        {
            Title = Resources.MouseWithoutBorders_Reconnect_Title,
            Subtitle = Resources.MouseWithoutBorders_Reconnect_Subtitle,
            Icon = reconnectIcon,
        };
    }
}
