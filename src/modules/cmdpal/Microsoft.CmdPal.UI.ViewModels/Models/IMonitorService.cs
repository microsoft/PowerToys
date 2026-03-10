// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Service for enumerating and tracking display monitors.
/// Implemented in the UI layer with Win32 APIs; consumed by ViewModels.
/// </summary>
public interface IMonitorService
{
    /// <summary>
    /// Gets a snapshot of all currently connected monitors.
    /// </summary>
    IReadOnlyList<MonitorInfo> GetMonitors();

    /// <summary>
    /// Gets the monitor matching the given device ID, or null if not found.
    /// </summary>
    MonitorInfo? GetMonitorByDeviceId(string deviceId);

    /// <summary>
    /// Gets the primary monitor. Always returns a value when at least one display is connected.
    /// </summary>
    MonitorInfo GetPrimaryMonitor();

    /// <summary>
    /// Raised when the set of connected monitors changes (connect, disconnect,
    /// resolution change, DPI change). Listeners should re-query <see cref="GetMonitors"/>.
    /// </summary>
    event Action? MonitorsChanged;
}
