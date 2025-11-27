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
        public async Task<MonitorOperationResult> SetBrightnessAsync(Monitor monitor, int brightness, CancellationToken cancellationToken = default)
        {
            brightness = Math.Clamp(brightness, 0, 100);

            return await Task.Run(
                () =>
                {
                    var physicalHandle = GetPhysicalHandle(monitor);
                    if (physicalHandle == IntPtr.Zero)
                    {
                        return MonitorOperationResult.Failure("No physical handle found");
                    }

                    try
                    {
                        var currentInfo = GetBrightnessInfoCore(monitor.Id, physicalHandle);
                        if (!currentInfo.IsValid)
                        {
                            Logger.LogWarning($"[{monitor.Id}] Cannot read current brightness");
                            return MonitorOperationResult.Failure("Cannot read current brightness");
                        }

                        uint targetValue = (uint)currentInfo.FromPercentage(brightness);

                        // First try high-level API
                        if (DdcCiNative.TrySetMonitorBrightness(physicalHandle, targetValue))
                        {
                            Logger.LogInfo($"[{monitor.Id}] Set brightness to {brightness}% via high-level API");
                            return MonitorOperationResult.Success();
                        }

                        // Try VCP code 0x10 (standard brightness)
                        if (DdcCiNative.TrySetVCPFeature(physicalHandle, VcpCodeBrightness, targetValue))
                        {
                            Logger.LogInfo($"[{monitor.Id}] Set brightness to {brightness}% via 0x10");
                            return MonitorOperationResult.Success();
                        }

                        var lastError = GetLastError();
                        Logger.LogError($"[{monitor.Id}] Failed to set brightness, error: {lastError}");
                        return MonitorOperationResult.Failure($"Failed to set brightness via DDC/CI", (int)lastError);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"[{monitor.Id}] Exception setting brightness: {ex.Message}");
                        return MonitorOperationResult.Failure($"Exception setting brightness: {ex.Message}");
                    }
                },
                cancellationToken);
        }

        /// <summary>
        /// Get monitor contrast
        /// </summary>
        public Task<BrightnessInfo> GetContrastAsync(Monitor monitor, CancellationToken cancellationToken = default)
            => GetVcpFeatureAsync(monitor, NativeConstants.VcpCodeContrast, cancellationToken);

        /// <summary>
        /// Set monitor contrast
        /// </summary>
        public Task<MonitorOperationResult> SetContrastAsync(Monitor monitor, int contrast, CancellationToken cancellationToken = default)
            => SetVcpFeatureAsync(monitor, NativeConstants.VcpCodeContrast, contrast, 0, 100, cancellationToken);

        /// <summary>
        /// Get monitor volume
        /// </summary>
        public Task<BrightnessInfo> GetVolumeAsync(Monitor monitor, CancellationToken cancellationToken = default)
            => GetVcpFeatureAsync(monitor, NativeConstants.VcpCodeVolume, cancellationToken);

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
            return await Task.Run(
                () =>
                {
                    if (monitor.Handle == IntPtr.Zero)
                    {
                        Logger.LogDebug($"[{monitor.Id}] Invalid handle for color temperature read");
                        return BrightnessInfo.Invalid;
                    }

                    // Try VCP code 0x14 (Select Color Preset)
                    if (DdcCiNative.TryGetVCPFeature(monitor.Handle, VcpCodeSelectColorPreset, out uint current, out uint max))
                    {
                        var presetName = VcpValueNames.GetFormattedName(0x14, (int)current);
                        Logger.LogInfo($"[{monitor.Id}] Color temperature via 0x14: {presetName}");
                        return new BrightnessInfo((int)current, 0, (int)max);
                    }

                    Logger.LogWarning($"[{monitor.Id}] Failed to read color temperature (0x14 not supported)");
                    return BrightnessInfo.Invalid;
                },
                cancellationToken);
        }

        /// <summary>
        /// Set monitor color temperature using VCP code 0x14 (Select Color Preset)
        /// </summary>
        /// <param name="monitor">Monitor to control</param>
        /// <param name="colorTemperature">VCP preset value (e.g., 0x05 for 6500K), not Kelvin temperature</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task<MonitorOperationResult> SetColorTemperatureAsync(Monitor monitor, int colorTemperature, CancellationToken cancellationToken = default)
        {
            return await Task.Run(
                () =>
                {
                    if (monitor.Handle == IntPtr.Zero)
                    {
                        return MonitorOperationResult.Failure("Invalid monitor handle");
                    }

                    try
                    {
                        // Validate value is in supported list if capabilities available
                        var capabilities = monitor.VcpCapabilitiesInfo;
                        if (capabilities != null && capabilities.SupportsVcpCode(0x14))
                        {
                            var supportedValues = capabilities.GetSupportedValues(0x14);
                            if (supportedValues?.Count > 0 && !supportedValues.Contains(colorTemperature))
                            {
                                var supportedList = string.Join(", ", supportedValues.Select(v => $"0x{v:X2}"));
                                Logger.LogWarning($"[{monitor.Id}] Color preset 0x{colorTemperature:X2} not in supported list: [{supportedList}]");
                                return MonitorOperationResult.Failure($"Color preset 0x{colorTemperature:X2} not supported by monitor");
                            }
                        }

                        // Set VCP 0x14 value
                        var presetName = VcpValueNames.GetFormattedName(0x14, colorTemperature);
                        if (DdcCiNative.TrySetVCPFeature(monitor.Handle, VcpCodeSelectColorPreset, (uint)colorTemperature))
                        {
                            Logger.LogInfo($"[{monitor.Id}] Set color temperature to {presetName} via 0x14");
                            return MonitorOperationResult.Success();
                        }

                        var lastError = GetLastError();
                        Logger.LogError($"[{monitor.Id}] Failed to set color temperature, error: {lastError}");
                        return MonitorOperationResult.Failure($"Failed to set color temperature via DDC/CI", (int)lastError);
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
            return await Task.Run(
                () =>
                {
                    if (monitor.Handle == IntPtr.Zero)
                    {
                        Logger.LogDebug($"[{monitor.Id}] Invalid handle for input source read");
                        return BrightnessInfo.Invalid;
                    }

                    // Try VCP code 0x60 (Input Source)
                    if (DdcCiNative.TryGetVCPFeature(monitor.Handle, VcpCodeInputSource, out uint current, out uint max))
                    {
                        var sourceName = VcpValueNames.GetFormattedName(0x60, (int)current);
                        Logger.LogInfo($"[{monitor.Id}] Input source via 0x60: {sourceName}");
                        return new BrightnessInfo((int)current, 0, (int)max);
                    }

                    Logger.LogWarning($"[{monitor.Id}] Failed to read input source (0x60 not supported)");
                    return BrightnessInfo.Invalid;
                },
                cancellationToken);
        }

        /// <summary>
        /// Set input source using VCP code 0x60
        /// </summary>
        /// <param name="monitor">Monitor to control</param>
        /// <param name="inputSource">VCP input source value (e.g., 0x11 for HDMI-1)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task<MonitorOperationResult> SetInputSourceAsync(Monitor monitor, int inputSource, CancellationToken cancellationToken = default)
        {
            return await Task.Run(
                () =>
                {
                    if (monitor.Handle == IntPtr.Zero)
                    {
                        return MonitorOperationResult.Failure("Invalid monitor handle");
                    }

                    try
                    {
                        // Validate value is in supported list if capabilities available
                        var capabilities = monitor.VcpCapabilitiesInfo;
                        if (capabilities != null && capabilities.SupportsVcpCode(0x60))
                        {
                            var supportedValues = capabilities.GetSupportedValues(0x60);
                            if (supportedValues?.Count > 0 && !supportedValues.Contains(inputSource))
                            {
                                var supportedList = string.Join(", ", supportedValues.Select(v => $"0x{v:X2}"));
                                Logger.LogWarning($"[{monitor.Id}] Input source 0x{inputSource:X2} not in supported list: [{supportedList}]");
                                return MonitorOperationResult.Failure($"Input source 0x{inputSource:X2} not supported by monitor");
                            }
                        }

                        // Set VCP 0x60 value
                        var sourceName = VcpValueNames.GetFormattedName(0x60, inputSource);
                        if (DdcCiNative.TrySetVCPFeature(monitor.Handle, VcpCodeInputSource, (uint)inputSource))
                        {
                            Logger.LogInfo($"[{monitor.Id}] Set input source to {sourceName} via 0x60");

                            // Verify the change by reading back the value after a short delay
                            System.Threading.Thread.Sleep(100);
                            if (DdcCiNative.TryGetVCPFeature(monitor.Handle, VcpCodeInputSource, out uint verifyValue, out uint _))
                            {
                                var verifyName = VcpValueNames.GetFormattedName(0x60, (int)verifyValue);
                                if (verifyValue == (uint)inputSource)
                                {
                                    Logger.LogInfo($"[{monitor.Id}] Input source verified: {verifyName} (0x{verifyValue:X2})");
                                }
                                else
                                {
                                    Logger.LogWarning($"[{monitor.Id}] Input source verification mismatch! Expected 0x{inputSource:X2}, got {verifyName} (0x{verifyValue:X2}). Monitor may have refused to switch (no signal on target port?)");
                                }
                            }
                            else
                            {
                                Logger.LogWarning($"[{monitor.Id}] Could not verify input source change");
                            }

                            // Update the monitor model with the new value
                            monitor.CurrentInputSource = inputSource;

                            return MonitorOperationResult.Success();
                        }

                        var lastError = GetLastError();
                        Logger.LogError($"[{monitor.Id}] Failed to set input source, error: {lastError}");
                        return MonitorOperationResult.Failure($"Failed to set input source via DDC/CI", (int)lastError);
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
        /// Get monitor capabilities string with retry logic.
        /// Uses cached CapabilitiesRaw if available to avoid slow I2C operations.
        /// </summary>
        public async Task<string> GetCapabilitiesStringAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
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
                            Logger.LogInfo($"Got capabilities string (length: {capsString.Length})");
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
        /// Save current settings
        /// </summary>
        public async Task<MonitorOperationResult> SaveCurrentSettingsAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            return await Task.Run(
                () =>
                {
                if (monitor.Handle == IntPtr.Zero)
                {
                    return MonitorOperationResult.Failure("Invalid monitor handle");
                }

                try
                {
                    if (SaveCurrentSettings(monitor.Handle))
                    {
                        return MonitorOperationResult.Success();
                    }

                    var lastError = GetLastError();
                    return MonitorOperationResult.Failure($"Failed to save settings", (int)lastError);
                }
                catch (Exception ex)
                {
                    return MonitorOperationResult.Failure($"Exception saving settings: {ex.Message}");
                }
                },
                cancellationToken);
        }

        /// <summary>
        /// Discover supported monitors
        /// </summary>
        public async Task<IEnumerable<Monitor>> DiscoverMonitorsAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(
                async () =>
                {
                var monitors = new List<Monitor>();
                var newHandleMap = new Dictionary<string, IntPtr>();

                try
                {
                    // Get all display devices with stable device IDs
                    var displayDevices = DdcCiNative.GetAllDisplayDevices();

                    // Also get hardware info for friendly names
                    var monitorDisplayInfo = DdcCiNative.GetAllMonitorDisplayInfo();

                    // Enumerate all monitors
                    var monitorHandles = new List<IntPtr>();

                    bool EnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData)
                    {
                        monitorHandles.Add(hMonitor);
                        return true;
                    }

                    bool enumResult = EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, EnumProc, IntPtr.Zero);

                    if (!enumResult)
                    {
                        Logger.LogWarning($"DDC: EnumDisplayMonitors failed");
                        return monitors;
                    }

                    // Phase 1: Collect all candidate monitors with their handles
                    var candidateMonitors = new List<(IntPtr Handle, string DeviceKey, PHYSICAL_MONITOR PhysicalMonitor, string AdapterName, int Index, DisplayDeviceInfo? MatchedDevice)>();

                    foreach (var hMonitor in monitorHandles)
                    {
                        var adapterName = _discoveryHelper.GetMonitorDeviceId(hMonitor);
                        if (string.IsNullOrEmpty(adapterName))
                        {
                            continue;
                        }

                        // Get physical monitors with retry logic for NULL handle workaround
                        var physicalMonitors = await GetPhysicalMonitorsWithRetryAsync(hMonitor, cancellationToken);

                        if (physicalMonitors == null || physicalMonitors.Length == 0)
                        {
                            Logger.LogWarning($"DDC: Failed to get physical monitors for hMonitor 0x{hMonitor:X} after retries");
                            continue;
                        }

                        // Match physical monitors with DisplayDeviceInfo
                        for (int i = 0; i < physicalMonitors.Length; i++)
                        {
                            var physicalMonitor = physicalMonitors[i];
                            if (physicalMonitor.HPhysicalMonitor == IntPtr.Zero)
                            {
                                continue;
                            }

                            // Find matching DisplayDeviceInfo for this physical monitor
                            DisplayDeviceInfo? matchedDevice = null;
                            int foundCount = 0;

                            foreach (var displayDevice in displayDevices)
                            {
                                if (displayDevice.AdapterName == adapterName)
                                {
                                    if (foundCount == i)
                                    {
                                        matchedDevice = displayDevice;
                                        break;
                                    }

                                    foundCount++;
                                }
                            }

                            // Determine device key for handle reuse logic
                            string deviceKey = matchedDevice?.DeviceKey ?? $"{adapterName}_{i}";

                            // Use HandleManager to reuse or create handle
                            var (handleToUse, _) = _handleManager.ReuseOrCreateHandle(deviceKey, physicalMonitor.HPhysicalMonitor);

                            // Update physical monitor handle
                            var monitorToCreate = physicalMonitor;
                            monitorToCreate.HPhysicalMonitor = handleToUse;

                            candidateMonitors.Add((handleToUse, deviceKey, monitorToCreate, adapterName, i, matchedDevice));
                        }
                    }

                    // Phase 2: Fetch capabilities in PARALLEL for all candidate monitors
                    // This is the slow I2C operation (~4s per monitor), but parallelization
                    // significantly reduces total time when multiple monitors are connected.
                    // Results are cached regardless of success/failure.
                    Logger.LogInfo($"DDC: Phase 2 - Fetching capabilities for {candidateMonitors.Count} monitors in parallel");

                    var fetchTasks = candidateMonitors.Select(candidate =>
                        Task.Run(
                            () =>
                            {
                                var capabilitiesResult = DdcCiNative.FetchCapabilities(candidate.Handle);
                                return (Candidate: candidate, CapabilitiesResult: capabilitiesResult);
                            },
                            cancellationToken));

                    var fetchResults = await Task.WhenAll(fetchTasks);

                    Logger.LogInfo($"DDC: Phase 2 completed - Got results for {fetchResults.Length} monitors");

                    // Phase 3: Create monitor objects for valid DDC/CI monitors
                    // A monitor is valid for DDC if it has capabilities with brightness support
                    foreach (var result in fetchResults)
                    {
                        // Skip monitors that don't support DDC/CI brightness control
                        if (!result.CapabilitiesResult.IsValid)
                        {
                            Logger.LogDebug($"DDC: Handle 0x{result.Candidate.Handle:X} - No DDC/CI brightness support, skipping");
                            continue;
                        }

                        var monitor = _discoveryHelper.CreateMonitorFromPhysical(
                            result.Candidate.PhysicalMonitor,
                            result.Candidate.AdapterName,
                            result.Candidate.Index,
                            monitorDisplayInfo,
                            result.Candidate.MatchedDevice);

                        if (monitor != null)
                        {
                            // Attach cached capabilities data - this is the key optimization!
                            // By caching here, we avoid re-fetching during InitializeMonitorCapabilitiesAsync
                            if (!string.IsNullOrEmpty(result.CapabilitiesResult.CapabilitiesString))
                            {
                                monitor.CapabilitiesRaw = result.CapabilitiesResult.CapabilitiesString;
                            }

                            if (result.CapabilitiesResult.VcpCapabilitiesInfo != null)
                            {
                                monitor.VcpCapabilitiesInfo = result.CapabilitiesResult.VcpCapabilitiesInfo;
                            }

                            monitors.Add(monitor);
                            newHandleMap[monitor.DeviceKey] = result.Candidate.Handle;

                            Logger.LogInfo($"DDC: Added monitor {monitor.Id} with {monitor.VcpCapabilitiesInfo?.SupportedVcpCodes.Count ?? 0} VCP codes");
                        }
                    }

                    // Update handle manager with new mapping
                    _handleManager.UpdateHandleMap(newHandleMap);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"DDC: DiscoverMonitorsAsync exception: {ex.Message}\nStack: {ex.StackTrace}");
                }

                return monitors;
                },
                cancellationToken);
        }

        /// <summary>
        /// Validate monitor connection status.
        /// Uses quick VCP read instead of full capabilities retrieval.
        /// </summary>
        public async Task<bool> ValidateConnectionAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            return await Task.Run(
                () => monitor.Handle != IntPtr.Zero && DdcCiNative.QuickConnectionCheck(monitor.Handle),
                cancellationToken);
        }

        /// <summary>
        /// Get physical monitors with retry logic to handle Windows API occasionally returning NULL handles
        /// </summary>
        /// <param name="hMonitor">Handle to the monitor</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Array of physical monitors, or null if failed after retries</returns>
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

                var monitors = _discoveryHelper.GetPhysicalMonitors(hMonitor);

                var validationResult = ValidatePhysicalMonitors(monitors, attempt, maxRetries);

                if (validationResult.IsValid)
                {
                    return monitors;
                }

                if (validationResult.ShouldRetry)
                {
                    continue;
                }

                // Last attempt failed, return what we have
                return monitors;
            }

            return null;
        }

        /// <summary>
        /// Validate physical monitors array for null handles
        /// </summary>
        /// <returns>Tuple indicating if valid and if should retry</returns>
        private (bool IsValid, bool ShouldRetry) ValidatePhysicalMonitors(
            PHYSICAL_MONITOR[]? monitors,
            int attempt,
            int maxRetries)
        {
            if (monitors == null || monitors.Length == 0)
            {
                if (attempt < maxRetries - 1)
                {
                    Logger.LogWarning($"DDC: GetPhysicalMonitors returned null/empty on attempt {attempt + 1}, will retry");
                }

                return (false, true);
            }

            bool hasNullHandle = HasAnyNullHandles(monitors, out int nullIndex);

            if (!hasNullHandle)
            {
                return (true, false); // Valid, don't retry
            }

            if (attempt < maxRetries - 1)
            {
                Logger.LogWarning($"DDC: Physical monitor [{nullIndex}] has NULL handle on attempt {attempt + 1}, will retry");
                return (false, true); // Invalid, should retry
            }

            Logger.LogWarning($"DDC: NULL handle still present after {maxRetries} attempts, continuing anyway");
            return (false, false); // Invalid but no more retries
        }

        /// <summary>
        /// Check if any physical monitor has a NULL handle
        /// </summary>
        /// <param name="monitors">Array of physical monitors to check</param>
        /// <param name="nullIndex">Output index of first NULL handle found, or -1 if none</param>
        /// <returns>True if any NULL handle found</returns>
        private bool HasAnyNullHandles(PHYSICAL_MONITOR[] monitors, out int nullIndex)
        {
            for (int i = 0; i < monitors.Length; i++)
            {
                if (monitors[i].HPhysicalMonitor == IntPtr.Zero)
                {
                    nullIndex = i;
                    return true;
                }
            }

            nullIndex = -1;
            return false;
        }

        /// <summary>
        /// Generic method to get VCP feature value
        /// </summary>
        private async Task<BrightnessInfo> GetVcpFeatureAsync(
            Monitor monitor,
            byte vcpCode,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(
                () =>
                {
                    if (monitor.Handle == IntPtr.Zero)
                    {
                        return BrightnessInfo.Invalid;
                    }

                    if (DdcCiNative.TryGetVCPFeature(monitor.Handle, vcpCode, out uint current, out uint max))
                    {
                        return new BrightnessInfo((int)current, 0, (int)max);
                    }

                    return BrightnessInfo.Invalid;
                },
                cancellationToken);
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
        /// Core implementation for getting brightness information using high-level API or VCP code 0x10.
        /// Used by both GetBrightnessAsync and SetBrightnessAsync.
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

            // First try high-level API
            if (DdcCiNative.TryGetMonitorBrightness(physicalHandle, out uint min, out uint current, out uint max))
            {
                Logger.LogDebug($"[{monitorId}] Brightness via high-level API: {current}/{max}");
                return new BrightnessInfo((int)current, (int)min, (int)max);
            }

            // Try VCP code 0x10 (standard brightness)
            if (DdcCiNative.TryGetVCPFeature(physicalHandle, VcpCodeBrightness, out current, out max))
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
