// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Common.Drivers;
using PowerDisplay.Common.Models;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Common.Interfaces
{
    /// <summary>
    /// Monitor controller interface.
    /// </summary>
    /// <remarks>
    /// <para><b>Continuous-range VCPs (Brightness 0x10, Contrast 0x12, Volume 0x62)</b>:</para>
    /// <list type="bullet">
    /// <item><description><c>Get*Async</c> returns a <see cref="VcpFeatureValue"/> with <b>raw VCP</b> <c>Current</c> and <c>Maximum</c>.
    ///   Most monitors report <c>Maximum</c>=100, but some (e.g. Samsung) report 50. Callers must use
    ///   <see cref="VcpFeatureValue.ToPercentage"/> to obtain a 0-100 percentage rather than treating <c>Current</c> as a percent.</description></item>
    /// <item><description><c>Set*Async</c> takes a <b>percentage 0-100</b>. The implementation scales it to the device's
    ///   raw VCP range using the per-monitor max captured at discovery (<c>Monitor.BrightnessVcpMax</c> etc.).</description></item>
    /// </list>
    /// <para><b>Discrete VCPs (ColorTemperature 0x14, InputSource 0x60, PowerState 0xD6)</b>: values are raw VCP enum codes
    /// (e.g. <c>0x05</c> = 6500K, <c>0x11</c> = HDMI-1, <c>0x01</c> = Power On). No percentage semantics — pass values through verbatim.</para>
    /// </remarks>
    public interface IMonitorController
    {
        /// <summary>
        /// Gets controller name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Reads the monitor's current brightness directly from hardware (VCP 0x10).
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>
        /// <see cref="VcpFeatureValue"/> with raw VCP <c>Current</c> and <c>Maximum</c>.
        /// Call <see cref="VcpFeatureValue.ToPercentage"/> to obtain a 0-100 percent value.
        /// </returns>
        Task<VcpFeatureValue> GetBrightnessAsync(Monitor monitor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the monitor brightness (VCP 0x10) from a percentage. The implementation scales
        /// 0-100 to the device's raw VCP range before issuing the DDC/CI write.
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="brightness">Brightness percentage (0-100)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        Task<MonitorOperationResult> SetBrightnessAsync(Monitor monitor, int brightness, CancellationToken cancellationToken = default);

        /// <summary>
        /// Discovers supported monitors. Caller (MonitorManager) is responsible for
        /// passing a pre-filtered list of MonitorDisplayInfo entries that match this
        /// controller's scope (internal-only for WMI, external-only for DDC/CI).
        /// </summary>
        /// <param name="targets">Pre-filtered display targets to consider.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of monitors</returns>
        Task<IEnumerable<Monitor>> DiscoverMonitorsAsync(
            IReadOnlyList<MonitorDisplayInfo> targets,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the monitor's current contrast directly from hardware (VCP 0x12).
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>
        /// <see cref="VcpFeatureValue"/> with raw VCP <c>Current</c> and <c>Maximum</c>.
        /// Call <see cref="VcpFeatureValue.ToPercentage"/> to obtain a 0-100 percent value.
        /// </returns>
        Task<VcpFeatureValue> GetContrastAsync(Monitor monitor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the monitor contrast (VCP 0x12) from a percentage. The implementation scales
        /// 0-100 to the device's raw VCP range before issuing the DDC/CI write.
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="contrast">Contrast percentage (0-100)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        Task<MonitorOperationResult> SetContrastAsync(Monitor monitor, int contrast, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the monitor's current volume directly from hardware (VCP 0x62).
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>
        /// <see cref="VcpFeatureValue"/> with raw VCP <c>Current</c> and <c>Maximum</c>.
        /// Call <see cref="VcpFeatureValue.ToPercentage"/> to obtain a 0-100 percent value.
        /// </returns>
        Task<VcpFeatureValue> GetVolumeAsync(Monitor monitor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the monitor volume (VCP 0x62) from a percentage. The implementation scales
        /// 0-100 to the device's raw VCP range before issuing the DDC/CI write.
        /// </summary>
        /// <param name="monitor">Monitor object</param>
        /// <param name="volume">Volume percentage (0-100)</param>
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
