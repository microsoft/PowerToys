// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.RemoteDesktop.Commands;
using Microsoft.CmdPal.Ext.RemoteDesktop.Settings;
using Microsoft.Win32;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Helper;

internal class RDPConnectionsManager : IRDPConnectionManager
{
    private readonly ISettingsInterface _settingsManager;
    private readonly ConnectionListItem _openRdpCommandListItem = new(string.Empty);

    private List<ConnectionListItem> _connections = [];
    private List<string> _registryConnections = [];
    private List<string> _predefinedConnections = [];

    private const int DaysToCache = 1;
    private DateTime? _registryConnectionsLastLoaded;
    private DateTime? _predefinedConnectionsLastLoaded;

    public IReadOnlyCollection<ConnectionListItem> Connections => _connections.AsReadOnly();

    public RDPConnectionsManager(ISettingsInterface settingsManager)
    {
        _settingsManager = settingsManager;
        _settingsManager.Settings.SettingsChanged += (s, e) =>
        {
            _predefinedConnectionsLastLoaded = null;
            Reload();
        };

        Reload();
    }

    private void Reload()
    {
        _connections.Clear();

        if (!_registryConnectionsLastLoaded.HasValue ||
            (DateTime.Now - _registryConnectionsLastLoaded.Value).TotalDays >= DaysToCache)
        {
            // Load RDP connections from registry
            GetRdpConnectionsFromRegistry();
        }

        if (!_predefinedConnectionsLastLoaded.HasValue ||
          (DateTime.Now - _predefinedConnectionsLastLoaded.Value).TotalDays >= DaysToCache)
        {
            // Load predefined connections from settings
            GetPredefinedConnectionsFromSettings();
        }

        _connections = new List<ConnectionListItem>(_registryConnections.Count + _predefinedConnections.Count);
        _connections.AddRange(_registryConnections.Select(ConnectionHelpers.MapToResult));
        _connections.AddRange(_predefinedConnections.Select(ConnectionHelpers.MapToResult));
        _connections.Insert(0, _openRdpCommandListItem);
    }

    private void GetRdpConnectionsFromRegistry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Terminal Server Client\Default");

        if (key is null)
        {
            _registryConnections = [];
            _registryConnectionsLastLoaded = DateTime.Now;
            return;
        }

        var validConnections = key.GetValueNames()
                                    .Select(name => key.GetValue(name))
                                    .OfType<string>() // Keep only string values
                                    .Select(v => v.Trim()) // Normalize
                                    .Where(v => !string.IsNullOrWhiteSpace(v))
                                    .Distinct() // Remove dupes if any
                                    .ToList();

        _registryConnections = validConnections;
        _registryConnectionsLastLoaded = DateTime.Now;
    }

    private void GetPredefinedConnectionsFromSettings()
    {
        var validConnections = _settingsManager.PredefinedConnections
                                    .Select(s => s.Trim())
                                    .Where(value => !string.IsNullOrWhiteSpace(value))
                                    .ToList();

        _predefinedConnections = validConnections;
        _predefinedConnectionsLastLoaded = DateTime.Now;
    }
}
