// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.RemoteDesktop.Commands;
using Microsoft.CmdPal.Ext.RemoteDesktop.Helper;
using Microsoft.CmdPal.Ext.RemoteDesktop.Settings;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.UnitTests;

/// <summary>
/// Lightweight test double for <see cref="RDPConnectionsManager"/> that avoids registry access
/// and gives tests deterministic control over the connection list.
/// </summary>
internal sealed class MockRDPConnectionsManager : RDPConnectionsManager
{
    private readonly List<ConnectionListItem> _connections = new();

    public MockRDPConnectionsManager(SettingsManager settingsManager)
        : base(settingsManager)
    {
        _connections.AddRange(settingsManager.PredefinedConnections.Select(RDPConnectionsManager.MapToResult));
    }
}
