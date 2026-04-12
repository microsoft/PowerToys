// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Service for enumerating and tracking connected display monitors.
/// </summary>
public interface IMonitorService
{
    /// <summary>
    /// Gets all currently connected monitors.
    /// </summary>
    IReadOnlyList<MonitorInfo> GetMonitors();

    /// <summary>
    /// Gets a specific monitor by its device identifier.
    /// </summary>
    MonitorInfo? GetMonitorByDeviceId(string deviceId);

    /// <summary>
    /// Gets the primary monitor.
    /// </summary>
    MonitorInfo? GetPrimaryMonitor();

    /// <summary>
    /// Raised when the set of connected monitors changes (connect, disconnect, or resolution change).
    /// </summary>
    event System.EventHandler? MonitorsChanged;
}
