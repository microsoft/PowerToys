// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Core.Models;
using Monitor = PowerDisplay.Core.Models.Monitor;

namespace PowerDisplay.Core.Interfaces
{
    /// <summary>
    /// Monitor controller interface
    /// </summary>
    public interface IMonitorController
    {
        /// <summary>
        /// Controller name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Supported monitor type
        /// </summary>
        MonitorType SupportedType { get; }

        /// <summary>
        /// Checks whether the specified monitor can be controlled
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Whether the monitor can be controlled</returns>
        Task<bool> CanControlMonitorAsync(Monitor monitor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets monitor brightness
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Brightness information</returns>
        Task<BrightnessInfo> GetBrightnessAsync(Monitor monitor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets monitor brightness
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="brightness">Brightness value (0-100)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        Task<MonitorOperationResult> SetBrightnessAsync(Monitor monitor, int brightness, CancellationToken cancellationToken = default);

        /// <summary>
        /// Discovers supported monitors
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of monitors</returns>
        Task<IEnumerable<Monitor>> DiscoverMonitorsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates monitor connection status
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Whether the monitor is connected</returns>
        Task<bool> ValidateConnectionAsync(Monitor monitor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases resources
        /// </summary>
        void Dispose();
    }

    /// <summary>
    /// Extended monitor controller interface (supports additional features)
    /// </summary>
    public interface IExtendedMonitorController : IMonitorController
    {
        /// <summary>
        /// Gets monitor contrast
        /// </summary>
        Task<BrightnessInfo> GetContrastAsync(Monitor monitor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets monitor contrast
        /// </summary>
        Task<MonitorOperationResult> SetContrastAsync(Monitor monitor, int contrast, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets monitor volume
        /// </summary>
        Task<BrightnessInfo> GetVolumeAsync(Monitor monitor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets monitor volume
        /// </summary>
        Task<MonitorOperationResult> SetVolumeAsync(Monitor monitor, int volume, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets monitor capabilities string (DDC/CI)
        /// </summary>
        Task<string> GetCapabilitiesStringAsync(Monitor monitor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves current settings to monitor
        /// </summary>
        Task<MonitorOperationResult> SaveCurrentSettingsAsync(Monitor monitor, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Monitor manager interface
    /// </summary>
    public interface IMonitorManager
    {
        /// <summary>
        /// Currently detected monitors list
        /// </summary>
        IReadOnlyList<Monitor> Monitors { get; }

        /// <summary>
        /// Monitor list changed event
        /// </summary>
        event EventHandler<MonitorListChangedEventArgs>? MonitorsChanged;

        /// <summary>
        /// Monitor status changed event
        /// </summary>
        event EventHandler<MonitorStatusChangedEventArgs>? MonitorStatusChanged;

        /// <summary>
        /// Discovers all monitors
        /// </summary>
        Task<IReadOnlyList<Monitor>> DiscoverMonitorsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets brightness of specified monitor
        /// </summary>
        Task<BrightnessInfo> GetBrightnessAsync(string monitorId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets brightness of specified monitor
        /// </summary>
        Task<MonitorOperationResult> SetBrightnessAsync(string monitorId, int brightness, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets brightness of all monitors
        /// </summary>
        Task<IEnumerable<MonitorOperationResult>> SetAllBrightnessAsync(int brightness, CancellationToken cancellationToken = default);

        /// <summary>
        /// Refreshes monitor status
        /// </summary>
        Task RefreshMonitorStatusAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets monitor by ID
        /// </summary>
        Monitor? GetMonitor(string monitorId);
    }
}
