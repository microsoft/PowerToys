// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Polly;
using Polly.Retry;
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
        /// <summary>
        /// Represents a candidate monitor discovered during Phase 1 of monitor enumeration.
        /// </summary>
        /// <param name="Handle">Physical monitor handle for DDC/CI communication</param>
        /// <param name="PhysicalMonitor">Native physical monitor structure with description</param>
        /// <param name="MonitorInfo">Display info from QueryDisplayConfig (HardwareId, FriendlyName, MonitorNumber)</param>
        private readonly record struct CandidateMonitor(
            IntPtr Handle,
            PHYSICAL_MONITOR PhysicalMonitor,
            MonitorDisplayInfo MonitorInfo);

        /// <summary>
        /// Delay between retry attempts for DDC/CI operations (in milliseconds)
        /// </summary>
        private const int RetryDelayMs = 100;

        /// <summary>
        /// Retry pipeline for getting capabilities string length (3 retries).
        /// </summary>
        private static readonly ResiliencePipeline<uint> CapabilitiesLengthRetryPipeline =
            new ResiliencePipelineBuilder<uint>()
                .AddRetry(new RetryStrategyOptions<uint>
                {
                    MaxRetryAttempts = 2, // 2 retries = 3 total attempts
                    Delay = TimeSpan.FromMilliseconds(RetryDelayMs),
                    ShouldHandle = new PredicateBuilder<uint>().HandleResult(len => len == 0),
                    OnRetry = static args =>
                    {
                        Logger.LogWarning($"[Retry] GetCapabilitiesStringLength returned invalid result on attempt {args.AttemptNumber + 1}, retrying...");
                        return default;
                    },
                })
                .Build();

        /// <summary>
        /// Retry pipeline for getting capabilities string (5 retries).
        /// </summary>
        private static readonly ResiliencePipeline<string?> CapabilitiesStringRetryPipeline =
            new ResiliencePipelineBuilder<string?>()
                .AddRetry(new RetryStrategyOptions<string?>
                {
                    MaxRetryAttempts = 4, // 4 retries = 5 total attempts
                    Delay = TimeSpan.FromMilliseconds(RetryDelayMs),
                    ShouldHandle = new PredicateBuilder<string?>().HandleResult(static str => string.IsNullOrEmpty(str)),
                    OnRetry = static args =>
                    {
                        Logger.LogWarning($"[Retry] GetCapabilitiesString returned invalid result on attempt {args.AttemptNumber + 1}, retrying...");
                        return default;
                    },
                })
                .Build();

        private readonly PhysicalMonitorHandleManager _handleManager = new();
        private readonly MonitorDiscoveryHelper _discoveryHelper;

        private bool _disposed;

        public DdcCiController()
        {
            _discoveryHelper = new MonitorDiscoveryHelper();
        }

        public string Name => "DDC/CI Monitor Controller";

        /// <summary>
        /// Get monitor brightness using VCP code 0x10
        /// </summary>
        public async Task<VcpFeatureValue> GetBrightnessAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);
            return await GetVcpFeatureAsync(monitor, VcpCodeBrightness, cancellationToken);
        }

        /// <summary>
        /// Set monitor brightness using VCP code 0x10
        /// </summary>
        public Task<MonitorOperationResult> SetBrightnessAsync(Monitor monitor, int brightness, CancellationToken cancellationToken = default)
            => SetVcpFeatureAsync(monitor, NativeConstants.VcpCodeBrightness, brightness, cancellationToken);

        /// <summary>
        /// Set monitor contrast
        /// </summary>
        public Task<MonitorOperationResult> SetContrastAsync(Monitor monitor, int contrast, CancellationToken cancellationToken = default)
            => SetVcpFeatureAsync(monitor, NativeConstants.VcpCodeContrast, contrast, cancellationToken);

        /// <summary>
        /// Set monitor volume
        /// </summary>
        public Task<MonitorOperationResult> SetVolumeAsync(Monitor monitor, int volume, CancellationToken cancellationToken = default)
            => SetVcpFeatureAsync(monitor, NativeConstants.VcpCodeVolume, volume, cancellationToken);

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
        /// Get monitor capabilities string with retry logic.
        /// Uses cached CapabilitiesRaw if available to avoid slow I2C operations.
        /// </summary>
        public async Task<string> GetCapabilitiesStringAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);

            // Check if capabilities are already cached
            if (!string.IsNullOrEmpty(monitor.CapabilitiesRaw))
            {
                return monitor.CapabilitiesRaw;
            }

            return await Task.Run(
                () =>
                {
                    if (monitor.Handle == IntPtr.Zero)
                    {
                        return string.Empty;
                    }

                    try
                    {
                        // Step 1: Get capabilities string length with retry
                        var length = CapabilitiesLengthRetryPipeline.Execute(() =>
                        {
                            if (GetCapabilitiesStringLength(monitor.Handle, out uint len) && len > 0)
                            {
                                return len;
                            }

                            return 0u;
                        });

                        if (length == 0)
                        {
                            Logger.LogWarning("[Retry] GetCapabilitiesStringLength failed after 3 attempts");
                            return string.Empty;
                        }

                        // Step 2: Get actual capabilities string with retry
                        var capsString = CapabilitiesStringRetryPipeline.Execute(
                            () => TryGetCapabilitiesString(monitor.Handle, length));

                        if (!string.IsNullOrEmpty(capsString))
                        {
                            return capsString;
                        }

                        Logger.LogWarning("[Retry] GetCapabilitiesString failed after 5 attempts");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Exception getting capabilities string: {ex.Message}");
                    }

                    return string.Empty;
                },
                cancellationToken);
        }

        /// <summary>
        /// Try to get capabilities string from monitor handle.
        /// </summary>
        private string? TryGetCapabilitiesString(IntPtr handle, uint length)
        {
            var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal((int)length);
            try
            {
                if (CapabilitiesRequestAndCapabilitiesReply(handle, buffer, length))
                {
                    return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(buffer);
                }

                return null;
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>
        /// Discover supported monitors using a three-phase approach:
        /// Phase 1: Enumerate and collect candidate monitors with their handles
        /// Phase 2: Fetch DDC/CI capabilities in parallel (slow I2C operations)
        /// Phase 3: Create Monitor objects for valid DDC/CI monitors
        /// </summary>
        public async Task<IEnumerable<Monitor>> DiscoverMonitorsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Get monitor display info from QueryDisplayConfig, keyed by device path (unique per target)
                var allMonitorDisplayInfo = DdcCiNative.GetAllMonitorDisplayInfo();

                // Phase 1: Collect candidate monitors
                var monitorHandles = EnumerateMonitorHandles();
                if (monitorHandles.Count == 0)
                {
                    return Enumerable.Empty<Monitor>();
                }

                var candidateMonitors = await CollectCandidateMonitorsAsync(
                    monitorHandles, allMonitorDisplayInfo, cancellationToken);

                if (candidateMonitors.Count == 0)
                {
                    return Enumerable.Empty<Monitor>();
                }

                // Phase 2: Fetch capabilities in parallel
                var fetchResults = await FetchCapabilitiesInParallelAsync(
                    candidateMonitors, cancellationToken);

                // Phase 3: Create monitor objects
                return CreateValidMonitors(fetchResults);
            }
            catch (Exception ex)
            {
                Logger.LogError($"DDC: DiscoverMonitorsAsync exception: {ex.Message}\nStack: {ex.StackTrace}");
                return Enumerable.Empty<Monitor>();
            }
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
        /// Phase 1: Collect all candidate monitors with their physical handles.
        /// Matches physical monitors with MonitorDisplayInfo using GDI device name and friendly name.
        /// Supports mirror mode where multiple physical monitors share the same GDI name.
        /// </summary>
        private async Task<List<CandidateMonitor>> CollectCandidateMonitorsAsync(
            List<IntPtr> monitorHandles,
            Dictionary<string, MonitorDisplayInfo> allMonitorDisplayInfo,
            CancellationToken cancellationToken)
        {
            var candidates = new List<CandidateMonitor>();

            foreach (var hMonitor in monitorHandles)
            {
                // Get GDI device name for this monitor (e.g., "\\.\DISPLAY1")
                var gdiDeviceName = GetGdiDeviceName(hMonitor);
                if (string.IsNullOrEmpty(gdiDeviceName))
                {
                    Logger.LogWarning($"DDC: Failed to get GDI device name for hMonitor 0x{hMonitor:X}");
                    continue;
                }

                var physicalMonitors = await GetPhysicalMonitorsWithRetryAsync(hMonitor, cancellationToken);
                if (physicalMonitors == null || physicalMonitors.Length == 0)
                {
                    Logger.LogWarning($"DDC: Failed to get physical monitors for {gdiDeviceName} after retries");
                    continue;
                }

                // Find all MonitorDisplayInfo entries that match this GDI device name
                // In mirror mode, multiple targets share the same GDI name
                var matchingInfos = allMonitorDisplayInfo.Values
                    .Where(info => string.Equals(info.GdiDeviceName, gdiDeviceName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matchingInfos.Count == 0)
                {
                    Logger.LogWarning($"DDC: No QueryDisplayConfig info for {gdiDeviceName}, skipping");
                    continue;
                }

                for (int i = 0; i < physicalMonitors.Length; i++)
                {
                    var physicalMonitor = physicalMonitors[i];

                    if (i >= matchingInfos.Count)
                    {
                        Logger.LogWarning($"DDC: Physical monitor index {i} exceeds available QueryDisplayConfig entries ({matchingInfos.Count}) for {gdiDeviceName}");
                        break;
                    }

                    var monitorInfo = matchingInfos[i];

                    candidates.Add(new CandidateMonitor(physicalMonitor.HPhysicalMonitor, physicalMonitor, monitorInfo));
                }
            }

            return candidates;
        }

        /// <summary>
        /// Phase 2: Fetch DDC/CI capabilities in parallel for all candidate monitors.
        /// This is the slow I2C operation (~4s per monitor), but parallelization
        /// significantly reduces total time when multiple monitors are connected.
        /// </summary>
        private async Task<(CandidateMonitor Candidate, DdcCiValidationResult Result)[]> FetchCapabilitiesInParallelAsync(
            List<CandidateMonitor> candidates,
            CancellationToken cancellationToken)
        {
            var tasks = candidates.Select(candidate =>
                Task.Run(
                    () => (Candidate: candidate, Result: DdcCiNative.FetchCapabilities(candidate.Handle)),
                    cancellationToken));

            var results = await Task.WhenAll(tasks);

            return results;
        }

        /// <summary>
        /// Phase 3: Create Monitor objects for valid DDC/CI monitors.
        /// A monitor is valid if it has capabilities with brightness support.
        /// </summary>
        private List<Monitor> CreateValidMonitors(
            (CandidateMonitor Candidate, DdcCiValidationResult Result)[] fetchResults)
        {
            var monitors = new List<Monitor>();
            var newHandleMap = new Dictionary<string, IntPtr>();

            foreach (var (candidate, capResult) in fetchResults)
            {
                if (!capResult.IsValid)
                {
                    continue;
                }

                var monitor = _discoveryHelper.CreateMonitorFromPhysical(
                    candidate.PhysicalMonitor,
                    candidate.MonitorInfo);

                if (monitor == null)
                {
                    continue;
                }

                // Set capabilities data
                if (!string.IsNullOrEmpty(capResult.CapabilitiesString))
                {
                    monitor.CapabilitiesRaw = capResult.CapabilitiesString;
                }

                if (capResult.VcpCapabilitiesInfo != null)
                {
                    monitor.VcpCapabilitiesInfo = capResult.VcpCapabilitiesInfo;
                    UpdateMonitorCapabilitiesFromVcp(monitor, capResult.VcpCapabilitiesInfo);

                    // Initialize input source if supported
                    if (monitor.SupportsInputSource)
                    {
                        InitializeInputSource(monitor, candidate.Handle);
                    }

                    // Initialize color temperature if supported
                    if (monitor.SupportsColorTemperature)
                    {
                        InitializeColorTemperature(monitor, candidate.Handle);
                    }
                }

                // Initialize brightness (always supported for DDC/CI monitors)
                InitializeBrightness(monitor, candidate.Handle);

                monitors.Add(monitor);
                newHandleMap[monitor.Id] = candidate.Handle;
            }

            _handleManager.UpdateHandleMap(newHandleMap);
            return monitors;
        }

        /// <summary>
        /// Initialize input source value for a monitor using VCP 0x60.
        /// </summary>
        private static void InitializeInputSource(Monitor monitor, IntPtr handle)
        {
            if (GetVCPFeatureAndVCPFeatureReply(handle, VcpCodeInputSource, IntPtr.Zero, out uint current, out uint _))
            {
                monitor.CurrentInputSource = (int)current;
            }
        }

        /// <summary>
        /// Initialize color temperature value for a monitor using VCP 0x14.
        /// </summary>
        private static void InitializeColorTemperature(Monitor monitor, IntPtr handle)
        {
            if (GetVCPFeatureAndVCPFeatureReply(handle, VcpCodeSelectColorPreset, IntPtr.Zero, out uint current, out uint _))
            {
                monitor.CurrentColorTemperature = (int)current;
            }
        }

        /// <summary>
        /// Initialize brightness value for a monitor using VCP 0x10.
        /// </summary>
        private static void InitializeBrightness(Monitor monitor, IntPtr handle)
        {
            if (GetVCPFeatureAndVCPFeatureReply(handle, VcpCodeBrightness, IntPtr.Zero, out uint current, out uint max))
            {
                var brightnessInfo = new VcpFeatureValue((int)current, 0, (int)max);
                monitor.CurrentBrightness = brightnessInfo.ToPercentage();
            }
        }

        /// <summary>
        /// Update monitor capability flags based on parsed VCP capabilities.
        /// </summary>
        private static void UpdateMonitorCapabilitiesFromVcp(Monitor monitor, VcpCapabilities vcpCaps)
        {
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

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                if (attempt > 0)
                {
                    await Task.Delay(retryDelayMs, cancellationToken);
                }

                var monitors = _discoveryHelper.GetPhysicalMonitors(hMonitor, out bool hasNullHandles);

                // Success: got valid monitors with no NULL handles filtered out
                if (monitors != null && !hasNullHandles)
                {
                    return monitors;
                }

                // Got monitors but some had NULL handles - retry to see if API stabilizes
                if (monitors != null && hasNullHandles && attempt < maxRetries - 1)
                {
                    Logger.LogWarning($"DDC: Some monitors had NULL handles on attempt {attempt + 1}, will retry");
                    continue;
                }

                // No monitors returned - retry
                if (monitors == null && attempt < maxRetries - 1)
                {
                    Logger.LogWarning($"DDC: GetPhysicalMonitors returned null on attempt {attempt + 1}, will retry");
                    continue;
                }

                // Last attempt - return whatever we have (may have NULL handles filtered)
                if (monitors != null && hasNullHandles)
                {
                    Logger.LogWarning($"DDC: NULL handles still present after {maxRetries} attempts, using filtered result");
                }

                return monitors;
            }

            return null;
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

                    if (GetVCPFeatureAndVCPFeatureReply(monitor.Handle, vcpCode, IntPtr.Zero, out uint current, out uint max))
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

                        var lastError = GetLastError();
                        return MonitorOperationResult.Failure($"Failed to set VCP 0x{vcpCode:X2}", (int)lastError);
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
