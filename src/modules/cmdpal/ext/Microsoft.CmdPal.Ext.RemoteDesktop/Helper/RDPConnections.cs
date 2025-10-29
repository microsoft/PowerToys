// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.RemoteDesktop.Commands;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Win32;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Helper;

internal sealed class RDPConnections
{
    private readonly List<string> _connections;

    private RDPConnections(IEnumerable<string> connections)
    {
        _connections = [.. connections];
    }

    public static RDPConnections Create(IEnumerable<string> connections) => new(connections);

    public static string[] GetRdpConnectionsFromRegistry()
    {
        var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Terminal Server Client\Default");
        if (key is null)
        {
            return [];
        }

        return [.. key.GetValueNames().Select(x => key.GetValue(x.Trim())?.ToString()).Where(value => value != null).Cast<string>()];
    }

    public static ConnectionListItem MapToResult(Scored<string> item) => new(item.Item);

    public IReadOnlyCollection<string> Connections => _connections;

    public void Reload(IReadOnlyCollection<string> rdpConnections)
    {
        var newConnections = rdpConnections.Where(x => !_connections.Contains(x)).ToList();
        var oldConnections = _connections.Where(x => !rdpConnections.Contains(x)).ToList();

        foreach (var oldConnection in oldConnections)
        {
            _connections.Remove(oldConnection);
        }

        _connections.AddRange(newConnections);
    }

    public void ConnectionWasSelected(string connection)
    {
        var index = _connections.IndexOf(connection);
        if (index == -1)
        {
            return;
        }

        _connections.RemoveAt(index);
        _connections.Insert(0, connection);
    }

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
