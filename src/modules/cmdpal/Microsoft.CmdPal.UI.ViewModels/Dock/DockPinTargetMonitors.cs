// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

/// <summary>
/// Picks the monitors offered when pinning a command to the dock.
/// </summary>
public static class DockPinTargetMonitors
{
    /// <summary>
    /// Gets the monitors where the dock is currently enabled, so users can't pin a
    /// command to a display that has no dock visible. Each monitor's
    /// <see cref="MonitorInfo.DisplayName"/> is replaced with the name shown in dock
    /// settings, including any name the user gave it.
    /// </summary>
    public static IReadOnlyList<MonitorInfo>? Get(IReadOnlyList<MonitorInfo>? monitors, DockSettings dockSettings)
    {
        if (monitors is null)
        {
            return null;
        }

        var configs = dockSettings.MonitorConfigs;

        // When there are no per-monitor configs (legacy / first-run), the dock
        // is only shown on the primary monitor.
        if (configs.Count == 0)
        {
            return monitors
                .Where(m => m.IsPrimary)
                .Select(m => m with { DisplayName = DockMonitorDisplayName.Resolve(m, null) })
                .ToList();
        }

        return monitors
            .Select(m => (Monitor: m, Config: configs.Find(c =>
                string.Equals(c.MonitorDeviceId, m.StableId, StringComparison.OrdinalIgnoreCase) &&
                c.Enabled)))
            .Where(x => x.Config is not null)
            .Select(x => x.Monitor with { DisplayName = DockMonitorDisplayName.Resolve(x.Monitor, x.Config) })
            .ToList();
    }
}
