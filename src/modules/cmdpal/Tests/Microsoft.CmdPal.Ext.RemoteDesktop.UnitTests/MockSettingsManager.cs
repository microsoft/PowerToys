// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.RemoteDesktop.Settings;
using ToolkitSettings = Microsoft.CommandPalette.Extensions.Toolkit.Settings;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.UnitTests;

internal sealed class MockSettingsManager : ISettingsInterface
{
    private readonly List<string> _connections;

    public IReadOnlyCollection<string> PredefinedConnections => _connections;

    public ToolkitSettings Settings { get; } = new();

    public MockSettingsManager(params string[] predefinedConnections)
    {
        _connections = new(predefinedConnections);
    }
}
