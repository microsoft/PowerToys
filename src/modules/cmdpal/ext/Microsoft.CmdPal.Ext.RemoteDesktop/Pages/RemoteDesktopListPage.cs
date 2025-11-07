// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.RemoteDesktop.Helper;
using Microsoft.CmdPal.Ext.RemoteDesktop.Properties;
using Microsoft.CmdPal.Ext.RemoteDesktop.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Pages;

internal sealed partial class RemoteDesktopListPage : ListPage
{
    private readonly ServiceProvider _serviceProvider;
    private readonly RDPConnectionsManager _rdpConnectionManager;

    public RemoteDesktopListPage(ServiceProvider serviceProvider)
    {
        Icon = Icons.RDPIcon;
        Name = Resources.remotedesktop_title;
        Id = "com.microsoft.cmdpal.builtin.remotedesktop";

        _serviceProvider = serviceProvider;
        _rdpConnectionManager = _serviceProvider.GetRequiredService<RDPConnectionsManager>();
    }

    public override IListItem[] GetItems() => _rdpConnectionManager.Connections.ToArray();

    public ICommandItem ToCommandItem()
    {
        var settingsManager = _serviceProvider.GetRequiredService<SettingsManager>();
        return new CommandItem(this)
        {
            Subtitle = Resources.remotedesktop_subtitle,
            MoreCommands = [
                new CommandContextItem(settingsManager.Settings.SettingsPage),
            ],
        };
    }
}
