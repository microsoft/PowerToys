// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Common.Drivers;
using PowerDisplay.Common.Models;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Common.Interfaces
{
    /// <summary>
    /// Surface that every monitor controller must implement: identity, discovery,
    /// disposal, and brightness control. Both DDC/CI (external monitors) and the
    /// Windows <c>WmiMonitorBrightness</c> path (internal panels) can deliver this.
    /// </summary>
    /// <remarks>
    /// <para><b>Brightness semantics:</b></para>
    /// <list type="bullet">
    /// <item><description><see cref="GetBrightnessAsync"/> returns a <see cref="VcpFeatureValue"/> with <b>raw VCP</b>
    ///   <c>Current</c> and <c>Maximum</c>. Most monitors report <c>Maximum</c>=100, but some (e.g. Samsung)
    ///   report 50 over DDC/CI. Callers must use <see cref="VcpFeatureValue.ToPercentage"/> to obtain a
    ///   0-100 percentage rather than treating <c>Current</c> as a percent.</description></item>
    /// <item><description><see cref="SetBrightnessAsync"/> takes a <b>percentage 0-100</b>. The DDC/CI implementation
    ///   scales it to the device's raw VCP range using the per-monitor max captured at discovery
    ///   (<c>Monitor.BrightnessVcpMax</c>); the WMI implementation passes 0-100 through to
    ///   <c>WmiMonitorBrightnessMethods.WmiSetBrightness</c> directly.</description></item>
    /// </list>
    /// </remarks>
    public interface IBasicMonitorController
    {
        /// <summary>
        /// Gets controller name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Reads the monitor's current brightness directly from hardware.
        /// </summary>
        Task<VcpFeatureValue> GetBrightnessAsync(Monitor monitor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the monitor brightness from a 0-100 percentage.
        /// </summary>
        Task<MonitorOperationResult> SetBrightnessAsync(Monitor monitor, int brightness, CancellationToken cancellationToken = default);

        /// <summary>
        /// Discovers supported monitors. Caller (MonitorManager) is responsible for
        /// passing a pre-filtered list of MonitorDisplayInfo entries that match this
        /// controller's scope (internal-only for WMI, external-only for DDC/CI).
        /// </summary>
        Task<IEnumerable<Monitor>> DiscoverMonitorsAsync(
            IReadOnlyList<MonitorDisplayInfo> targets,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases resources.
        /// </summary>
        void Dispose();
    }
}
