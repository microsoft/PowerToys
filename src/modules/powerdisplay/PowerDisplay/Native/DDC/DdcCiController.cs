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
using PowerDisplay.Helpers;
using static PowerDisplay.Native.NativeConstants;
using static PowerDisplay.Native.NativeDelegates;
using static PowerDisplay.Native.PInvoke;
using Monitor = PowerDisplay.Common.Models.Monitor;

// Type aliases matching Windows API naming conventions for better readability when working with native structures.
// These uppercase aliases are used consistently throughout this file to match Win32 API documentation.
using MONITORINFOEX = PowerDisplay.Native.MonitorInfoEx;
using PHYSICAL_MONITOR = PowerDisplay.Native.PhysicalMonitor;
using RECT = PowerDisplay.Native.Rect;

namespace PowerDisplay.Native.DDC
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
        /// Check if the specified monitor can be controlled
        /// </summary>
        public async Task<bool> CanControlMonitorAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            return await Task.Run(
                () =>
                {
                    var physicalHandle = GetPhysicalHandle(monitor);
                    return physicalHandle != IntPtr.Zero && DdcCiNative.ValidateDdcCiConnection(physicalHandle);
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
                    if (physicalHandle == IntPtr.Zero)
                    {
                        Logger.LogDebug($"[{monitor.Id}] Invalid physical handle");
                        return BrightnessInfo.Invalid;
                    }

                    // First try high-level API
                    if (DdcCiNative.TryGetMonitorBrightness(physicalHandle, out uint minBrightness, out uint currentBrightness, out uint maxBrightness))
                    {
                        Logger.LogDebug($"[{monitor.Id}] Brightness via high-level API: {currentBrightness}/{maxBrightness}");
                        return new BrightnessInfo((int)currentBrightness, (int)minBrightness, (int)maxBrightness);
                    }

                    // Try VCP code 0x10 (standard brightness)
                    if (DdcCiNative.TryGetVCPFeature(physicalHandle, VcpCodeBrightness, out uint current, out uint max))
                    {
                        Logger.LogDebug($"[{monitor.Id}] Brightness via 0x10: {current}/{max}");
                        return new BrightnessInfo((int)current, 0, (int)max);
                    }

                    Logger.LogWarning($"[{monitor.Id}] Failed to read brightness");
                    return BrightnessInfo.Invalid;
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
                        var currentInfo = GetBrightnessInfo(monitor, physicalHandle);
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
        /// Get monitor capabilities string with retry logic
        /// </summary>
        public async Task<string> GetCapabilitiesStringAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            return await Task.Run(
                () =>
                {
                if (monitor.Handle == IntPtr.Zero)
                {
                    return string.Empty;
                }

                try
                {
                    // Step 1: Get capabilities string length (retry up to 3 times)
                    uint length = 0;
                    const int lengthMaxRetries = 3;
                    for (int i = 0; i < lengthMaxRetries; i++)
                    {
                        if (GetCapabilitiesStringLength(monitor.Handle, out length) && length > 0)
                        {
                            Logger.LogDebug($"Got capabilities length: {length} (attempt {i + 1})");
                            break;
                        }

                        if (i < lengthMaxRetries - 1)
                        {
                            Thread.Sleep(RetryDelayMs);
                        }
                    }

                    if (length == 0)
                    {
                        Logger.LogWarning("Failed to get capabilities string length after retries");
                        return string.Empty;
                    }

                    // Step 2: Get actual capabilities string (retry up to 5 times)
                    const int capsMaxRetries = 5;
                    for (int i = 0; i < capsMaxRetries; i++)
                    {
                        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal((int)length);
                        try
                        {
                            if (CapabilitiesRequestAndCapabilitiesReply(monitor.Handle, buffer, length))
                            {
                                var capsString = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(buffer) ?? string.Empty;
                                if (!string.IsNullOrEmpty(capsString))
                                {
                                    Logger.LogInfo($"Got capabilities string (length: {capsString.Length}, attempt: {i + 1})");
                                    return capsString;
                                }
                            }
                        }
                        finally
                        {
                            System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
                        }

                        if (i < capsMaxRetries - 1)
                        {
                            Thread.Sleep(RetryDelayMs);
                        }
                    }

                    Logger.LogWarning("Failed to get capabilities string after retries");
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

                    // Get physical handles for each monitor
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
                        // For each physical monitor on this adapter, find the corresponding DisplayDeviceInfo
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
                            var (handleToUse, reusingOldHandle) = _handleManager.ReuseOrCreateHandle(deviceKey, physicalMonitor.HPhysicalMonitor);

                            // Validate DDC/CI connection for the handle we're going to use
                            if (!reusingOldHandle && !DdcCiNative.ValidateDdcCiConnection(handleToUse))
                            {
                                Logger.LogWarning($"DDC: New handle 0x{handleToUse:X} failed DDC/CI validation, skipping");
                                continue;
                            }

                            // Update physical monitor handle to use the correct one
                            var monitorToCreate = physicalMonitor;
                            monitorToCreate.HPhysicalMonitor = handleToUse;

                            var monitor = _discoveryHelper.CreateMonitorFromPhysical(monitorToCreate, adapterName, i, monitorDisplayInfo, matchedDevice);
                            if (monitor != null)
                            {
                                monitors.Add(monitor);

                                // Store in new map for cleanup
                                newHandleMap[monitor.DeviceKey] = handleToUse;
                            }
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
        /// Validate monitor connection status
        /// </summary>
        public async Task<bool> ValidateConnectionAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            return await Task.Run(
                () => monitor.Handle != IntPtr.Zero && DdcCiNative.ValidateDdcCiConnection(monitor.Handle),
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
        /// Get brightness information using VCP code 0x10 only
        /// </summary>
        private BrightnessInfo GetBrightnessInfo(Monitor monitor, IntPtr physicalHandle)
        {
            if (physicalHandle == IntPtr.Zero)
            {
                return BrightnessInfo.Invalid;
            }

            // First try high-level API
            if (DdcCiNative.TryGetMonitorBrightness(physicalHandle, out uint min, out uint current, out uint max))
            {
                return new BrightnessInfo((int)current, (int)min, (int)max);
            }

            // Try VCP code 0x10 (standard brightness)
            if (DdcCiNative.TryGetVCPFeature(physicalHandle, VcpCodeBrightness, out current, out max))
            {
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
