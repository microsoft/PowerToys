// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Common.Models;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Common.Interfaces
{
    /// <summary>
    /// Extended monitor controller contract for DDC/CI-only features (everything beyond brightness).
    /// Only monitors reachable over DDC/CI (external displays) expose these; the WMI brightness
    /// path used for internal panels does not, so <see cref="WMI"/>-style controllers should
    /// implement only <see cref="IMonitorController"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Continuous-range VCPs (Contrast 0x12, Volume 0x62):</b></para>
    /// <list type="bullet">
    /// <item><description><c>Get*Async</c> returns a <see cref="VcpFeatureValue"/> with <b>raw VCP</b>
    ///   <c>Current</c> and <c>Maximum</c>. Use <see cref="VcpFeatureValue.ToPercentage"/> for 0-100.</description></item>
    /// <item><description><c>Set*Async</c> takes a <b>percentage 0-100</b>; the implementation scales it
    ///   to the device's raw range using the per-monitor max captured at discovery.</description></item>
    /// </list>
    /// <para><b>Discrete VCPs (ColorTemperature 0x14, InputSource 0x60, PowerState 0xD6):</b> values are raw
    /// VCP enum codes (e.g. <c>0x05</c>=6500K, <c>0x11</c>=HDMI-1, <c>0x01</c>=Power On). No percentage
    /// semantics — pass values through verbatim.</para>
    /// </remarks>
    public interface IDdcController : IMonitorController
    {
        /// <summary>
        /// Reads the monitor's current contrast directly from hardware (VCP 0x12).
        /// </summary>
        Task<VcpFeatureValue> GetContrastAsync(Monitor monitor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the monitor contrast (VCP 0x12) from a 0-100 percentage.
        /// </summary>
        Task<MonitorOperationResult> SetContrastAsync(Monitor monitor, int contrast, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the monitor's current volume directly from hardware (VCP 0x62).
        /// </summary>
        Task<VcpFeatureValue> GetVolumeAsync(Monitor monitor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the monitor volume (VCP 0x62) from a 0-100 percentage.
        /// </summary>
        Task<MonitorOperationResult> SetVolumeAsync(Monitor monitor, int volume, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets monitor color temperature using VCP 0x14 (Select Color Preset).
        /// </summary>
        /// <returns>Raw VCP preset value (e.g., 0x05 for 6500K), not Kelvin temperature.</returns>
        Task<VcpFeatureValue> GetColorTemperatureAsync(Monitor monitor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets monitor color temperature using VCP 0x14 preset value.
        /// </summary>
        Task<MonitorOperationResult> SetColorTemperatureAsync(Monitor monitor, int colorTemperature, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets current input source using VCP 0x60.
        /// </summary>
        /// <returns>Raw VCP input source value (e.g., 0x11 for HDMI-1).</returns>
        Task<VcpFeatureValue> GetInputSourceAsync(Monitor monitor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets input source using VCP 0x60.
        /// </summary>
        Task<MonitorOperationResult> SetInputSourceAsync(Monitor monitor, int inputSource, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets power state using VCP 0xD6 (Power Mode).
        /// Values: 0x01=On, 0x02=Standby, 0x03=Suspend, 0x04=Off(DPM), 0x05=Off(Hard).
        /// </summary>
        Task<MonitorOperationResult> SetPowerStateAsync(Monitor monitor, int powerState, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets current power state using VCP 0xD6 (Power Mode).
        /// </summary>
        Task<VcpFeatureValue> GetPowerStateAsync(Monitor monitor, CancellationToken cancellationToken = default);
    }
}
