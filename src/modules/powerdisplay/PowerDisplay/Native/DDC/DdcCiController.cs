// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using PowerDisplay.Core.Interfaces;
using PowerDisplay.Core.Models;
using static PowerDisplay.Native.NativeConstants;
using static PowerDisplay.Native.NativeDelegates;
using static PowerDisplay.Native.PInvoke;
using Monitor = PowerDisplay.Core.Models.Monitor;

// Type aliases matching Windows API naming conventions for better readability when working with native structures.
// These uppercase aliases are used consistently throughout this file to match Win32 API documentation.
using PHYSICAL_MONITOR = PowerDisplay.Native.PhysicalMonitor;
using RECT = PowerDisplay.Native.Rect;
using MONITORINFOEX = PowerDisplay.Native.MonitorInfoEx;

namespace PowerDisplay.Native.DDC
{
    /// <summary>
    /// DDC/CI monitor controller for controlling external monitors
    /// </summary>
    public partial class DdcCiController : IMonitorController, IDisposable
    {
        private readonly PhysicalMonitorHandleManager _handleManager = new();
        private readonly VcpCodeResolver _vcpResolver = new();
        private readonly MonitorDiscoveryHelper _discoveryHelper;

        private bool _disposed;

        public DdcCiController()
        {
            _discoveryHelper = new MonitorDiscoveryHelper(_vcpResolver);
        }

        public string Name => "DDC/CI Monitor Controller";

        public MonitorType SupportedType => MonitorType.External;

        /// <summary>
        /// Check if the specified monitor can be controlled
        /// </summary>
        public async Task<bool> CanControlMonitorAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            if (monitor.Type != MonitorType.External)
            {
                return false;
            }

            return await Task.Run(
                () =>
                {
                    var physicalHandle = GetPhysicalHandle(monitor);
                    return physicalHandle != IntPtr.Zero && DdcCiNative.ValidateDdcCiConnection(physicalHandle);
                },
                cancellationToken);
        }

        /// <summary>
        /// Get monitor brightness
        /// </summary>
        public async Task<BrightnessInfo> GetBrightnessAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            return await Task.Run(
                () =>
                {
                    var physicalHandle = GetPhysicalHandle(monitor);
                    if (physicalHandle == IntPtr.Zero)
                    {
                        return BrightnessInfo.Invalid;
                    }

                // First try high-level API
                if (DdcCiNative.TryGetMonitorBrightness(physicalHandle, out uint minBrightness, out uint currentBrightness, out uint maxBrightness))
                {
                    return new BrightnessInfo((int)currentBrightness, (int)minBrightness, (int)maxBrightness);
                }

                // Try different VCP codes
                var vcpCode = _vcpResolver.GetBrightnessVcpCode(monitor.Id, physicalHandle);
                if (vcpCode.HasValue && DdcCiNative.TryGetVCPFeature(physicalHandle, vcpCode.Value, out uint current, out uint max))
                {
                    return new BrightnessInfo((int)current, 0, (int)max);
                }

                    return BrightnessInfo.Invalid;
                },
                cancellationToken);
        }

        /// <summary>
        /// Set monitor brightness
        /// </summary>
        public async Task<MonitorOperationResult> SetBrightnessAsync(Monitor monitor, int brightness, CancellationToken cancellationToken = default)
        {
            brightness = Math.Clamp(brightness, 0, 100);

            return await Task.Run(() =>
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
                        return MonitorOperationResult.Failure("Cannot read current brightness");
                    }

                    uint targetValue = (uint)currentInfo.FromPercentage(brightness);

                    // First try high-level API
                    if (DdcCiNative.TrySetMonitorBrightness(physicalHandle, targetValue))
                    {
                        return MonitorOperationResult.Success();
                    }

                    // Try VCP codes
                    var vcpCode = _vcpResolver.GetBrightnessVcpCode(monitor.Id, physicalHandle);
                    if (vcpCode.HasValue && DdcCiNative.TrySetVCPFeature(physicalHandle, vcpCode.Value, targetValue))
                    {
                        return MonitorOperationResult.Success();
                    }

                    var lastError = GetLastError();
                    return MonitorOperationResult.Failure($"Failed to set brightness via DDC/CI", (int)lastError);
                }
                catch (Exception ex)
                {
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
        /// Get monitor color temperature
        /// </summary>
        public async Task<BrightnessInfo> GetColorTemperatureAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            return await Task.Run(
                () =>
                {
                    if (monitor.Handle == IntPtr.Zero)
                    {
                        return BrightnessInfo.Invalid;
                    }

                    // Try different VCP codes for color temperature
                    var vcpCode = _vcpResolver.GetColorTemperatureVcpCode(monitor.Id, monitor.Handle);
                    if (vcpCode.HasValue && DdcCiNative.TryGetVCPFeature(monitor.Handle, vcpCode.Value, out uint current, out uint max))
                    {
                        return new BrightnessInfo((int)current, 0, (int)max);
                    }

                    return BrightnessInfo.Invalid;
                },
                cancellationToken);
        }

        /// <summary>
        /// Set monitor color temperature
        /// </summary>
        public async Task<MonitorOperationResult> SetColorTemperatureAsync(Monitor monitor, int colorTemperature, CancellationToken cancellationToken = default)
        {
            colorTemperature = Math.Clamp(colorTemperature, 2000, 10000);

            return await Task.Run(
                () =>
                {
                    if (monitor.Handle == IntPtr.Zero)
                    {
                        return MonitorOperationResult.Failure("Invalid monitor handle");
                    }

                    try
                    {
                        // Get current color temperature info to understand the range
                        var currentInfo = _vcpResolver.GetCurrentColorTemperature(monitor.Handle);
                        if (!currentInfo.IsValid)
                        {
                            return MonitorOperationResult.Failure("Cannot read current color temperature");
                        }

                        // Convert Kelvin temperature to VCP value
                        uint targetValue = _vcpResolver.ConvertKelvinToVcpValue(colorTemperature, currentInfo);

                        // Try to set using the best available VCP code
                        var vcpCode = _vcpResolver.GetColorTemperatureVcpCode(monitor.Id, monitor.Handle);
                        if (vcpCode.HasValue && DdcCiNative.TrySetVCPFeature(monitor.Handle, vcpCode.Value, targetValue))
                        {
                            Logger.LogInfo($"Successfully set color temperature to {colorTemperature}K via DDC/CI (VCP 0x{vcpCode.Value:X2})");
                            return MonitorOperationResult.Success();
                        }

                        var lastError = GetLastError();
                        return MonitorOperationResult.Failure($"Failed to set color temperature via DDC/CI", (int)lastError);
                    }
                    catch (Exception ex)
                    {
                        return MonitorOperationResult.Failure($"Exception setting color temperature: {ex.Message}");
                    }
                },
                cancellationToken);
        }

        /// <summary>
        /// Get monitor capabilities string
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
                    if (GetCapabilitiesStringLength(monitor.Handle, out uint length) && length > 0)
                    {
                        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal((int)length);
                        try
                        {
                            if (CapabilitiesRequestAndCapabilitiesReply(monitor.Handle, buffer, length))
                            {
                                return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(buffer) ?? string.Empty;
                            }
                        }
                        finally
                        {
                            System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Failed to get capabilities string: {ex.Message}");
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
                    // Get all display devices with stable device IDs (Twinkle Tray style)
                    var displayDevices = DdcCiNative.GetAllDisplayDevices();
                    Logger.LogInfo($"DDC: Found {displayDevices.Count} display devices via EnumDisplayDevices");

                    // Also get hardware info for friendly names
                    var monitorDisplayInfo = DdcCiNative.GetAllMonitorDisplayInfo();
                    Logger.LogDebug($"DDC: GetAllMonitorDisplayInfo returned {monitorDisplayInfo.Count} items");

                    // Enumerate all monitors
                    var monitorHandles = new List<IntPtr>();
                    Logger.LogDebug($"DDC: About to call EnumDisplayMonitors...");

                    bool EnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData)
                    {
                        Logger.LogDebug($"DDC: EnumProc callback - hMonitor=0x{hMonitor:X}");
                        monitorHandles.Add(hMonitor);
                        return true;
                    }

                    bool enumResult = EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, EnumProc, IntPtr.Zero);
                    Logger.LogDebug($"DDC: EnumDisplayMonitors returned {enumResult}, found {monitorHandles.Count} monitor handles");

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

                        // Sometimes Windows returns NULL handles. Implement Twinkle Tray's retry logic.
                        // See: twinkle-tray/src/Monitors.js line 617
                        PHYSICAL_MONITOR[]? physicalMonitors = null;
                        const int maxRetries = 3;
                        const int retryDelayMs = 200;

                        for (int attempt = 0; attempt < maxRetries; attempt++)
                        {
                            if (attempt > 0)
                            {
                                Logger.LogInfo($"DDC: Retry attempt {attempt}/{maxRetries - 1} for hMonitor 0x{hMonitor:X} after {retryDelayMs}ms delay");
                                await Task.Delay(retryDelayMs, cancellationToken);
                            }

                            physicalMonitors = _discoveryHelper.GetPhysicalMonitors(hMonitor);

                            if (physicalMonitors == null || physicalMonitors.Length == 0)
                            {
                                if (attempt < maxRetries - 1)
                                {
                                    Logger.LogWarning($"DDC: GetPhysicalMonitors returned null/empty on attempt {attempt + 1}, will retry");
                                }
                                continue;
                            }

                            // Check if any handle is NULL (Twinkle Tray checks handleIsValid)
                            bool hasNullHandle = false;
                            for (int i = 0; i < physicalMonitors.Length; i++)
                            {
                                if (physicalMonitors[i].HPhysicalMonitor == IntPtr.Zero)
                                {
                                    hasNullHandle = true;
                                    Logger.LogWarning($"DDC: Physical monitor [{i}] has NULL handle on attempt {attempt + 1}");
                                    break;
                                }
                            }

                            if (!hasNullHandle)
                            {
                                // Success! All handles are valid
                                if (attempt > 0)
                                {
                                    Logger.LogInfo($"DDC: Successfully obtained valid handles on attempt {attempt + 1}");
                                }
                                break;
                            }
                            else if (attempt < maxRetries - 1)
                            {
                                Logger.LogWarning($"DDC: NULL handle detected, will retry (attempt {attempt + 1}/{maxRetries})");
                                physicalMonitors = null; // Reset for retry
                            }
                            else
                            {
                                Logger.LogWarning($"DDC: NULL handle still present after {maxRetries} attempts, continuing anyway");
                            }
                        }

                        if (physicalMonitors == null || physicalMonitors.Length == 0)
                        {
                            Logger.LogWarning($"DDC: Failed to get physical monitors for hMonitor 0x{hMonitor:X} after {maxRetries} attempts");
                            continue;
                        }

                        // Match physical monitors with DisplayDeviceInfo (Twinkle Tray logic)
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
                                Logger.LogInfo($"DDC: Created monitor {monitor.Id} with handle 0x{monitor.Handle:X} (reused: {reusingOldHandle}), deviceKey: {monitor.DeviceKey}");
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
                finally
                {
                    Logger.LogDebug($"DDC: DiscoverMonitorsAsync returning {monitors.Count} monitors");
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
        /// Generic method to get VCP feature value
        /// </summary>
        private async Task<BrightnessInfo> GetVcpFeatureAsync(
            Monitor monitor,
            byte vcpCode,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
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
            }, cancellationToken);
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

            return await Task.Run(() =>
            {
                if (monitor.Handle == IntPtr.Zero)
                {
                    return MonitorOperationResult.Failure("Invalid monitor handle");
                }

                try
                {
                    // Get current value to determine range
                    var currentInfo = GetVcpFeatureAsync(monitor, vcpCode).Result;
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
            }, cancellationToken);
        }


        /// <summary>
        /// Get brightness information (with explicit handle)
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

            // Try VCP codes
            var vcpCode = _vcpResolver.GetBrightnessVcpCode(monitor.Id, physicalHandle);
            if (vcpCode.HasValue && DdcCiNative.TryGetVCPFeature(physicalHandle, vcpCode.Value, out current, out max))
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
                _vcpResolver?.ClearCache();
                _disposed = true;
            }
        }
    }
}
