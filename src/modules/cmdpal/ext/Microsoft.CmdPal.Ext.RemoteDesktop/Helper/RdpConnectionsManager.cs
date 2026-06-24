// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Microsoft.CmdPal.Ext.RemoteDesktop.Commands;
using Microsoft.CmdPal.Ext.RemoteDesktop.Settings;
using Microsoft.Win32;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Helper;

internal class RdpConnectionsManager : IRdpConnectionsManager
{
    private readonly ISettingsInterface _settingsManager;
    private readonly ConnectionListItem _openRdpCommandListItem = new(string.Empty);

    private ReadOnlyCollection<ConnectionListItem> _connections = new(Array.Empty<ConnectionListItem>());

    private const int MinutesToCache = 1;
    private DateTime? _connectionsLastLoaded;

    public RdpConnectionsManager(ISettingsInterface settingsManager)
    {
        _settingsManager = settingsManager;
        _settingsManager.Settings.SettingsChanged += (s, e) =>
        {
            _connectionsLastLoaded = null;
        };
    }

    public IReadOnlyCollection<ConnectionListItem> Connections
    {
        get
        {
            if (!_connectionsLastLoaded.HasValue ||
           (DateTime.Now - _connectionsLastLoaded.Value).TotalMinutes >= MinutesToCache)
            {
                var registryConnections = GetRdpConnectionsFromRegistry();
                var predefinedConnections = GetPredefinedConnectionsFromSettings();
                _connectionsLastLoaded = DateTime.Now;

                var newConnections = new List<ConnectionListItem>(registryConnections.Count + predefinedConnections.Count + 1);
                newConnections.AddRange(registryConnections);
                newConnections.AddRange(predefinedConnections);
                newConnections.Insert(0, _openRdpCommandListItem);

                Interlocked.Exchange(ref _connections, new ReadOnlyCollection<ConnectionListItem>(newConnections));
            }

            return _connections;
        }
    }

    private List<ConnectionListItem> GetRdpConnectionsFromRegistry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Terminal Server Client\Default");

        var validConnections = new List<ConnectionListItem>();

        if (key is not null)
        {
            validConnections = key.GetValueNames()
                                        .Select(name => key.GetValue(name))
                                        .OfType<string>() // Keep only string values
                                        .Select(v => v.Trim()) // Normalize
                                        .Where(v => !string.IsNullOrWhiteSpace(v))
                                        .Distinct() // Remove dupes if any
                                        .Select(ConnectionHelpers.MapToResult)
                                        .ToList();
        }

        return validConnections;
    }

    private List<ConnectionListItem> GetPredefinedConnectionsFromSettings()
    {
        var validConnections = _settingsManager.PredefinedConnections
                                    .Select(s => s.Trim())
                                    .Where(value => !string.IsNullOrWhiteSpace(value))
                                    .Select(ConnectionHelpers.MapToResult)
                                    .ToList();

        return validConnections;
    }
}
