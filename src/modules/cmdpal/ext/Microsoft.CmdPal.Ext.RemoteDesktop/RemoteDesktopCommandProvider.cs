// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.RemoteDesktop.Commands;
using Microsoft.CmdPal.Ext.RemoteDesktop.Helper;
using Microsoft.CmdPal.Ext.RemoteDesktop.Pages;
using Microsoft.CmdPal.Ext.RemoteDesktop.Properties;
using Microsoft.CmdPal.Ext.RemoteDesktop.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RemoteDesktop;

public partial class RemoteDesktopCommandProvider : CommandProvider
{
    private readonly CommandItem listPageCommand;
    private readonly FallbackRemoteDesktopItem fallback;

    public RemoteDesktopCommandProvider()
    {
        Id = "com.microsoft.cmdpal.builtin.remotedesktop";
        DisplayName = Resources.remotedesktop_title;
        Icon = Icons.RDPIcon;

        var settingsManager = new SettingsManager();
        var rdpConnectionsManager = new RdpConnectionsManager(settingsManager);
        var listPage = new RemoteDesktopListPage(rdpConnectionsManager);

        fallback = new FallbackRemoteDesktopItem(rdpConnectionsManager);

        listPageCommand = new CommandItem(listPage)
        {
            Subtitle = Resources.remotedesktop_subtitle,
            Icon = Icons.RDPIcon,
            MoreCommands = [
                new CommandContextItem(settingsManager.Settings.SettingsPage),
            ],
        };
    }

    public override ICommandItem[] TopLevelCommands() => [listPageCommand];

    public override IFallbackCommandItem[] FallbackCommands() => [fallback];
}
