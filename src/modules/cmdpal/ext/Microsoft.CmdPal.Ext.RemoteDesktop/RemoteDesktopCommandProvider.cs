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
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.Ext.RemoteDesktop;

public partial class RemoteDesktopCommandProvider : CommandProvider
{
    private RemoteDesktopListPage listPage;
    private FallbackRemoteDesktopItem fallback;

    public RemoteDesktopCommandProvider()
    {
        Id = "com.microsoft.cmdpal.builtin.remotedesktop";
        DisplayName = Resources.remotedesktop_title;
        Icon = Icons.RDPIcon;

        ServiceCollection services = new();
        services.AddSingleton<ISettingsInterface, SettingsManager>();
        services.AddSingleton<IRDPConnectionManager, RDPConnectionsManager>();
        var serviceProvider = services.BuildServiceProvider();

        listPage = new RemoteDesktopListPage(serviceProvider);
        fallback = new FallbackRemoteDesktopItem(serviceProvider);
    }

    public override ICommandItem[] TopLevelCommands() => [listPage.ToCommandItem()];

    public override IFallbackCommandItem[] FallbackCommands() => [fallback];
}
