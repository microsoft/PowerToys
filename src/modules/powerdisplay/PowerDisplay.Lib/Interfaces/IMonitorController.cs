// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Common.Models;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Common.Interfaces
{
    /// <summary>
    /// Monitor controller interface
    /// </summary>
    public interface IMonitorController
    {
        /// <summary>
        /// Gets controller name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets monitor brightness
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Brightness information</returns>
        Task<VcpFeatureValue> GetBrightnessAsync(Monitor monitor, CancellationToken cancellationToken = default);

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
        /// Sets monitor contrast
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="contrast">Contrast value (0-100)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        Task<MonitorOperationResult> SetContrastAsync(Monitor monitor, int contrast, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets monitor volume
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="volume">Volume value (0-100)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        Task<MonitorOperationResult> SetVolumeAsync(Monitor monitor, int volume, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets monitor color temperature using VCP 0x14 (Select Color Preset)
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>VCP preset value (e.g., 0x05 for 6500K), not Kelvin temperature</returns>
        Task<VcpFeatureValue> GetColorTemperatureAsync(Monitor monitor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets monitor color temperature using VCP 0x14 preset value
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="colorTemperature">VCP preset value (e.g., 0x05 for 6500K), not Kelvin temperature</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        Task<MonitorOperationResult> SetColorTemperatureAsync(Monitor monitor, int colorTemperature, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets current input source using VCP 0x60
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>VCP input source value (e.g., 0x11 for HDMI-1)</returns>
        Task<VcpFeatureValue> GetInputSourceAsync(Monitor monitor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets input source using VCP 0x60
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="inputSource">VCP input source value (e.g., 0x11 for HDMI-1)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        Task<MonitorOperationResult> SetInputSourceAsync(Monitor monitor, int inputSource, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets power state using VCP 0xD6 (Power Mode)
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="powerState">VCP power state value: 0x01=On, 0x02=Standby, 0x03=Suspend, 0x04=Off(DPM), 0x05=Off(Hard)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        Task<MonitorOperationResult> SetPowerStateAsync(Monitor monitor, int powerState, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets current power state using VCP 0xD6 (Power Mode)
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>VCP power state value: 0x01=On, 0x02=Standby, 0x03=Suspend, 0x04=Off(DPM), 0x05=Off(Hard)</returns>
        Task<VcpFeatureValue> GetPowerStateAsync(Monitor monitor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases resources
        /// </summary>
        void Dispose();
    }
}
