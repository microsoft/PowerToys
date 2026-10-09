// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.RemoteDesktop.Commands;
using Microsoft.CmdPal.Ext.RemoteDesktop.Helper;
using Microsoft.CmdPal.Ext.RemoteDesktop.Settings;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.UnitTests;

internal sealed class MockRdpConnectionsManager : IRdpConnectionsManager
{
    private readonly List<ConnectionListItem> _connections = new();

    public IReadOnlyCollection<ConnectionListItem> Connections => _connections.AsReadOnly();

    public MockRdpConnectionsManager(ISettingsInterface settingsManager)
    {
        _connections.AddRange(settingsManager.PredefinedConnections.Select(ConnectionHelpers.MapToResult));
    }
}
