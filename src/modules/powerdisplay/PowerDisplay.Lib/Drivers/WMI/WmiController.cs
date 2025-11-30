// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using PowerDisplay.Common.Interfaces;
using PowerDisplay.Common.Models;
using WmiLight;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Common.Drivers.WMI
{
    /// <summary>
    /// WMI monitor controller for controlling internal laptop displays.
    /// Rewritten to use WmiLight library for Native AOT compatibility.
    /// </summary>
    public partial class WmiController : IMonitorController, IDisposable
    {
        private const string WmiNamespace = @"root\WMI";
        private const string BrightnessQueryClass = "WmiMonitorBrightness";
        private const string BrightnessMethodClass = "WmiMonitorBrightnessMethods";
        private const string MonitorIdClass = "WmiMonitorID";

        // Common WMI error codes for classification
        private const int WbemENotFound = unchecked((int)0x80041002);
        private const int WbemEAccessDenied = unchecked((int)0x80041003);
        private const int WbemEProviderFailure = unchecked((int)0x80041004);
        private const int WbemEInvalidQuery = unchecked((int)0x80041017);
        private const int WmiFeatureNotSupported = 0x1068;

        private bool _disposed;

        /// <summary>
        /// Classifies WMI exceptions into user-friendly error messages.
        /// </summary>
        private static MonitorOperationResult ClassifyWmiError(WmiException ex, string operation)
        {
            var hresult = ex.HResult;

            return hresult switch
            {
                WbemENotFound => MonitorOperationResult.Failure($"WMI class not found during {operation}. This feature may not be supported on your system.", hresult),
                WbemEAccessDenied => MonitorOperationResult.Failure($"Access denied during {operation}. Administrator privileges may be required.", hresult),
                WbemEProviderFailure => MonitorOperationResult.Failure($"WMI provider failure during {operation}. The display driver may not support this feature.", hresult),
                WbemEInvalidQuery => MonitorOperationResult.Failure($"Invalid WMI query during {operation}. This is likely a bug.", hresult),
                WmiFeatureNotSupported => MonitorOperationResult.Failure($"WMI brightness control not supported on this system during {operation}.", hresult),
                _ => MonitorOperationResult.Failure($"WMI error during {operation}: {ex.Message}", hresult),
            };
        }

        /// <summary>
        /// Determines if the WMI error is expected for systems without WMI brightness support.
        /// </summary>
        private static bool IsExpectedUnsupportedError(WmiException ex)
        {
            return ex.HResult == WmiFeatureNotSupported || ex.HResult == WbemENotFound;
        }

        public string Name => "WMI Monitor Controller (WmiLight)";

        /// <summary>
        /// Check if the specified monitor can be controlled
        /// </summary>
        public async Task<bool> CanControlMonitorAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);

            if (monitor.CommunicationMethod != "WMI")
            {
                return false;
            }

            return await Task.Run(
                () =>
                {
                    try
                    {
                        using var connection = new WmiConnection(WmiNamespace);
                        var query = $"SELECT * FROM {BrightnessQueryClass}";
                        var results = connection.CreateQuery(query).ToList();
                        return results.Count > 0;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"WMI CanControlMonitor check failed: {ex.Message}");
                        return false;
                    }
                },
                cancellationToken);
        }

        /// <summary>
        /// Get monitor brightness
        /// </summary>
        public async Task<BrightnessInfo> GetBrightnessAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);

            return await Task.Run(
                () =>
                {
                    try
                    {
                        using var connection = new WmiConnection(WmiNamespace);
                        var query = $"SELECT CurrentBrightness FROM {BrightnessQueryClass}";
                        var results = connection.CreateQuery(query);

                        foreach (var obj in results)
                        {
                            var currentBrightness = obj.GetPropertyValue<byte>("CurrentBrightness");
                            return new BrightnessInfo(currentBrightness, 0, 100);
                        }
                    }
                    catch (WmiException ex)
                    {
                        Logger.LogWarning($"WMI GetBrightness failed: {ex.Message} (HResult: 0x{ex.HResult:X})");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"WMI GetBrightness failed: {ex.Message}");
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
            ArgumentNullException.ThrowIfNull(monitor);

            // Validate brightness range
            brightness = Math.Clamp(brightness, 0, 100);

            return await Task.Run(
                () =>
                {
                    try
                    {
                    using var connection = new WmiConnection(WmiNamespace);
                    var query = $"SELECT * FROM {BrightnessMethodClass}";
                    var results = connection.CreateQuery(query);

                    foreach (var obj in results)
                    {
                        // Call WmiSetBrightness method
                        // Parameters: Timeout (uint32), Brightness (uint8)
                        // Note: WmiLight requires string values for method parameters
                        using (WmiMethod method = obj.GetMethod("WmiSetBrightness"))
                        using (WmiMethodParameters inParams = method.CreateInParameters())
                        {
                            inParams.SetPropertyValue("Timeout", "0");
                            inParams.SetPropertyValue("Brightness", brightness.ToString(CultureInfo.InvariantCulture));

                            uint result = obj.ExecuteMethod<uint>(
                                method,
                                inParams,
                                out WmiMethodParameters outParams);

                            // Check return value (0 indicates success)
                            if (result == 0)
                            {
                                return MonitorOperationResult.Success();
                            }
                            else
                            {
                                return MonitorOperationResult.Failure($"WMI method returned error code: {result}", (int)result);
                            }
                        }
                    }

                    return MonitorOperationResult.Failure("No WMI brightness methods found");
                }
                catch (UnauthorizedAccessException)
                {
                    return MonitorOperationResult.Failure("Access denied. Administrator privileges may be required.", 5);
                }
                catch (WmiException ex)
                {
                    return ClassifyWmiError(ex, "SetBrightness");
                }
                catch (Exception ex)
                {
                    return MonitorOperationResult.Failure($"Unexpected error during SetBrightness: {ex.Message}");
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
                    using var connection = new WmiConnection(WmiNamespace);

                    // First check if WMI brightness support is available
                    var brightnessQuery = $"SELECT * FROM {BrightnessQueryClass}";
                    var brightnessResults = connection.CreateQuery(brightnessQuery).ToList();

                    if (brightnessResults.Count == 0)
                    {
                        return monitors;
                    }

                    // Get monitor information
                    var idQuery = $"SELECT * FROM {MonitorIdClass}";
                    var idResults = connection.CreateQuery(idQuery).ToList();

                    var monitorInfos = new Dictionary<string, (string Name, string InstanceName)>();

                    foreach (var obj in idResults)
                    {
                        try
                        {
                            var instanceName = obj.GetPropertyValue<string>("InstanceName") ?? string.Empty;
                            var userFriendlyName = GetUserFriendlyName(obj) ?? "Internal Display";

                            if (!string.IsNullOrEmpty(instanceName))
                            {
                                monitorInfos[instanceName] = (userFriendlyName, instanceName);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Skip problematic entries
                            Logger.LogDebug($"Failed to parse WMI monitor info: {ex.Message}");
                        }
                    }

                    // Pre-fetch display devices once to avoid repeated Win32 API calls in the loop
                    var displayDevices = Drivers.DDC.DdcCiNative.GetAllDisplayDevices();

                    // Create monitor objects for each supported brightness instance
                    foreach (var obj in brightnessResults)
                    {
                        try
                        {
                            var instanceName = obj.GetPropertyValue<string>("InstanceName") ?? string.Empty;
                            var currentBrightness = obj.GetPropertyValue<byte>("CurrentBrightness");

                            var name = "Internal Display";
                            if (monitorInfos.TryGetValue(instanceName, out var info))
                            {
                                name = info.Name;
                            }

                            var monitor = new Monitor
                            {
                                Id = $"WMI_{instanceName}",
                                Name = name,

                                CurrentBrightness = currentBrightness,
                                MinBrightness = 0,
                                MaxBrightness = 100,
                                IsAvailable = true,
                                InstanceName = instanceName,
                                Capabilities = MonitorCapabilities.Brightness | MonitorCapabilities.Wmi,
                                ConnectionType = "Internal",
                                CommunicationMethod = "WMI",
                                Manufacturer = "Internal",
                                SupportsColorTemperature = false,
                                MonitorNumber = Utils.MonitorMatchingHelper.GetMonitorNumberFromWmiInstanceName(instanceName, displayDevices),
                            };

                            monitors.Add(monitor);
                        }
                        catch (Exception ex)
                        {
                            // Skip problematic monitors
                            Logger.LogWarning($"Failed to create monitor from WMI data: {ex.Message}");
                        }
                    }
                }
                catch (WmiException ex)
                {
                    // Return empty list instead of throwing exception
                    Logger.LogError($"WMI DiscoverMonitors failed: {ex.Message} (HResult: 0x{ex.HResult:X})");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"WMI DiscoverMonitors failed: {ex.Message}");
                }

                return monitors;
            },
                cancellationToken);
        }

        /// <summary>
        /// Get user-friendly name from WMI object
        /// </summary>
        private static string? GetUserFriendlyName(WmiObject monitorObject)
        {
            try
            {
                // WmiLight returns arrays as object arrays
                var userFriendlyNameObj = monitorObject.GetPropertyValue<object>("UserFriendlyName");

                if (userFriendlyNameObj is ushort[] userFriendlyName && userFriendlyName.Length > 0)
                {
                    // Convert UINT16 array to string
                    var chars = userFriendlyName
                        .Where(c => c != 0)
                        .Select(c => (char)c)
                        .ToArray();

                    if (chars.Length > 0)
                    {
                        return new string(chars).Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                // Ignore conversion errors
                Logger.LogDebug($"Failed to parse UserFriendlyName: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Check WMI service availability
        /// </summary>
        public static bool IsWmiAvailable()
        {
            try
            {
                using var connection = new WmiConnection(WmiNamespace);
                var query = $"SELECT * FROM {BrightnessQueryClass}";
                var results = connection.CreateQuery(query).ToList();
                return results.Count > 0;
            }
            catch (WmiException ex) when (IsExpectedUnsupportedError(ex))
            {
                // Expected on systems without WMI brightness support (desktops, some laptops)
                Logger.LogInfo("WMI brightness control not supported on this system (expected for desktops)");
                return false;
            }
            catch (WmiException ex)
            {
                // Unexpected WMI error - log with details for debugging
                Logger.LogWarning($"WMI availability check failed: {ex.Message} (HResult: 0x{ex.HResult:X})");
                return false;
            }
            catch (Exception ex)
            {
                // Unexpected non-WMI error
                Logger.LogDebug($"WMI availability check failed: {ex.Message}");
                return false;
            }
        }

        // Extended features not supported by WMI
        public Task<MonitorOperationResult> SetContrastAsync(Monitor monitor, int contrast, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(MonitorOperationResult.Failure("Contrast control not supported via WMI"));
        }

        public Task<MonitorOperationResult> SetVolumeAsync(Monitor monitor, int volume, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(MonitorOperationResult.Failure("Volume control not supported via WMI"));
        }

        public Task<BrightnessInfo> GetColorTemperatureAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(BrightnessInfo.Invalid);
        }

        public Task<MonitorOperationResult> SetColorTemperatureAsync(Monitor monitor, int colorTemperature, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(MonitorOperationResult.Failure("Color temperature control not supported via WMI"));
        }

        public Task<BrightnessInfo> GetInputSourceAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            // Input source switching not supported for internal displays
            return Task.FromResult(BrightnessInfo.Invalid);
        }

        public Task<MonitorOperationResult> SetInputSourceAsync(Monitor monitor, int inputSource, CancellationToken cancellationToken = default)
        {
            // Input source switching not supported for internal displays
            return Task.FromResult(MonitorOperationResult.Failure("Input source switching not supported via WMI"));
        }

        public Task<string> GetCapabilitiesStringAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(string.Empty);
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
                // WmiLight objects are automatically cleaned up, no specific cleanup needed here
                _disposed = true;
            }
        }
    }
}
