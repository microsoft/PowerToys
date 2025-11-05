// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.RemoteDesktop.Commands;
using Microsoft.CmdPal.Ext.RemoteDesktop.Helper;
using Microsoft.CmdPal.Ext.RemoteDesktop.Properties;
using Microsoft.CmdPal.Ext.RemoteDesktop.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Pages;

internal sealed partial class RemoteDesktopListPage : DynamicListPage
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ConnectionListItem _openRdpCommandListItem = new(string.Empty);

    public RemoteDesktopListPage(ServiceProvider serviceProvider)
    {
        Icon = Icons.RDPIcon;
        Name = Resources.remotedesktop_title;
        Id = "com.microsoft.cmdpal.builtin.remotedesktop";

        _serviceProvider = serviceProvider;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (string.Equals(oldSearch, newSearch, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        RaiseItemsChanged();
    }

    public override IListItem[] GetItems()
    {
        List<ConnectionListItem> items = new();

        var rdpConnectionManager = _serviceProvider.GetRequiredService<RDPConnectionsManager>();
        rdpConnectionManager.Reload();

        var connections = rdpConnectionManager.FindConnections(SearchText) ?? Enumerable.Empty<Scored<string>>();

        items.AddRange(connections.OrderBy(o => o.Score).Select(RDPConnectionsManager.MapToResult));

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            items.Insert(0, _openRdpCommandListItem);
        }

        return items.ToArray();
    }

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
