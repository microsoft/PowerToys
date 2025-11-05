// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.RemoteDesktop.Commands;
using Microsoft.CmdPal.Ext.RemoteDesktop.Settings;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Win32;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Helper;

internal sealed class RDPConnectionsManager
{
    private readonly SettingsManager _settingsManager;
    private readonly List<string> _connections = [];

    public RDPConnectionsManager(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager;

        Reload();
    }

    public void Reload()
    {
        _connections.Clear();

        var rdpConnections = GetRdpConnectionsFromRegistry();

        var predefinedConnections = _settingsManager.PredefinedConnections;

        HashSet<string> newConnections = [.. rdpConnections];
        newConnections.UnionWith(predefinedConnections);

        _connections.AddRange(newConnections.ToArray());
    }

    private string[] GetRdpConnectionsFromRegistry()
    {
        var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Terminal Server Client\Default");
        if (key is null)
        {
            return [];
        }

        return [.. key.GetValueNames().Select(x => key.GetValue(x.Trim())?.ToString()).Where(value => value != null).Cast<string>()];
    }

    public static ConnectionListItem MapToResult(Scored<string> item) => new(item.Item);

    public IReadOnlyCollection<Scored<string>> FindConnections(string querySearch)
    {
        if (string.IsNullOrWhiteSpace(querySearch))
        {
            return _connections
                .Select(MapToScore)
                .ToList();
        }

        return _connections
            .Where(x => x.Contains(querySearch, StringComparison.InvariantCultureIgnoreCase))
            .Select(MapToScore)
            .ToList();
    }

    private Scored<string> MapToScore(string x, int i) => new() { Item = x, Score = _connections.Count + 1 - i };
}
