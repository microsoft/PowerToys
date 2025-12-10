// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
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
using RECT = PowerDisplay.Common.Drivers.Rect;

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

        private readonly PhysicalMonitorHandleManager _handleManager = new();
        private readonly MonitorDiscoveryHelper _discoveryHelper;

        private bool _disposed;

        public DdcCiController()
        {
            _discoveryHelper = new MonitorDiscoveryHelper();
        }

        public string Name => "DDC/CI Monitor Controller";

        /// <summary>
        /// Check if the specified monitor can be controlled.
        /// Uses quick connection check since capabilities are already cached during discovery.
        /// </summary>
        public async Task<bool> CanControlMonitorAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);

            return await Task.Run(
                () =>
                {
                    var physicalHandle = GetPhysicalHandle(monitor);
                    if (physicalHandle == IntPtr.Zero)
                    {
                        return false;
                    }

                    // Capabilities are always cached during DiscoverMonitorsAsync Phase 2,
                    // so we can use quick connection check instead of full validation
                    return DdcCiNative.QuickConnectionCheck(physicalHandle);
                },
                cancellationToken);
        }

        /// <summary>
        /// Get monitor brightness using VCP code 0x10
        /// </summary>
        public async Task<BrightnessInfo> GetBrightnessAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);

            return await Task.Run(
                () =>
                {
                    var physicalHandle = GetPhysicalHandle(monitor);
                    var result = GetBrightnessInfoCore(monitor.Id, physicalHandle);

                    if (!result.IsValid)
                    {
                        Logger.LogWarning($"[{monitor.Id}] Failed to read brightness");
                    }

                    return result;
                },
                cancellationToken);
        }

        /// <summary>
        /// Set monitor brightness using VCP code 0x10
        /// </summary>
        public Task<MonitorOperationResult> SetBrightnessAsync(Monitor monitor, int brightness, CancellationToken cancellationToken = default)
            => SetVcpFeatureAsync(monitor, NativeConstants.VcpCodeBrightness, brightness, 0, 100, cancellationToken);

        /// <summary>
        /// Set monitor contrast
        /// </summary>
        public Task<MonitorOperationResult> SetContrastAsync(Monitor monitor, int contrast, CancellationToken cancellationToken = default)
            => SetVcpFeatureAsync(monitor, NativeConstants.VcpCodeContrast, contrast, 0, 100, cancellationToken);

        /// <summary>
        /// Set monitor volume
        /// </summary>
        public Task<MonitorOperationResult> SetVolumeAsync(Monitor monitor, int volume, CancellationToken cancellationToken = default)
            => SetVcpFeatureAsync(monitor, NativeConstants.VcpCodeVolume, volume, 0, 100, cancellationToken);

        /// <summary>
        /// Get monitor color temperature using VCP code 0x14 (Select Color Preset)
        /// Returns the raw VCP preset value (e.g., 0x05 for 6500K), not Kelvin temperature
        /// </summary>
        public async Task<BrightnessInfo> GetColorTemperatureAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);
            return await GetVcpFeatureAsync(monitor, VcpCodeSelectColorPreset, "Color temperature", cancellationToken);
        }

        /// <summary>
        /// Set monitor color temperature using VCP code 0x14 (Select Color Preset)
        /// </summary>
        /// <param name="monitor">Monitor to control</param>
        /// <param name="colorTemperature">VCP preset value (e.g., 0x05 for 6500K), not Kelvin temperature</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task<MonitorOperationResult> SetColorTemperatureAsync(Monitor monitor, int colorTemperature, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);

            return await Task.Run(
                () =>
                {
                    if (monitor.Handle == IntPtr.Zero)
                    {
                        return MonitorOperationResult.Failure("Invalid monitor handle");
                    }

                    try
                    {
                        // Validate value is in supported list
                        var validationError = ValidateDiscreteVcpValue(monitor, VcpCodeSelectColorPreset, colorTemperature, "Color preset");
                        if (validationError != null)
                        {
                            return validationError.Value;
                        }

                        // Set VCP 0x14 value
                        var presetName = VcpValueNames.GetFormattedName(VcpCodeSelectColorPreset, colorTemperature);
                        if (DdcCiNative.TrySetVCPFeature(monitor.Handle, VcpCodeSelectColorPreset, (uint)colorTemperature))
                        {
                            Logger.LogInfo($"[{monitor.Id}] Set color temperature to {presetName} via 0x14");
                            return MonitorOperationResult.Success();
                        }

                        var lastError = GetLastError();
                        Logger.LogError($"[{monitor.Id}] Failed to set color temperature, error: {lastError}");
                        return MonitorOperationResult.Failure("Failed to set color temperature via DDC/CI", (int)lastError);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"[{monitor.Id}] Exception setting color temperature: {ex.Message}");
                        return MonitorOperationResult.Failure($"Exception setting color temperature: {ex.Message}");
                    }
                },
                cancellationToken);
        }

        /// <summary>
        /// Get current input source using VCP code 0x60
        /// Returns the raw VCP value (e.g., 0x11 for HDMI-1)
        /// </summary>
        public async Task<BrightnessInfo> GetInputSourceAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);
            return await GetVcpFeatureAsync(monitor, VcpCodeInputSource, "Input source", cancellationToken);
        }

        /// <summary>
        /// Set input source using VCP code 0x60
        /// </summary>
        /// <param name="monitor">Monitor to control</param>
        /// <param name="inputSource">VCP input source value (e.g., 0x11 for HDMI-1)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task<MonitorOperationResult> SetInputSourceAsync(Monitor monitor, int inputSource, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);

            return await Task.Run(
                async () =>
                {
                    if (monitor.Handle == IntPtr.Zero)
                    {
                        return MonitorOperationResult.Failure("Invalid monitor handle");
                    }

                    try
                    {
                        // Validate value is in supported list
                        var validationError = ValidateDiscreteVcpValue(monitor, VcpCodeInputSource, inputSource, "Input source");
                        if (validationError != null)
                        {
                            return validationError.Value;
                        }

                        // Set VCP 0x60 value
                        var sourceName = VcpValueNames.GetFormattedName(VcpCodeInputSource, inputSource);
                        if (DdcCiNative.TrySetVCPFeature(monitor.Handle, VcpCodeInputSource, (uint)inputSource))
                        {
                            Logger.LogInfo($"[{monitor.Id}] Set input source to {sourceName} via 0x60");

                            // Verify the change by reading back the value after a short delay
                            await VerifyInputSourceChangeAsync(monitor, inputSource, cancellationToken);

                            // Update the monitor model with the new value
                            monitor.CurrentInputSource = inputSource;

                            return MonitorOperationResult.Success();
                        }

                        var lastError = GetLastError();
                        Logger.LogError($"[{monitor.Id}] Failed to set input source, error: {lastError}");
                        return MonitorOperationResult.Failure("Failed to set input source via DDC/CI", (int)lastError);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"[{monitor.Id}] Exception setting input source: {ex.Message}");
                        return MonitorOperationResult.Failure($"Exception setting input source: {ex.Message}");
                    }
                },
                cancellationToken);
        }

        /// <summary>
        /// Verify input source change by reading back the value after a short delay.
        /// Logs warning if verification fails or value doesn't match.
        /// </summary>
        private static async Task VerifyInputSourceChangeAsync(Monitor monitor, int expectedValue, CancellationToken cancellationToken)
        {
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);

            if (DdcCiNative.TryGetVCPFeature(monitor.Handle, VcpCodeInputSource, out uint verifyValue, out uint _))
            {
                var verifyName = VcpValueNames.GetFormattedName(VcpCodeInputSource, (int)verifyValue);
                if (verifyValue == (uint)expectedValue)
                {
                    Logger.LogDebug($"[{monitor.Id}] Input source verified: {verifyName} (0x{verifyValue:X2})");
                }
                else
                {
                    Logger.LogWarning($"[{monitor.Id}] Input source verification mismatch! Expected 0x{expectedValue:X2}, got {verifyName} (0x{verifyValue:X2}). Monitor may have refused to switch (no signal on target port?)");
                }
            }
            else
            {
                Logger.LogWarning($"[{monitor.Id}] Could not verify input source change");
            }
        }

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
                Logger.LogDebug($"GetCapabilitiesStringAsync: Using cached capabilities for {monitor.Id} (length: {monitor.CapabilitiesRaw.Length})");
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
                        var length = RetryHelper.ExecuteWithRetry(
                            () =>
                            {
                                if (GetCapabilitiesStringLength(monitor.Handle, out uint len) && len > 0)
                                {
                                    return len;
                                }

                                return 0u;
                            },
                            len => len > 0,
                            maxRetries: 3,
                            delayMs: RetryDelayMs,
                            operationName: "GetCapabilitiesStringLength");

                        if (length == 0)
                        {
                            return string.Empty;
                        }

                        // Step 2: Get actual capabilities string with retry
                        var capsString = RetryHelper.ExecuteWithRetry(
                            () => TryGetCapabilitiesString(monitor.Handle, length),
                            str => !string.IsNullOrEmpty(str),
                            maxRetries: 5,
                            delayMs: RetryDelayMs,
                            operationName: "GetCapabilitiesString");

                        if (!string.IsNullOrEmpty(capsString))
                        {
                            Logger.LogDebug($"Got capabilities string (length: {capsString.Length})");
                            return capsString;
                        }
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
            if (GetMonitorInfo(hMonitor, ref monitorInfo))
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

                    // Generate stable device key using DevicePath hash for uniqueness
                    var deviceKey = !string.IsNullOrEmpty(monitorInfo.HardwareId)
                        ? $"{monitorInfo.HardwareId}_{monitorInfo.MonitorNumber}"
                        : $"Unknown_{monitorInfo.MonitorNumber}";

                    var (handleToUse, _) = _handleManager.ReuseOrCreateHandle(deviceKey, physicalMonitor.HPhysicalMonitor);

                    var monitorToCreate = physicalMonitor;
                    monitorToCreate.HPhysicalMonitor = handleToUse;

                    candidates.Add(new CandidateMonitor(handleToUse, monitorToCreate, monitorInfo));

                    Logger.LogDebug($"DDC: Candidate {gdiDeviceName} -> DevicePath={monitorInfo.DevicePath}, HardwareId={monitorInfo.HardwareId}");
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
            Logger.LogInfo($"DDC: Phase 2 - Fetching capabilities for {candidates.Count} monitors in parallel");

            var tasks = candidates.Select(candidate =>
                Task.Run(
                    () => (Candidate: candidate, Result: DdcCiNative.FetchCapabilities(candidate.Handle)),
                    cancellationToken));

            var results = await Task.WhenAll(tasks);

            Logger.LogInfo($"DDC: Phase 2 completed - Got results for {results.Length} monitors");
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
                    Logger.LogDebug($"DDC: Handle 0x{candidate.Handle:X} - No DDC/CI brightness support, skipping");
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
                }

                monitors.Add(monitor);
                newHandleMap[monitor.DeviceKey] = candidate.Handle;

                Logger.LogInfo($"DDC: Added monitor {monitor.Id} with {monitor.VcpCapabilitiesInfo?.SupportedVcpCodes.Count ?? 0} VCP codes");
            }

            _handleManager.UpdateHandleMap(newHandleMap);
            return monitors;
        }

        /// <summary>
        /// Initialize input source value for a monitor using VCP 0x60.
        /// </summary>
        private static void InitializeInputSource(Monitor monitor, IntPtr handle)
        {
            if (DdcCiNative.TryGetVCPFeature(handle, VcpCodeInputSource, out uint current, out uint _))
            {
                monitor.CurrentInputSource = (int)current;
                Logger.LogDebug($"[{monitor.Id}] Input source: {VcpValueNames.GetFormattedName(VcpCodeInputSource, (int)current)}");
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

            Logger.LogDebug($"[{monitor.Id}] Capabilities: Contrast={monitor.SupportsContrast}, Volume={monitor.SupportsVolume}, ColorTemp={monitor.SupportsColorTemperature}, InputSource={monitor.SupportsInputSource}");
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
        /// Generic method to get VCP feature value with optional logging.
        /// </summary>
        /// <param name="monitor">Monitor to query</param>
        /// <param name="vcpCode">VCP code to read</param>
        /// <param name="featureName">Optional feature name for logging (e.g., "color temperature", "input source")</param>
        /// <param name="cancellationToken">Cancellation token</param>
        private async Task<BrightnessInfo> GetVcpFeatureAsync(
            Monitor monitor,
            byte vcpCode,
            string? featureName = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(
                () =>
                {
                    if (monitor.Handle == IntPtr.Zero)
                    {
                        if (featureName != null)
                        {
                            Logger.LogDebug($"[{monitor.Id}] Invalid handle for {featureName} read");
                        }

                        return BrightnessInfo.Invalid;
                    }

                    if (DdcCiNative.TryGetVCPFeature(monitor.Handle, vcpCode, out uint current, out uint max))
                    {
                        if (featureName != null)
                        {
                            var valueName = VcpValueNames.GetFormattedName(vcpCode, (int)current);
                            Logger.LogDebug($"[{monitor.Id}] {featureName} via 0x{vcpCode:X2}: {valueName}");
                        }

                        return new BrightnessInfo((int)current, 0, (int)max);
                    }

                    if (featureName != null)
                    {
                        Logger.LogWarning($"[{monitor.Id}] Failed to read {featureName} (0x{vcpCode:X2} not supported)");
                    }

                    return BrightnessInfo.Invalid;
                },
                cancellationToken);
        }

        /// <summary>
        /// Validate that a discrete VCP value is supported by the monitor.
        /// Returns null if valid, or a failure result if invalid.
        /// </summary>
        /// <param name="monitor">Monitor to validate against</param>
        /// <param name="vcpCode">VCP code to check</param>
        /// <param name="value">Value to validate</param>
        /// <param name="featureName">Feature name for error messages</param>
        /// <returns>Null if valid, MonitorOperationResult.Failure if invalid</returns>
        private static MonitorOperationResult? ValidateDiscreteVcpValue(
            Monitor monitor,
            byte vcpCode,
            int value,
            string featureName)
        {
            var capabilities = monitor.VcpCapabilitiesInfo;
            if (capabilities == null || !capabilities.SupportsVcpCode(vcpCode))
            {
                return null; // No capabilities to validate against, allow the operation
            }

            var supportedValues = capabilities.GetSupportedValues(vcpCode);
            if (supportedValues == null || supportedValues.Count == 0 || supportedValues.Contains(value))
            {
                return null; // Value is valid or no discrete values defined
            }

            var supportedList = string.Join(", ", supportedValues.Select(v => $"0x{v:X2}"));
            Logger.LogWarning($"[{monitor.Id}] {featureName} 0x{value:X2} not in supported list: [{supportedList}]");
            return MonitorOperationResult.Failure($"{featureName} 0x{value:X2} not supported by monitor");
        }

        /// <summary>
        /// Generic method to set VCP feature value
        /// </summary>
        private async Task<MonitorOperationResult> SetVcpFeatureAsync(
            Monitor monitor,
            byte vcpCode,
            int value,
            int min = 0,
            int max = 100,
            CancellationToken cancellationToken = default)
        {
            value = Math.Clamp(value, min, max);

            return await Task.Run(
                async () =>
                {
                    if (monitor.Handle == IntPtr.Zero)
                    {
                        return MonitorOperationResult.Failure("Invalid monitor handle");
                    }

                    try
                    {
                        // Get current value to determine range
                        var currentInfo = await GetVcpFeatureAsync(monitor, vcpCode);
                        if (!currentInfo.IsValid)
                        {
                            return MonitorOperationResult.Failure($"Cannot read current value for VCP 0x{vcpCode:X2}");
                        }

                        uint targetValue = (uint)currentInfo.FromPercentage(value);

                        if (DdcCiNative.TrySetVCPFeature(monitor.Handle, vcpCode, targetValue))
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

        /// <summary>
        /// Core implementation for getting brightness information using VCP code 0x10.
        /// </summary>
        /// <param name="monitorId">Monitor ID for logging.</param>
        /// <param name="physicalHandle">Physical monitor handle.</param>
        /// <returns>BrightnessInfo with current, min, and max values, or Invalid if failed.</returns>
        private BrightnessInfo GetBrightnessInfoCore(string monitorId, IntPtr physicalHandle)
        {
            if (physicalHandle == IntPtr.Zero)
            {
                Logger.LogDebug($"[{monitorId}] Invalid physical handle");
                return BrightnessInfo.Invalid;
            }

            if (DdcCiNative.TryGetVCPFeature(physicalHandle, VcpCodeBrightness, out uint current, out uint max))
            {
                Logger.LogDebug($"[{monitorId}] Brightness via VCP 0x10: {current}/{max}");
                return new BrightnessInfo((int)current, 0, (int)max);
            }

            return BrightnessInfo.Invalid;
        }

        /// <summary>
        /// Get physical handle for monitor using stable deviceKey
        /// </summary>
        private IntPtr GetPhysicalHandle(Monitor monitor)
        {
            return _handleManager.GetPhysicalHandle(monitor);
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
