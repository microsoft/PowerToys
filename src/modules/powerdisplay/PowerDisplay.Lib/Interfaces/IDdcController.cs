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
    /// Extended monitor features that ride on DDC/CI VCP codes: contrast, volume,
    /// color temperature, input source, power state.
    /// </summary>
    /// <remarks>
    /// Every method has a default implementation that returns an "unsupported"
    /// outcome — getters return <see cref="VcpFeatureValue.Invalid"/>, setters return
    /// <see cref="MonitorOperationResult.Failure(string, int)"/>. A concrete controller
    /// (e.g. <c>DdcCiController</c>) overrides what it actually supports; a controller
    /// that does not speak DDC/CI (e.g. <c>WmiController</c>) simply inherits the
    /// defaults and never overrides them. The runtime dispatch in <c>MonitorManager</c>
    /// therefore does not need an explicit capability check — calling
    /// <c>SetVolumeAsync</c> on a WMI controller resolves to the default and produces
    /// a benign failure result that the caller logs and ignores.
    ///
    /// <para><b>Continuous-range VCPs (Contrast 0x12, Volume 0x62):</b> <c>Get*Async</c> returns
    /// raw VCP <c>Current</c>/<c>Maximum</c>; use <see cref="VcpFeatureValue.ToPercentage"/> for 0-100.
    /// <c>Set*Async</c> takes a percentage 0-100; the implementation scales it to raw using the
    /// per-monitor max captured at discovery.</para>
    ///
    /// <para><b>Discrete VCPs (ColorTemperature 0x14, InputSource 0x60, PowerState 0xD6):</b> values
    /// are raw VCP enum codes (e.g. <c>0x05</c>=6500K, <c>0x11</c>=HDMI-1, <c>0x01</c>=Power On).
    /// No percentage semantics — pass values through verbatim.</para>
    /// </remarks>
    public interface IDdcController
    {
        /// <summary>
        /// Reads the monitor's current contrast directly from hardware (VCP 0x12).
        /// </summary>
        Task<VcpFeatureValue> GetContrastAsync(Monitor monitor, CancellationToken cancellationToken = default)
            => Task.FromResult(VcpFeatureValue.Invalid);

        /// <summary>
        /// Sets the monitor contrast (VCP 0x12) from a 0-100 percentage.
        /// </summary>
        Task<MonitorOperationResult> SetContrastAsync(Monitor monitor, int contrast, CancellationToken cancellationToken = default)
            => Task.FromResult(MonitorOperationResult.Failure("Contrast not supported by this controller"));

        /// <summary>
        /// Reads the monitor's current volume directly from hardware (VCP 0x62).
        /// </summary>
        Task<VcpFeatureValue> GetVolumeAsync(Monitor monitor, CancellationToken cancellationToken = default)
            => Task.FromResult(VcpFeatureValue.Invalid);

        /// <summary>
        /// Sets the monitor volume (VCP 0x62) from a 0-100 percentage.
        /// </summary>
        Task<MonitorOperationResult> SetVolumeAsync(Monitor monitor, int volume, CancellationToken cancellationToken = default)
            => Task.FromResult(MonitorOperationResult.Failure("Volume not supported by this controller"));

        /// <summary>
        /// Gets monitor color temperature using VCP 0x14 (Select Color Preset).
        /// </summary>
        /// <returns>Raw VCP preset value (e.g., 0x05 for 6500K), not Kelvin temperature.</returns>
        Task<VcpFeatureValue> GetColorTemperatureAsync(Monitor monitor, CancellationToken cancellationToken = default)
            => Task.FromResult(VcpFeatureValue.Invalid);

        /// <summary>
        /// Sets monitor color temperature using VCP 0x14 preset value.
        /// </summary>
        Task<MonitorOperationResult> SetColorTemperatureAsync(Monitor monitor, int colorTemperature, CancellationToken cancellationToken = default)
            => Task.FromResult(MonitorOperationResult.Failure("Color temperature not supported by this controller"));

        /// <summary>
        /// Gets current input source using VCP 0x60.
        /// </summary>
        /// <returns>Raw VCP input source value (e.g., 0x11 for HDMI-1).</returns>
        Task<VcpFeatureValue> GetInputSourceAsync(Monitor monitor, CancellationToken cancellationToken = default)
            => Task.FromResult(VcpFeatureValue.Invalid);

        /// <summary>
        /// Sets input source using VCP 0x60.
        /// </summary>
        Task<MonitorOperationResult> SetInputSourceAsync(Monitor monitor, int inputSource, CancellationToken cancellationToken = default)
            => Task.FromResult(MonitorOperationResult.Failure("Input source not supported by this controller"));

        /// <summary>
        /// Sets power state using VCP 0xD6 (Power Mode).
        /// Values: 0x01=On, 0x02=Standby, 0x03=Suspend, 0x04=Off(DPM), 0x05=Off(Hard).
        /// </summary>
        Task<MonitorOperationResult> SetPowerStateAsync(Monitor monitor, int powerState, CancellationToken cancellationToken = default)
            => Task.FromResult(MonitorOperationResult.Failure("Power state not supported by this controller"));

        /// <summary>
        /// Gets current power state using VCP 0xD6 (Power Mode).
        /// </summary>
        Task<VcpFeatureValue> GetPowerStateAsync(Monitor monitor, CancellationToken cancellationToken = default)
            => Task.FromResult(VcpFeatureValue.Invalid);
    }
}
