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
        /// Gets monitor contrast
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Contrast information</returns>
        Task<BrightnessInfo> GetContrastAsync(Monitor monitor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets monitor contrast
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="contrast">Contrast value (0-100)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        Task<MonitorOperationResult> SetContrastAsync(Monitor monitor, int contrast, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets monitor volume
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Volume information</returns>
        Task<BrightnessInfo> GetVolumeAsync(Monitor monitor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets monitor volume
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="volume">Volume value (0-100)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        Task<MonitorOperationResult> SetVolumeAsync(Monitor monitor, int volume, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets monitor color temperature
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Color temperature information</returns>
        Task<BrightnessInfo> GetColorTemperatureAsync(Monitor monitor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets monitor color temperature
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="colorTemperature">Color temperature value (2000-10000K)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        Task<MonitorOperationResult> SetColorTemperatureAsync(Monitor monitor, int colorTemperature, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets monitor capabilities string (DDC/CI)
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Capabilities string</returns>
        Task<string> GetCapabilitiesStringAsync(Monitor monitor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves current settings to monitor
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        Task<MonitorOperationResult> SaveCurrentSettingsAsync(Monitor monitor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases resources
        /// </summary>
        void Dispose();
    }

    // IMonitorManager interface removed - YAGNI principle
    // Only one implementation exists (MonitorManager), so interface abstraction is unnecessary
    // This simplifies the codebase and eliminates maintenance overhead
}
