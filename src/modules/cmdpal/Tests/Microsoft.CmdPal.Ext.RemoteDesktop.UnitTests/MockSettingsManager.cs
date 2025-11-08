// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Reflection;
using Microsoft.CmdPal.Ext.RemoteDesktop.Settings;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.UnitTests;

/// <summary>
/// Test double for <see cref="SettingsManager"/> that bypasses disk I/O and
/// exposes helpers for manipulating predefined connections.
/// </summary>
internal sealed class MockSettingsManager : SettingsManager
{
    private const char TextBoxNewLine = '\r';
    private const string PredefinedConnectionsKey = "com.microsoft.cmdpal.builtin.remotedesktop.PredefinedConnections";

    public MockSettingsManager(params string[] predefinedConnections)
    {
        SetPredefinedConnections(predefinedConnections);
    }

    public override void LoadSettings()
    {
        // Intentionally bypass file system access during tests.
    }

    public override void SaveSettings()
    {
        // Intentionally bypass file system access during tests.
    }

    public void SetPredefinedConnections(IEnumerable<string> connections, bool raiseChangedEvent = false)
    {
        if (connections is null)
        {
            return;
        }

        if (Settings.TryGetSetting<string>(PredefinedConnectionsKey, out var setting) && setting is not null)
        {
            setting = string.Join(TextBoxNewLine, connections);
            if (raiseChangedEvent)
            {
                RaiseSettingsChanged();
            }
        }
    }

    public void RaiseSettingsChanged()
    {
        // Use the fully qualified type name to avoid ambiguity with namespace 'Settings'
        var settingsType = Settings.GetType();
        var raiseMethod = settingsType.GetMethod("RaiseSettingsChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        raiseMethod?.Invoke(Settings, null);
    }
}
