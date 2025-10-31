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

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Pages;

internal sealed partial class RemoteDesktopListPage : DynamicListPage
{
    private readonly SettingsManager _settingsManager;
    private readonly RDPConnectionsManager _rdpConnectionManager;
    private readonly ConnectionListItem _openRdpCommandListItem = new(string.Empty);

    private readonly List<ConnectionListItem> _items = new();

    public RemoteDesktopListPage(RDPConnectionsManager rdpConnectionsManager, ISettingsInterface settingsManager)
    {
        _settingsManager = (SettingsManager)settingsManager;
        _rdpConnectionManager = rdpConnectionsManager;

        Icon = Icons.RDPIcon;
        Name = Resources.remotedesktop_title;
        Id = "com.microsoft.cmdpal.builtin.remotedesktop";

        LoadConnections();
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (string.Equals(oldSearch, newSearch, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        UpdateConnections(newSearch);
        RaiseItemsChanged();
    }

    public override IListItem[] GetItems() => _items.ToArray();

    private void LoadConnections()
    {
        _items.Clear();
        var connections = _rdpConnectionManager.FindConnections(string.Empty) ?? Enumerable.Empty<Scored<string>>();

        _items.AddRange(connections.OrderBy(o => o.Score).Select(RDPConnectionsManager.MapToResult));

        _items.Add(_openRdpCommandListItem);
    }

    private void UpdateConnections(string query)
    {
        _items.Clear();

        _rdpConnectionManager.Reload();

        var connections = _rdpConnectionManager.FindConnections(query) ?? Enumerable.Empty<Scored<string>>();

        _items.AddRange(connections.OrderBy(o => o.Score).Select(RDPConnectionsManager.MapToResult));

        _items.Add(_openRdpCommandListItem);
    }

    public ICommandItem ToCommandItem()
    {
        return new CommandItem(this)
        {
            Subtitle = Resources.remotedesktop_subtitle,
            MoreCommands = [
                new CommandContextItem(_settingsManager.Settings.SettingsPage),
            ],
        };
    }
}
