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
using Monitor = PowerDisplay.Core.Models.Monitor;

// Type aliases compatible with Windows API naming conventions
using PHYSICAL_MONITOR = PowerDisplay.Native.PhysicalMonitor;
using RECT = PowerDisplay.Native.Rect;
using MONITORINFOEX = PowerDisplay.Native.MonitorInfoEx;

namespace PowerDisplay.Native.DDC
{
    /// <summary>
    /// DDC/CI monitor controller for controlling external monitors
    /// </summary>
    public class DdcCiController : IExtendedMonitorController, IDisposable
    {
        private readonly Dictionary<string, IntPtr> _monitorHandles = new();
        private readonly Dictionary<string, byte[]> _supportedVcpCodes = new();
        
        // Twinkle Tray style mapping: deviceKey -> physical handle
        private readonly Dictionary<string, IntPtr> _deviceKeyToHandleMap = new();
        
        private bool _disposed;

        // VCP code priority order (for brightness control)
        private static readonly byte[] BrightnessVcpCodes =
        {
            NativeConstants.VcpCodeBrightness,           // 0x10 - Standard brightness
            NativeConstants.VcpCodeBacklightControl,    // 0x13 - Backlight control
            NativeConstants.VcpCodeBacklightLevelWhite, // 0x6B - White backlight level
            NativeConstants.VcpCodeContrast,             // 0x12 - Contrast as last resort
        };

        // VCP code priority order (for color temperature control)
        private static readonly byte[] ColorTemperatureVcpCodes =
        {
            NativeConstants.VcpCodeColorTemperature,           // 0x0C - Standard color temperature
            NativeConstants.VcpCodeColorTemperatureIncrement,  // 0x0B - Color temperature increment
            NativeConstants.VcpCodeSelectColorPreset,          // 0x14 - Color preset selection
            NativeConstants.VcpCodeGamma,                      // 0x72 - Gamma correction
        };

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
                var vcpCode = GetBrightnessVcpCode(monitor, physicalHandle);
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
            
            // Debug logging
            Logger.LogInfo($"DDC: Setting brightness {brightness} for monitor {monitor.Id} (handle 0x{monitor.Handle:X})");

            return await Task.Run(() =>
            {
                // Use new handle lookup mechanism
                var physicalHandle = GetPhysicalHandle(monitor);
                if (physicalHandle == IntPtr.Zero)
                {
                    Logger.LogError($"DDC: No physical handle found for monitor {monitor.Id} (deviceKey: {monitor.DeviceKey})");
                    return MonitorOperationResult.Failure("No physical handle found");
                }

                try
                {
                    // Get current brightness info to determine actual brightness range
                    var currentInfo = GetBrightnessInfo(monitor, physicalHandle);
                    if (!currentInfo.IsValid)
                    {
                        Logger.LogError($"DDC: Cannot read current brightness for {monitor.Id}");
                        return MonitorOperationResult.Failure("Cannot read current brightness");
                    }

                    // Calculate target brightness value
                    uint targetValue = (uint)currentInfo.FromPercentage(brightness);

                    // First try high-level API
                    if (DdcCiNative.TrySetMonitorBrightness(physicalHandle, targetValue))
                    {
                        Logger.LogInfo($"DDC: Successfully set brightness {brightness} for {monitor.Id} via high-level API (handle 0x{physicalHandle:X})");
                        return MonitorOperationResult.Success();
                    }

                    // Try VCP codes
                    var vcpCode = GetBrightnessVcpCode(monitor, physicalHandle);
                    if (vcpCode.HasValue && DdcCiNative.TrySetVCPFeature(physicalHandle, vcpCode.Value, targetValue))
                    {
                        Logger.LogInfo($"DDC: Successfully set brightness {brightness} for {monitor.Id} via VCP code 0x{vcpCode:X} (handle 0x{physicalHandle:X})");
                        return MonitorOperationResult.Success();
                    }

                    var lastError = DdcCiNative.GetLastError();
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
        public async Task<BrightnessInfo> GetContrastAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            return await Task.Run(
                () =>
                {
                    if (monitor.Handle == IntPtr.Zero)
                    {
                        return BrightnessInfo.Invalid;
                    }

                // Try high-level API
                if (DdcCiNative.TryGetVCPFeature(monitor.Handle, NativeConstants.VcpCodeContrast, out uint current, out uint max))
                {
                    return new BrightnessInfo((int)current, 0, (int)max);
                }

                    return BrightnessInfo.Invalid;
                },
                cancellationToken);
        }

        /// <summary>
        /// Set monitor contrast
        /// </summary>
        public async Task<MonitorOperationResult> SetContrastAsync(Monitor monitor, int contrast, CancellationToken cancellationToken = default)
        {
            contrast = Math.Clamp(contrast, 0, 100);

            return await Task.Run(
                () =>
                {
                if (monitor.Handle == IntPtr.Zero)
                {
                    return MonitorOperationResult.Failure("Invalid monitor handle");
                }

                try
                {
                    // Get current contrast info
                    var currentInfo = GetContrastInfo(monitor);
                    if (!currentInfo.IsValid)
                    {
                        return MonitorOperationResult.Failure("Cannot read current contrast");
                    }

                    uint targetValue = (uint)currentInfo.FromPercentage(contrast);

                    if (DdcCiNative.TrySetVCPFeature(monitor.Handle, NativeConstants.VcpCodeContrast, targetValue))
                    {
                        return MonitorOperationResult.Success();
                    }

                    var lastError = DdcCiNative.GetLastError();
                    return MonitorOperationResult.Failure($"Failed to set contrast", (int)lastError);
                }
                catch (Exception ex)
                {
                    return MonitorOperationResult.Failure($"Exception setting contrast: {ex.Message}");
                }
                },
                cancellationToken);
        }

        /// <summary>
        /// Get monitor volume
        /// </summary>
        public async Task<BrightnessInfo> GetVolumeAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            return await Task.Run(
                () =>
                {
                    if (monitor.Handle == IntPtr.Zero)
                    {
                        return BrightnessInfo.Invalid;
                    }

                    // Try to get volume using VCP code
                    if (DdcCiNative.TryGetVCPFeature(monitor.Handle, NativeConstants.VcpCodeVolume, out uint current, out uint max))
                    {
                        return new BrightnessInfo((int)current, 0, (int)max);
                    }

                    return BrightnessInfo.Invalid;
                },
                cancellationToken);
        }

        /// <summary>
        /// Set monitor volume
        /// </summary>
        public async Task<MonitorOperationResult> SetVolumeAsync(Monitor monitor, int volume, CancellationToken cancellationToken = default)
        {
            volume = Math.Clamp(volume, 0, 100);

            return await Task.Run(
                () =>
                {
                    if (monitor.Handle == IntPtr.Zero)
                    {
                        return MonitorOperationResult.Failure("Invalid monitor handle");
                    }

                    try
                    {
                        // Get current volume info
                        var currentInfo = GetVolumeInfo(monitor);
                        if (!currentInfo.IsValid)
                        {
                            return MonitorOperationResult.Failure("Cannot read current volume");
                        }

                        uint targetValue = (uint)currentInfo.FromPercentage(volume);

                        if (DdcCiNative.TrySetVCPFeature(monitor.Handle, NativeConstants.VcpCodeVolume, targetValue))
                        {
                            return MonitorOperationResult.Success();
                        }

                        var lastError = DdcCiNative.GetLastError();
                        return MonitorOperationResult.Failure($"Failed to set volume", (int)lastError);
                    }
                    catch (Exception ex)
                    {
                        return MonitorOperationResult.Failure($"Exception setting volume: {ex.Message}");
                    }
                },
                cancellationToken);
        }

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
                    var vcpCode = GetColorTemperatureVcpCode(monitor, monitor.Handle);
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
                        var currentInfo = GetCurrentColorTemperature(monitor.Handle);
                        if (!currentInfo.IsValid)
                        {
                            return MonitorOperationResult.Failure("Cannot read current color temperature");
                        }

                        // Convert Kelvin temperature to VCP value
                        uint targetValue = ConvertKelvinToVcpValue(colorTemperature, currentInfo);

                        // Try to set using the best available VCP code
                        var vcpCode = GetColorTemperatureVcpCode(monitor, monitor.Handle);
                        if (vcpCode.HasValue && DdcCiNative.TrySetVCPFeature(monitor.Handle, vcpCode.Value, targetValue))
                        {
                            Logger.LogInfo($"Successfully set color temperature to {colorTemperature}K via DDC/CI (VCP 0x{vcpCode.Value:X2})");
                            return MonitorOperationResult.Success();
                        }

                        var lastError = DdcCiNative.GetLastError();
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
                    if (DdcCiNative.GetCapabilitiesStringLength(monitor.Handle, out uint length) && length > 0)
                    {
                        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal((int)length);
                        try
                        {
                            if (DdcCiNative.CapabilitiesRequestAndCapabilitiesReply(monitor.Handle, buffer, length))
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
                catch
                {
                    // Silent failure
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
                    if (DdcCiNative.SaveCurrentSettings(monitor.Handle))
                    {
                        return MonitorOperationResult.Success();
                    }

                    var lastError = DdcCiNative.GetLastError();
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
                () =>
                {
                var monitors = new List<Monitor>();

                try
                {
                    // First get complete monitor information including hardware IDs
                    var monitorDisplayInfo = DdcCiNative.GetAllMonitorDisplayInfo();

                    // Enumerate all monitors
                    var monitorHandles = new List<IntPtr>();

                    bool EnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
                    {
                        monitorHandles.Add(hMonitor);
                        return true;
                    }

                    if (!DdcCiNative.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, EnumProc, IntPtr.Zero))
                    {
                        return monitors;
                    }

                    // Get physical handles for each monitor
                    foreach (var hMonitor in monitorHandles)
                    {
                        var monitorId = GetMonitorDeviceId(hMonitor);
                        if (string.IsNullOrEmpty(monitorId))
                        {
                            continue;
                        }

                        var physicalMonitors = GetPhysicalMonitors(hMonitor);
                        if (physicalMonitors == null || physicalMonitors.Length == 0)
                        {
                            continue;
                        }

                        for (int i = 0; i < physicalMonitors.Length; i++)
                        {
                            var physicalMonitor = physicalMonitors[i];
                            if (physicalMonitor.HPhysicalMonitor == IntPtr.Zero)
                            {
                                continue;
                            }

                            // Validate DDC/CI connection
                            if (!DdcCiNative.ValidateDdcCiConnection(physicalMonitor.HPhysicalMonitor))
                            {
                                continue;
                            }

                            // Generate unique device ID for this physical monitor instance
                            var uniqueDeviceId = GetUniqueMonitorDeviceId(hMonitor, i);

                            var monitor = CreateMonitorFromPhysical(physicalMonitor, uniqueDeviceId, i, monitorDisplayInfo);
                            if (monitor != null)
                            {
                                // Debug logging for handle verification
                                Logger.LogInfo($"DDC: Created monitor {monitor.Id} with handle 0x{monitor.Handle:X}, deviceKey: {monitor.DeviceKey}, fullDeviceID: {monitor.DeviceID}");
                                monitors.Add(monitor);
                                
                                // Store mapping like Twinkle Tray: use full device ID as key (not just deviceKey)
                                _monitorHandles[monitor.Id] = physicalMonitor.HPhysicalMonitor;
                                _deviceKeyToHandleMap[monitor.DeviceID] = physicalMonitor.HPhysicalMonitor; // Use full ID like Twinkle Tray
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Return discovered monitors
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
        /// Get brightness VCP code for the monitor (with explicit handle)
        /// </summary>
        private byte? GetBrightnessVcpCode(Monitor monitor, IntPtr physicalHandle)
        {
            if (_supportedVcpCodes.TryGetValue(monitor.Id, out var cachedCodes) && cachedCodes.Length > 0)
            {
                return cachedCodes[0]; // Return first (best) supported code
            }

            // Test all possible VCP codes with the provided handle
            var supportedCodes = new List<byte>();
            foreach (var code in BrightnessVcpCodes)
            {
                if (DdcCiNative.TryGetVCPFeature(physicalHandle, code, out _, out _))
                {
                    supportedCodes.Add(code);
                }
            }

            if (supportedCodes.Count > 0)
            {
                _supportedVcpCodes[monitor.Id] = supportedCodes.ToArray();
                return supportedCodes[0];
            }

            return null;
        }

        /// <summary>
        /// Get color temperature VCP code for the monitor (with explicit handle)
        /// </summary>
        private byte? GetColorTemperatureVcpCode(Monitor monitor, IntPtr physicalHandle)
        {
            var cacheKey = $"{monitor.Id}_colortemp";
            if (_supportedVcpCodes.TryGetValue(cacheKey, out var cachedCodes) && cachedCodes.Length > 0)
            {
                return cachedCodes[0]; // Return first (best) supported code
            }

            // Test all possible color temperature VCP codes
            var supportedCodes = new List<byte>();
            foreach (var code in ColorTemperatureVcpCodes)
            {
                if (DdcCiNative.TryGetVCPFeature(physicalHandle, code, out _, out _))
                {
                    supportedCodes.Add(code);
                    Logger.LogDebug($"Monitor {monitor.Id} supports color temperature VCP code 0x{code:X2}");
                }
            }

            if (supportedCodes.Count > 0)
            {
                _supportedVcpCodes[cacheKey] = supportedCodes.ToArray();
                Logger.LogInfo($"Monitor {monitor.Id} supports {supportedCodes.Count} color temperature VCP codes, using 0x{supportedCodes[0]:X2}");
                return supportedCodes[0];
            }

            Logger.LogWarning($"Monitor {monitor.Id} does not support any color temperature VCP codes");
            return null;
        }

        /// <summary>
        /// Get current color temperature information
        /// </summary>
        private BrightnessInfo GetCurrentColorTemperature(IntPtr physicalHandle)
        {
            // Try different VCP codes to get color temperature
            foreach (var code in ColorTemperatureVcpCodes)
            {
                if (DdcCiNative.TryGetVCPFeature(physicalHandle, code, out uint current, out uint max))
                {
                    return new BrightnessInfo((int)current, 0, (int)max);
                }
            }

            return BrightnessInfo.Invalid;
        }

        /// <summary>
        /// Convert Kelvin temperature to VCP value
        /// </summary>
        private uint ConvertKelvinToVcpValue(int kelvin, BrightnessInfo vcpRange)
        {
            // Standard color temperature range mapping
            // Cool: 6500K-10000K → VCP high values
            // Neutral: 5000K-6500K → VCP middle values  
            // Warm: 2000K-5000K → VCP low values
            
            const int minKelvin = 2000;
            const int maxKelvin = 10000;
            
            // Clamp input
            kelvin = Math.Clamp(kelvin, minKelvin, maxKelvin);
            
            // Normalize kelvin to 0-1 range
            double normalizedKelvin = (double)(kelvin - minKelvin) / (maxKelvin - minKelvin);
            
            // Map to VCP range (note: some monitors might have inverted ranges)
            uint vcpValue = (uint)(normalizedKelvin * vcpRange.Maximum);
            
            Logger.LogDebug($"Converting {kelvin}K to VCP value {vcpValue} (range 0-{vcpRange.Maximum})");
            return vcpValue;
        }

        /// <summary>
        /// Get brightness VCP code for the monitor (legacy method)
        /// </summary>
        private byte? GetBrightnessVcpCode(Monitor monitor)
        {
            var physicalHandle = GetPhysicalHandle(monitor);
            return GetBrightnessVcpCode(monitor, physicalHandle);
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

            // 尝试 VCP 代码
            var vcpCode = GetBrightnessVcpCode(monitor, physicalHandle);
            if (vcpCode.HasValue && DdcCiNative.TryGetVCPFeature(physicalHandle, vcpCode.Value, out current, out max))
            {
                return new BrightnessInfo((int)current, 0, (int)max);
            }

            return BrightnessInfo.Invalid;
        }

        /// <summary>
        /// Get brightness information (legacy method)
        /// </summary>
        private BrightnessInfo GetBrightnessInfo(Monitor monitor)
        {
            var physicalHandle = GetPhysicalHandle(monitor);
            return GetBrightnessInfo(monitor, physicalHandle);
        }

        /// <summary>
        /// Get contrast information
        /// </summary>
        private BrightnessInfo GetContrastInfo(Monitor monitor)
        {
            if (monitor.Handle != IntPtr.Zero &&
                DdcCiNative.TryGetVCPFeature(monitor.Handle, NativeConstants.VcpCodeContrast, out uint current, out uint max))
            {
                return new BrightnessInfo((int)current, 0, (int)max);
            }

            return BrightnessInfo.Invalid;
        }

        /// <summary>
        /// Get volume information
        /// </summary>
        private BrightnessInfo GetVolumeInfo(Monitor monitor)
        {
            if (monitor.Handle != IntPtr.Zero &&
                DdcCiNative.TryGetVCPFeature(monitor.Handle, NativeConstants.VcpCodeVolume, out uint current, out uint max))
            {
                return new BrightnessInfo((int)current, 0, (int)max);
            }

            return BrightnessInfo.Invalid;
        }

        /// <summary>
        /// Get monitor device ID
        /// </summary>
        private static string GetMonitorDeviceId(IntPtr hMonitor)
        {
            try
            {
                var monitorInfo = new MONITORINFOEX { CbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>() };
                if (DdcCiNative.GetMonitorInfo(hMonitor, ref monitorInfo))
                {
                    return monitorInfo.SzDevice ?? string.Empty;
                }
            }
            catch
            {
                // 静默失败
            }

            return string.Empty;
        }

        /// <summary>
        /// Get physical monitors
        /// </summary>
        private static PHYSICAL_MONITOR[]? GetPhysicalMonitors(IntPtr hMonitor)
        {
            try
            {
                uint numMonitors = 0;
                if (!DdcCiNative.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, ref numMonitors) || numMonitors == 0)
                {
                    return null;
                }

                var physicalMonitors = new PHYSICAL_MONITOR[numMonitors];
                if (!DdcCiNative.GetPhysicalMonitorsFromHMONITOR(hMonitor, numMonitors, physicalMonitors))
                {
                    return null;
                }

                return physicalMonitors;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Create Monitor object from physical monitor (backward compatibility overload)
        /// </summary>
        private Monitor? CreateMonitorFromPhysical(PHYSICAL_MONITOR physicalMonitor, string deviceId, int index)
        {
            return CreateMonitorFromPhysical(physicalMonitor, deviceId, index, new Dictionary<string, MonitorDisplayInfo>());
        }

        /// <summary>
        /// Create Monitor object from physical monitor
        /// </summary>
        private Monitor? CreateMonitorFromPhysical(PHYSICAL_MONITOR physicalMonitor, string uniqueDeviceId, int index, Dictionary<string, MonitorDisplayInfo> monitorDisplayInfo)
        {
            try
            {
                // Parse the unique device ID that we constructed
                // Format: \\.\DISPLAY1_PHYS_0, \\.\DISPLAY1_PHYS_1, etc.
                var parts = uniqueDeviceId.Split('_');
                var baseDevice = parts.Length > 0 ? parts[0] : uniqueDeviceId;
                var physicalIndex = parts.Length > 2 ? parts[2] : index.ToString();

                // Get hardware ID and friendly name from the display info
                string hardwareId = string.Empty;
                string name = physicalMonitor.SzPhysicalMonitorDescription;

                // Try to find matching monitor info
                foreach (var kvp in monitorDisplayInfo.Values)
                {
                    if (!string.IsNullOrEmpty(kvp.HardwareId))
                    {
                        hardwareId = kvp.HardwareId;
                        
                        if (!string.IsNullOrEmpty(kvp.FriendlyName) && !kvp.FriendlyName.Contains("Generic"))
                        {
                            name = kvp.FriendlyName;
                        }
                        break;
                    }
                }

                // Create device parts like Twinkle Tray
                var deviceParts = new string[]
                {
                    @"\\?\DISPLAY",
                    !string.IsNullOrEmpty(hardwareId) ? hardwareId : "EXTERNAL",
                    $"{baseDevice.Replace(@"\\.\", "")}_{physicalIndex}_{physicalMonitor.HPhysicalMonitor.ToString("X")}"
                };

                // Create deviceKey and full device ID
                var deviceKey = deviceParts[2]; // Use the unique instance part as device key
                var fullDeviceID = $"{deviceParts[0]}#{deviceParts[1]}#{deviceParts[2]}";

                // Generate monitor ID using deviceKey as primary identifier (like Twinkle Tray)
                var monitorId = $"DDC_{deviceKey}";

                // If still no good name, use default value
                if (string.IsNullOrEmpty(name) || name.Contains("Generic") || name.Contains("PnP"))
                {
                    name = $"External Display {index + 1}";
                }

                // Get current brightness
                var brightnessInfo = GetCurrentBrightness(physicalMonitor.HPhysicalMonitor);

                var monitor = new Monitor
                {
                    Id = monitorId,
                    HardwareId = hardwareId,
                    Name = name.Trim(),
                    Type = MonitorType.External,
                    CurrentBrightness = brightnessInfo.IsValid ? brightnessInfo.ToPercentage() : 50,
                    MinBrightness = 0,
                    MaxBrightness = 100,
                    IsAvailable = true,
                    Handle = physicalMonitor.HPhysicalMonitor,
                    DevicePath = uniqueDeviceId,
                    DeviceKey = deviceKey,
                    DeviceID = fullDeviceID,
                    Capabilities = MonitorCapabilities.Brightness | MonitorCapabilities.DdcCi,
                    ConnectionType = "External",
                    CommunicationMethod = "DDC/CI",
                    Manufacturer = ExtractManufacturer(name)
                };

                // Check contrast support
                if (DdcCiNative.TryGetVCPFeature(physicalMonitor.HPhysicalMonitor, NativeConstants.VcpCodeContrast, out _, out _))
                {
                    monitor.Capabilities |= MonitorCapabilities.Contrast;
                }

                // Check color temperature support
                var supportsColorTemp = false;
                foreach (var vcpCode in ColorTemperatureVcpCodes)
                {
                    if (DdcCiNative.TryGetVCPFeature(physicalMonitor.HPhysicalMonitor, vcpCode, out _, out _))
                    {
                        supportsColorTemp = true;
                        Logger.LogInfo($"Monitor {monitorId} supports color temperature via VCP 0x{vcpCode:X2}");
                        break;
                    }
                }
                monitor.SupportsColorTemperature = supportsColorTemp;

                // Check volume support
                if (DdcCiNative.TryGetVCPFeature(physicalMonitor.HPhysicalMonitor, NativeConstants.VcpCodeVolume, out _, out _))
                {
                    monitor.Capabilities |= MonitorCapabilities.Volume;
                }

                // Check high-level API support
                if (DdcCiNative.TryGetMonitorBrightness(physicalMonitor.HPhysicalMonitor, out _, out _, out _))
                {
                    monitor.Capabilities |= MonitorCapabilities.HighLevel;
                }

                return monitor;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get current brightness
        /// </summary>
        private static BrightnessInfo GetCurrentBrightness(IntPtr handle)
        {
            // Try high-level API
            if (DdcCiNative.TryGetMonitorBrightness(handle, out uint min, out uint current, out uint max))
            {
                return new BrightnessInfo((int)current, (int)min, (int)max);
            }

            // Try VCP codes
            foreach (var code in BrightnessVcpCodes)
            {
                if (DdcCiNative.TryGetVCPFeature(handle, code, out current, out max))
                {
                    return new BrightnessInfo((int)current, 0, (int)max);
                }
            }

            return BrightnessInfo.Invalid;
        }

        /// <summary>
        /// Extract manufacturer from name
        /// </summary>
        private static string ExtractManufacturer(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "Unknown";
            }

            // Common manufacturer prefixes
            var manufacturers = new[] { "DELL", "HP", "LG", "Samsung", "ASUS", "Acer", "BenQ", "AOC", "ViewSonic" };
            var upperName = name.ToUpperInvariant();

            foreach (var manufacturer in manufacturers)
            {
                if (upperName.Contains(manufacturer))
                {
                    return manufacturer;
                }
            }

            // Return first word as manufacturer
            var firstWord = name.Split(' ')[0];
            return firstWord.Length > 2 ? firstWord : "Unknown";
        }

        /// <summary>
        /// Parse device path like Twinkle Tray to extract hwid components
        /// </summary>
        private static string[]? ParseDevicePath(string devicePath)
        {
            try
            {
                if (string.IsNullOrEmpty(devicePath))
                {
                    return null;
                }

                // Handle Windows device path format like Twinkle Tray
                // Expected format: \\.\DISPLAY1 or similar
                // We need to get the real device ID from Windows APIs instead of creating artificial ones
                return new string[]
                {
                    @"\\?\DISPLAY",
                    "EXTERNAL", // Will be updated with real hardware info later
                    devicePath.Replace(@"\\.\", "").Replace("\\", "_") // Simple conversion for now
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get real Windows device instance ID for a monitor (like Twinkle Tray)
        /// </summary>
        private static string? GetRealDeviceId(IntPtr hMonitor, Dictionary<string, MonitorDisplayInfo> monitorDisplayInfo)
        {
            try
            {
                // First try to get from monitor display info which should contain real device IDs
                foreach (var kvp in monitorDisplayInfo)
                {
                    var displayInfo = kvp.Value;
                    // Since DeviceId doesn't exist, we'll use HardwareId instead
                    if (!string.IsNullOrEmpty(displayInfo.HardwareId))
                    {
                        // Construct a device ID-like string from available information
                        return $@"\\?\DISPLAY#{displayInfo.HardwareId}#{kvp.Key}";
                    }
                }

                // If not available, try to construct from monitor info
                var monitorInfo = new MONITORINFOEX { CbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>() };
                if (DdcCiNative.GetMonitorInfo(hMonitor, ref monitorInfo) && !string.IsNullOrEmpty(monitorInfo.SzDevice))
                {
                    // This gives us something like \\.\DISPLAY1
                    // We need to enhance this to be unique per physical monitor instance
                    var deviceName = monitorInfo.SzDevice;
                    
                    // Try to get more specific device information from registry or other sources
                    // For now, we'll create a unique identifier by combining with available info
                    var baseId = deviceName.Replace(@"\\.\", "");
                    
                    // Add unique identifier from available monitor info
                    var uniqueId = "DEFAULT";
                    foreach (var kvp in monitorDisplayInfo)
                    {
                        var displayInfo = kvp.Value;
                        if (!string.IsNullOrEmpty(displayInfo.HardwareId))
                        {
                            uniqueId = displayInfo.HardwareId;
                            break;
                        }
                    }
                    
                    return $@"\\?\DISPLAY#{uniqueId}#{baseId}";
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get monitor device id with unique instance identification
        /// </summary>
        private static string GetUniqueMonitorDeviceId(IntPtr hMonitor, int physicalIndex)
        {
            try
            {
                var monitorInfo = new MONITORINFOEX { CbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>() };
                if (DdcCiNative.GetMonitorInfo(hMonitor, ref monitorInfo))
                {
                    var baseDevice = monitorInfo.SzDevice ?? "UNKNOWN";
                    // Add physical monitor index to make it unique for multiple identical monitors
                    return $"{baseDevice}_PHYS_{physicalIndex}";
                }
            }
            catch
            {
                // 静默失败
            }

            return $"UNKNOWN_PHYS_{physicalIndex}";
        }

        /// <summary>
        /// Get physical handle by device key (Twinkle Tray style)
        /// </summary>
        private IntPtr GetHandleByDeviceKey(string deviceKey)
        {
            if (_deviceKeyToHandleMap.TryGetValue(deviceKey, out var handle))
            {
                return handle;
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Get physical handle for monitor - try both new and legacy methods
        /// </summary>
        private IntPtr GetPhysicalHandle(Monitor monitor)
        {
            // First try the new device key mapping using full device ID (Twinkle Tray style)
            if (!string.IsNullOrEmpty(monitor.DeviceID))
            {
                var handle = GetHandleByDeviceKey(monitor.DeviceID);
                if (handle != IntPtr.Zero)
                {
                    Logger.LogInfo($"DDC: Found handle 0x{handle:X} for full deviceID {monitor.DeviceID}");
                    return handle;
                }
            }

            // Try with just deviceKey as fallback
            if (!string.IsNullOrEmpty(monitor.DeviceKey))
            {
                var handle = GetHandleByDeviceKey(monitor.DeviceKey);
                if (handle != IntPtr.Zero)
                {
                    Logger.LogInfo($"DDC: Found handle 0x{handle:X} for deviceKey {monitor.DeviceKey}");
                    return handle;
                }
            }

            // Fallback to direct handle from monitor object
            if (monitor.Handle != IntPtr.Zero)
            {
                Logger.LogInfo($"DDC: Using direct handle 0x{monitor.Handle:X} for monitor {monitor.Id}");
                return monitor.Handle;
            }

            // Last resort: try old handle mapping
            if (_monitorHandles.TryGetValue(monitor.Id, out var legacyHandle))
            {
                Logger.LogWarning($"DDC: Using legacy handle mapping 0x{legacyHandle:X} for monitor {monitor.Id}");
                return legacyHandle;
            }

            return IntPtr.Zero;
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
                // Release all physical monitor handles
                foreach (var handle in _monitorHandles.Values)
                {
                    try
                    {
                        if (handle != IntPtr.Zero)
                        {
                            DdcCiNative.DestroyPhysicalMonitor(handle);
                        }
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }

                _monitorHandles.Clear();
                _deviceKeyToHandleMap.Clear();
                _supportedVcpCodes.Clear();
                _disposed = true;
            }
        }
    }
}
