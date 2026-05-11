// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using PowerDisplay.Common.Interfaces;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;
using static PowerDisplay.Common.Drivers.NativeConstants;
using static PowerDisplay.Common.Drivers.NativeDelegates;
using static PowerDisplay.Common.Drivers.PInvoke;
using Monitor = PowerDisplay.Common.Models.Monitor;

// Type aliases matching Windows API naming conventions for better readability when working with native structures.
// These uppercase aliases are used consistently throughout this file to match Win32 API documentation.
using MONITORINFOEX = PowerDisplay.Common.Drivers.MonitorInfoEx;
using PHYSICAL_MONITOR = PowerDisplay.Common.Drivers.PhysicalMonitor;

namespace PowerDisplay.Common.Drivers.DDC
{
    /// <summary>
    /// DDC/CI monitor controller for controlling external monitors
    /// </summary>
    public partial class DdcCiController : IMonitorController, IDisposable
    {
        private readonly PhysicalMonitorHandleManager _handleManager = new();
        private readonly MonitorDiscoveryHelper _discoveryHelper;

        private bool _disposed;

        public DdcCiController()
        {
            _discoveryHelper = new MonitorDiscoveryHelper();
        }

        public string Name => "DDC/CI Monitor Controller";

        /// <inheritdoc />
        public async Task<VcpFeatureValue> GetBrightnessAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);
            return await GetVcpFeatureAsync(monitor, VcpCodeBrightness, cancellationToken);
        }

        /// <inheritdoc />
        public Task<MonitorOperationResult> SetBrightnessAsync(Monitor monitor, int brightness, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);
            var raw = VcpFeatureValue.FromPercentage(brightness, monitor.BrightnessVcpMax);
            return SetVcpFeatureAsync(monitor, NativeConstants.VcpCodeBrightness, raw, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<VcpFeatureValue> GetContrastAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);
            return await GetVcpFeatureAsync(monitor, VcpCodeContrast, cancellationToken);
        }

        /// <inheritdoc />
        public Task<MonitorOperationResult> SetContrastAsync(Monitor monitor, int contrast, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);
            var raw = VcpFeatureValue.FromPercentage(contrast, monitor.ContrastVcpMax);
            return SetVcpFeatureAsync(monitor, NativeConstants.VcpCodeContrast, raw, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<VcpFeatureValue> GetVolumeAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);
            return await GetVcpFeatureAsync(monitor, VcpCodeVolume, cancellationToken);
        }

        /// <inheritdoc />
        public Task<MonitorOperationResult> SetVolumeAsync(Monitor monitor, int volume, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);
            var raw = VcpFeatureValue.FromPercentage(volume, monitor.VolumeVcpMax);
            return SetVcpFeatureAsync(monitor, NativeConstants.VcpCodeVolume, raw, cancellationToken);
        }

        /// <summary>
        /// Get monitor color temperature using VCP code 0x14 (Select Color Preset)
        /// Returns the raw VCP preset value (e.g., 0x05 for 6500K), not Kelvin temperature
        /// </summary>
        public async Task<VcpFeatureValue> GetColorTemperatureAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);
            return await GetVcpFeatureAsync(monitor, VcpCodeSelectColorPreset, cancellationToken);
        }

        /// <summary>
        /// Set monitor color temperature using VCP code 0x14 (Select Color Preset)
        /// </summary>
        public Task<MonitorOperationResult> SetColorTemperatureAsync(Monitor monitor, int colorTemperature, CancellationToken cancellationToken = default)
            => SetVcpFeatureAsync(monitor, VcpCodeSelectColorPreset, colorTemperature, cancellationToken);

        /// <summary>
        /// Get current input source using VCP code 0x60
        /// Returns the raw VCP value (e.g., 0x11 for HDMI-1)
        /// </summary>
        public async Task<VcpFeatureValue> GetInputSourceAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);
            return await GetVcpFeatureAsync(monitor, VcpCodeInputSource, cancellationToken);
        }

        /// <summary>
        /// Set input source using VCP code 0x60
        /// </summary>
        public Task<MonitorOperationResult> SetInputSourceAsync(Monitor monitor, int inputSource, CancellationToken cancellationToken = default)
            => SetVcpFeatureAsync(monitor, VcpCodeInputSource, inputSource, cancellationToken);

        /// <summary>
        /// Set power state using VCP code 0xD6 (Power Mode).
        /// Values: 0x01=On, 0x02=Standby, 0x03=Suspend, 0x04=Off(DPM), 0x05=Off(Hard).
        /// Note: Setting any value other than 0x01 (On) will turn off the display.
        /// </summary>
        public Task<MonitorOperationResult> SetPowerStateAsync(Monitor monitor, int powerState, CancellationToken cancellationToken = default)
            => SetVcpFeatureAsync(monitor, VcpCodePowerMode, powerState, cancellationToken);

        /// <summary>
        /// Get current power state using VCP code 0xD6 (Power Mode).
        /// Returns the raw VCP value (0x01=On, 0x02=Standby, etc.)
        /// </summary>
        public async Task<VcpFeatureValue> GetPowerStateAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);
            return await GetVcpFeatureAsync(monitor, VcpCodePowerMode, cancellationToken);
        }

        /// <summary>
        /// Discovers external DDC/CI-managed monitors. Each enumerated hMonitor runs its own
        /// async pipeline (filter → physical-handle retrieval → caps fetch + VCP init); all
        /// pipelines run concurrently via Task.WhenAll. Caller (MonitorManager) supplies the
        /// pre-filtered external-target list from Phase 0.
        /// </summary>
        /// <param name="targets">External-only display targets (pre-filtered by MonitorManager Phase 0).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of DDC/CI-managed external monitors.</returns>
        public async Task<IEnumerable<Monitor>> DiscoverMonitorsAsync(
            IReadOnlyList<MonitorDisplayInfo> targets,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            var handles = EnumerateMonitorHandles();
            var targetsByGdi = BuildGdiLookup(targets);
            Logger.LogInfo(
                $"DDC: Discovery start — {handles.Count} candidate handles, {targets.Count} external targets");

            if (handles.Count == 0)
            {
                Logger.LogInfo($"DDC: Discovery complete in {stopwatch.ElapsedMilliseconds}ms — 0 monitors (no handles)");
                return Enumerable.Empty<Monitor>();
            }

            var pipelines = handles
                .Select(h => DiscoverFromHandleAsync(h, targetsByGdi, cancellationToken))
                .ToList();
            var results = await Task.WhenAll(pipelines);

            var monitors = results.SelectMany(r => r).ToList();
            _handleManager.UpdateHandleMap(monitors.ToDictionary(m => m.Id, m => m.Handle));

            Logger.LogInfo(
                $"DDC: Discovery complete in {stopwatch.ElapsedMilliseconds}ms — " +
                $"{monitors.Count}/{handles.Count} monitors");
            return monitors;
        }

        /// <summary>
        /// Enumerate all logical monitor handles using Win32 API.
        /// </summary>
        private List<IntPtr> EnumerateMonitorHandles()
        {
            var handles = new List<IntPtr>();

            bool EnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData)
            {
                handles.Add(hMonitor);
                return true;
            }

            if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, EnumProc, IntPtr.Zero))
            {
                Logger.LogWarning("DDC: EnumDisplayMonitors failed");
            }

            return handles;
        }

        /// <summary>
        /// Group external targets by GDI device name (case-insensitive) into a lookup keyed by name.
        /// Mirror mode can have multiple targets share one GDI source — hence the value is a List.
        /// </summary>
        private static Dictionary<string, List<MonitorDisplayInfo>> BuildGdiLookup(
            IReadOnlyList<MonitorDisplayInfo> externalTargets)
        {
            return externalTargets
                .Where(t => !string.IsNullOrEmpty(t.GdiDeviceName))
                .GroupBy(t => t.GdiDeviceName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Get GDI device name for a monitor handle (e.g., "\\.\DISPLAY1").
        /// </summary>
        private unsafe string? GetGdiDeviceName(IntPtr hMonitor)
        {
            var monitorInfo = new MONITORINFOEX { CbSize = (uint)sizeof(MONITORINFOEX) };
            if (GetMonitorInfo(hMonitor, &monitorInfo))
            {
                return monitorInfo.GetDeviceName();
            }

            return null;
        }

        /// <summary>
        /// Full per-physical-monitor pipeline: fetch DDC/CI capabilities (slow I2C, ~4 s),
        /// construct the Monitor object, and read the supported VCP feature values.
        /// Returns null if capabilities are invalid, the Monitor can't be constructed, or
        /// any exception occurs (logged at Error level with the device path).
        /// </summary>
        /// <remarks>
        /// Pure synchronous work — callers wrap this in <see cref="Task.Run"/> to dispatch
        /// to the threadpool. Within a single physical monitor the VCP reads serialize on
        /// one I2C bus; parallelism across physical monitors happens at the caller.
        /// </remarks>
        private Monitor? BuildMonitorFromPhysical(PHYSICAL_MONITOR physical, MonitorDisplayInfo info)
        {
            try
            {
                var capResult = DdcCiNative.FetchCapabilities(physical.HPhysicalMonitor);
                if (!capResult.IsValid)
                {
                    return null;
                }

                var monitor = _discoveryHelper.CreateMonitorFromPhysical(physical, info);
                if (monitor == null)
                {
                    return null;
                }

                if (!string.IsNullOrEmpty(capResult.CapabilitiesString))
                {
                    monitor.CapabilitiesRaw = capResult.CapabilitiesString;
                }

                if (capResult.VcpCapabilitiesInfo != null)
                {
                    monitor.VcpCapabilitiesInfo = capResult.VcpCapabilitiesInfo;
                    UpdateMonitorCapabilitiesFromVcp(monitor, capResult.VcpCapabilitiesInfo);

                    if (monitor.SupportsInputSource)
                    {
                        InitializeInputSource(monitor, physical.HPhysicalMonitor);
                    }

                    if (monitor.SupportsColorTemperature)
                    {
                        InitializeColorTemperature(monitor, physical.HPhysicalMonitor);
                    }

                    if (monitor.SupportsPowerState)
                    {
                        InitializePowerState(monitor, physical.HPhysicalMonitor);
                    }

                    if (monitor.SupportsContrast)
                    {
                        InitializeContrast(monitor, physical.HPhysicalMonitor);
                    }

                    if (monitor.SupportsVolume)
                    {
                        InitializeVolume(monitor, physical.HPhysicalMonitor);
                    }
                }

                if (monitor.SupportsBrightness)
                {
                    InitializeBrightness(monitor, physical.HPhysicalMonitor);
                }

                return monitor;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Logger.LogError(
                    $"DDC: [DevicePath={info.DevicePath}] BuildMonitorFromPhysical exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Initialize input source value for a monitor using VCP 0x60.
        /// </summary>
        private static void InitializeInputSource(Monitor monitor, IntPtr handle)
        {
            if (TryGetVcpFeature(handle, VcpCodeInputSource, monitor.Id, out uint current, out uint _))
            {
                monitor.CurrentInputSource = (int)current;
            }
        }

        /// <summary>
        /// Initialize color temperature value for a monitor using VCP 0x14.
        /// </summary>
        private static void InitializeColorTemperature(Monitor monitor, IntPtr handle)
        {
            if (TryGetVcpFeature(handle, VcpCodeSelectColorPreset, monitor.Id, out uint current, out uint _))
            {
                monitor.CurrentColorTemperature = (int)current;
            }
        }

        /// <summary>
        /// Initialize power state value for a monitor using VCP 0xD6.
        /// </summary>
        private static void InitializePowerState(Monitor monitor, IntPtr handle)
        {
            if (TryGetVcpFeature(handle, VcpCodePowerMode, monitor.Id, out uint current, out uint _))
            {
                monitor.CurrentPowerState = (int)current;
            }
        }

        /// <summary>
        /// Initialize brightness value for a monitor using VCP 0x10.
        /// Persists the device-reported raw maximum so subsequent writes can scale percent → raw.
        /// </summary>
        private static void InitializeBrightness(Monitor monitor, IntPtr handle)
        {
            if (TryGetVcpFeature(handle, VcpCodeBrightness, monitor.Id, out uint current, out uint max))
            {
                monitor.BrightnessVcpMax = (int)max;
                var brightnessInfo = new VcpFeatureValue((int)current, 0, (int)max);
                monitor.CurrentBrightness = brightnessInfo.ToPercentage();
            }
        }

        /// <summary>
        /// Initialize contrast value for a monitor using VCP 0x12.
        /// Persists the device-reported raw maximum so subsequent writes can scale percent → raw.
        /// </summary>
        private static void InitializeContrast(Monitor monitor, IntPtr handle)
        {
            if (TryGetVcpFeature(handle, VcpCodeContrast, monitor.Id, out uint current, out uint max))
            {
                monitor.ContrastVcpMax = (int)max;
                var contrastInfo = new VcpFeatureValue((int)current, 0, (int)max);
                monitor.CurrentContrast = contrastInfo.ToPercentage();
            }
        }

        /// <summary>
        /// Initialize volume value for a monitor using VCP 0x62.
        /// Persists the device-reported raw maximum so subsequent writes can scale percent → raw.
        /// </summary>
        private static void InitializeVolume(Monitor monitor, IntPtr handle)
        {
            if (TryGetVcpFeature(handle, VcpCodeVolume, monitor.Id, out uint current, out uint max))
            {
                monitor.VolumeVcpMax = (int)max;
                var volumeInfo = new VcpFeatureValue((int)current, 0, (int)max);
                monitor.CurrentVolume = volumeInfo.ToPercentage();
            }
        }

        /// <summary>
        /// Wrapper for GetVCPFeatureAndVCPFeatureReply that logs errors on failure.
        /// </summary>
        /// <param name="handle">Physical monitor handle</param>
        /// <param name="vcpCode">VCP code to read</param>
        /// <param name="monitorId">Monitor ID for logging (optional)</param>
        /// <param name="currentValue">Output: current value</param>
        /// <param name="maxValue">Output: maximum value</param>
        /// <returns>True if successful, false otherwise</returns>
        private static bool TryGetVcpFeature(IntPtr handle, byte vcpCode, string? monitorId, out uint currentValue, out uint maxValue)
        {
            if (GetVCPFeatureAndVCPFeatureReply(handle, vcpCode, IntPtr.Zero, out currentValue, out maxValue))
            {
                return true;
            }

            var lastError = Marshal.GetLastWin32Error();
            var monitorPrefix = string.IsNullOrEmpty(monitorId) ? string.Empty : $"[{monitorId}] ";
            Logger.LogError($"{monitorPrefix}Failed to read VCP 0x{vcpCode:X2}, error code: {lastError}");
            return false;
        }

        /// <summary>
        /// Update monitor capability flags based on parsed VCP capabilities.
        /// </summary>
        private static void UpdateMonitorCapabilitiesFromVcp(Monitor monitor, VcpCapabilities vcpCaps)
        {
            // Check for Brightness support (VCP 0x10)
            if (vcpCaps.SupportsVcpCode(VcpCodeBrightness))
            {
                monitor.Capabilities |= MonitorCapabilities.Brightness;
            }

            // Check for Contrast support (VCP 0x12)
            if (vcpCaps.SupportsVcpCode(VcpCodeContrast))
            {
                monitor.Capabilities |= MonitorCapabilities.Contrast;
            }

            // Check for Volume support (VCP 0x62)
            if (vcpCaps.SupportsVcpCode(VcpCodeVolume))
            {
                monitor.Capabilities |= MonitorCapabilities.Volume;
            }

            // Check for Color Temperature support (VCP 0x14)
            if (vcpCaps.SupportsVcpCode(VcpCodeSelectColorPreset))
            {
                monitor.SupportsColorTemperature = true;
            }
        }

        /// <summary>
        /// Get physical monitors with retry logic to handle Windows API occasionally returning NULL handles.
        /// NULL handles are automatically filtered out by GetPhysicalMonitors; retry if any were filtered.
        /// </summary>
        /// <param name="hMonitor">Handle to the monitor</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Array of valid physical monitors, or null if failed after retries</returns>
        private async Task<PHYSICAL_MONITOR[]?> GetPhysicalMonitorsWithRetryAsync(
            IntPtr hMonitor,
            CancellationToken cancellationToken)
        {
            const int maxRetries = 3;
            const int retryDelayMs = 200;
            PHYSICAL_MONITOR[]? lastResult = null;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                if (attempt > 0)
                {
                    await Task.Delay(retryDelayMs, cancellationToken);
                }

                // Sync Win32 call wrapped on the threadpool so concurrent callers
                // (one per hMonitor pipeline) dispatch to separate threads rather
                // than serializing on the calling thread before the first await.
                var (monitors, hasNullHandles) = await Task.Run(
                    () =>
                    {
                        var m = _discoveryHelper.GetPhysicalMonitors(hMonitor, out bool nulls);
                        return (m, nulls);
                    },
                    cancellationToken);

                if (monitors != null && !hasNullHandles)
                {
                    return monitors;
                }

                lastResult = monitors;

                if (monitors != null && hasNullHandles && attempt < maxRetries - 1)
                {
                    Logger.LogWarning($"DDC: Some monitors had NULL handles on attempt {attempt + 1}, will retry");
                    continue;
                }

                if (monitors == null && attempt < maxRetries - 1)
                {
                    Logger.LogWarning($"DDC: GetPhysicalMonitors returned null on attempt {attempt + 1}, will retry");
                    continue;
                }

                if (monitors != null && hasNullHandles)
                {
                    Logger.LogWarning($"DDC: NULL handles still present after {maxRetries} attempts, using filtered result");
                }

                return monitors;
            }

            return lastResult;
        }

        /// <summary>
        /// Full per-hMonitor pipeline: GDI-name filter, get physical handles, and for each
        /// matching physical run <see cref="BuildMonitorFromPhysical"/> on the threadpool.
        /// Physical monitors that share an hMonitor (mirror mode) process sequentially —
        /// they share the GDI source and I2C arbitration. Parallelism across hMonitors is
        /// the caller's job (see <see cref="DiscoverMonitorsAsync"/>'s Task.WhenAll).
        /// </summary>
        /// <remarks>
        /// Catches all exceptions except <see cref="OperationCanceledException"/> and
        /// <see cref="OutOfMemoryException"/> — those propagate to Task.WhenAll and the
        /// surrounding MonitorManager.SafeDiscoverAsync wrapper.
        /// </remarks>
        private async Task<IReadOnlyList<Monitor>> DiscoverFromHandleAsync(
            IntPtr hMonitor,
            IReadOnlyDictionary<string, List<MonitorDisplayInfo>> targetsByGdi,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var gdiName = GetGdiDeviceName(hMonitor);
                if (string.IsNullOrEmpty(gdiName))
                {
                    Logger.LogWarning($"DDC: Failed to get GDI device name for hMonitor 0x{hMonitor:X}");
                    return Array.Empty<Monitor>();
                }

                if (!targetsByGdi.TryGetValue(gdiName, out var matchingInfos))
                {
                    // GDI name not in the external targets list — either a Phase 0 internal
                    // panel or a target QueryDisplayConfig didn't enumerate. Skip BEFORE the
                    // expensive GetPhysicalMonitorsFromHMONITOR call.
                    Logger.LogDebug($"DDC skipping {gdiName}: not in external targets list");
                    return Array.Empty<Monitor>();
                }

                var physicals = await GetPhysicalMonitorsWithRetryAsync(hMonitor, cancellationToken);
                if (physicals == null || physicals.Length == 0)
                {
                    Logger.LogWarning($"DDC: Failed to get physical monitors for {gdiName} after retries");
                    return Array.Empty<Monitor>();
                }

                var monitors = new List<Monitor>();
                for (int i = 0; i < physicals.Length; i++)
                {
                    if (i >= matchingInfos.Count)
                    {
                        Logger.LogWarning(
                            $"DDC: Physical monitor index {i} exceeds available QueryDisplayConfig entries " +
                            $"({matchingInfos.Count}) for {gdiName}");
                        break;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    var physical = physicals[i];
                    var info = matchingInfos[i];

                    // Heavy sync block (~4 s caps fetch + up to 6 × ~100 ms VCP reads on this
                    // one I2C bus). Dispatch to the threadpool; await it before the next physical
                    // because they share the same hMonitor's I2C arbitration.
                    var monitor = await Task.Run(
                        () => BuildMonitorFromPhysical(physical, info),
                        cancellationToken);

                    if (monitor != null)
                    {
                        monitors.Add(monitor);
                    }
                }

                return monitors;
            }
            catch (Exception ex) when (
                ex is not OperationCanceledException &&
                ex is not OutOfMemoryException)
            {
                Logger.LogError($"DDC: pipeline exception for hMonitor=0x{hMonitor:X}: {ex.Message}");
                return Array.Empty<Monitor>();
            }
        }

        /// <summary>
        /// Generic method to get VCP feature value.
        /// </summary>
        /// <param name="monitor">Monitor to query</param>
        /// <param name="vcpCode">VCP code to read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        private async Task<VcpFeatureValue> GetVcpFeatureAsync(
            Monitor monitor,
            byte vcpCode,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(
                () =>
                {
                    if (monitor.Handle == IntPtr.Zero)
                    {
                        return VcpFeatureValue.Invalid;
                    }

                    if (TryGetVcpFeature(monitor.Handle, vcpCode, monitor.Id, out uint current, out uint max))
                    {
                        return new VcpFeatureValue((int)current, 0, (int)max);
                    }

                    return VcpFeatureValue.Invalid;
                },
                cancellationToken);
        }

        /// <summary>
        /// Generic method to set VCP feature value directly.
        /// </summary>
        private Task<MonitorOperationResult> SetVcpFeatureAsync(
            Monitor monitor,
            byte vcpCode,
            int value,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);

            return Task.Run(
                () =>
                {
                    if (monitor.Handle == IntPtr.Zero)
                    {
                        return MonitorOperationResult.Failure("Invalid monitor handle");
                    }

                    try
                    {
                        if (SetVCPFeature(monitor.Handle, vcpCode, (uint)value))
                        {
                            return MonitorOperationResult.Success();
                        }

                        var lastError = Marshal.GetLastWin32Error();
                        return MonitorOperationResult.Failure($"Failed to set VCP 0x{vcpCode:X2}", lastError);
                    }
                    catch (Exception ex)
                    {
                        return MonitorOperationResult.Failure($"Exception setting VCP 0x{vcpCode:X2}: {ex.Message}");
                    }
                },
                cancellationToken);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _handleManager?.Dispose();
                _disposed = true;
            }
        }
    }
}
