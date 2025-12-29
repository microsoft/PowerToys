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
using PowerDisplay.Common.Utils;
using WmiLight;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Common.Drivers.WMI
{
    /// <summary>
    /// WMI monitor controller for controlling internal laptop displays.
    /// </summary>
    public partial class WmiController : IMonitorController, IDisposable
    {
        private const string WmiNamespace = @"root\WMI";
        private const string BrightnessQueryClass = "WmiMonitorBrightness";
        private const string BrightnessMethodClass = "WmiMonitorBrightnessMethods";

        // Common WMI error codes for classification
        private const int WbemENotFound = unchecked((int)0x80041002);
        private const int WbemEAccessDenied = unchecked((int)0x80041003);
        private const int WbemEProviderFailure = unchecked((int)0x80041004);
        private const int WbemEInvalidQuery = unchecked((int)0x80041017);
        private const int WmiFeatureNotSupported = 0x1068;

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
        /// Escape special characters in WMI query strings.
        /// WMI requires backslashes and single quotes to be escaped in WHERE clauses.
        /// See: https://learn.microsoft.com/en-us/windows/win32/wmisdk/wql-sql-for-wmi
        /// </summary>
        /// <param name="value">The string value to escape.</param>
        /// <returns>The escaped string safe for use in WMI queries.</returns>
        private static string EscapeWmiString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            // WMI requires backslashes and single quotes to be escaped in WHERE clauses
            // Backslash must be escaped first to avoid double-escaping the quote's backslash
            return value.Replace("\\", "\\\\").Replace("'", "\\'");
        }

        /// <summary>
        /// Extract hardware ID from WMI InstanceName.
        /// InstanceName format: "DISPLAY\BOE0900\4&amp;10fd3ab1&amp;0&amp;UID265988_0"
        /// Returns the second segment (e.g., "BOE0900") which is the manufacturer+product code.
        /// </summary>
        /// <param name="instanceName">The WMI InstanceName.</param>
        /// <returns>The EDID ID extracted from the InstanceName, or empty string if extraction fails.</returns>
        private static string ExtractEdidIdFromInstanceName(string instanceName)
        {
            if (string.IsNullOrEmpty(instanceName))
            {
                return string.Empty;
            }

            // Split by backslash: ["DISPLAY", "BOE0900", "4&10fd3ab1&0&UID265988_0"]
            var parts = instanceName.Split('\\');
            if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[1]))
            {
                // Return the second part (e.g., "BOE0900")
                return parts[1];
            }

            return string.Empty;
        }

        /// <summary>
        /// Build a WMI query filtered by monitor instance name.
        /// </summary>
        /// <param name="wmiClass">The WMI class to query.</param>
        /// <param name="instanceName">The monitor instance name to filter by.</param>
        /// <param name="selectClause">Optional SELECT clause fields (defaults to "*").</param>
        /// <returns>The formatted WMI query string.</returns>
        private static string BuildInstanceNameQuery(string wmiClass, string instanceName, string selectClause = "*")
        {
            var escapedInstanceName = EscapeWmiString(instanceName);
            return $"SELECT {selectClause} FROM {wmiClass} WHERE InstanceName = '{escapedInstanceName}'";
        }

        /// <summary>
        /// Get MonitorDisplayInfo from dictionary by matching EdidId.
        /// Uses QueryDisplayConfig path index which matches Windows Display Settings "Identify" feature.
        /// </summary>
        /// <param name="edidId">The EDID ID to match (e.g., "LEN4038", "BOE0900").</param>
        /// <param name="monitorDisplayInfos">Dictionary of monitor display info from QueryDisplayConfig.</param>
        /// <returns>MonitorDisplayInfo if found, or null if not found.</returns>
        private static Drivers.DDC.MonitorDisplayInfo? GetMonitorDisplayInfoByEdidId(string edidId, Dictionary<string, Drivers.DDC.MonitorDisplayInfo> monitorDisplayInfos)
        {
            if (string.IsNullOrEmpty(edidId) || monitorDisplayInfos == null || monitorDisplayInfos.Count == 0)
            {
                return null;
            }

            var match = monitorDisplayInfos.Values.FirstOrDefault(
                v => edidId.Equals(v.EdidId, StringComparison.OrdinalIgnoreCase));

            // Check if match was found (struct default has null/empty EdidId)
            if (!string.IsNullOrEmpty(match.EdidId))
            {
                return match;
            }

            Logger.LogWarning($"WMI: Could not find MonitorDisplayInfo for EdidId '{edidId}'");
            return null;
        }

        public string Name => "WMI Monitor Controller";

        /// <summary>
        /// Get monitor brightness
        /// </summary>
        public async Task<VcpFeatureValue> GetBrightnessAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);

            return await Task.Run(
                () =>
                {
                    try
                    {
                        using var connection = new WmiConnection(WmiNamespace);
                        var query = BuildInstanceNameQuery(BrightnessQueryClass, monitor.InstanceName, "CurrentBrightness");
                        var results = connection.CreateQuery(query);

                        foreach (var obj in results)
                        {
                            var currentBrightness = obj.GetPropertyValue<byte>("CurrentBrightness");
                            return new VcpFeatureValue(currentBrightness, 0, 100);
                        }

                        // No match found - monitor may have been disconnected
                    }
                    catch (WmiException ex)
                    {
                        Logger.LogWarning($"WMI GetBrightness failed: {ex.Message} (HResult: 0x{ex.HResult:X})");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"WMI GetBrightness failed: {ex.Message}");
                    }

                    return VcpFeatureValue.Invalid;
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
                        var query = BuildInstanceNameQuery(BrightnessMethodClass, monitor.InstanceName);
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

                                return MonitorOperationResult.Failure($"WMI method returned error code: {result}", (int)result);
                            }
                        }

                        // No match found - monitor may have been disconnected
                        Logger.LogWarning($"WMI SetBrightness: No monitor found with InstanceName '{monitor.InstanceName}'");
                        return MonitorOperationResult.Failure($"No WMI brightness method found for monitor '{monitor.InstanceName}'");
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
        /// Discover supported monitors.
        /// WMI brightness control is typically only available on internal laptop displays,
        /// which don't have meaningful UserFriendlyName in WmiMonitorID, so we use "Built-in Display".
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

                    // Query WMI brightness support - only internal displays typically support this
                    var brightnessQuery = $"SELECT InstanceName, CurrentBrightness FROM {BrightnessQueryClass}";
                    var brightnessResults = connection.CreateQuery(brightnessQuery).ToList();

                    if (brightnessResults.Count == 0)
                    {
                        return monitors;
                    }

                    // Get MonitorDisplayInfo from QueryDisplayConfig - this provides the correct monitor numbers
                    var monitorDisplayInfos = Drivers.DDC.DdcCiNative.GetAllMonitorDisplayInfo();

                    // Create monitor objects for each supported brightness instance
                    foreach (var obj in brightnessResults)
                    {
                        try
                        {
                            var instanceName = obj.GetPropertyValue<string>("InstanceName") ?? string.Empty;
                            var currentBrightness = obj.GetPropertyValue<byte>("CurrentBrightness");

                            // Extract EDID ID from InstanceName
                            // e.g., "DISPLAY\LEN4038\4&40f4dee&0&UID8388688_0" -> "LEN4038"
                            var edidId = ExtractEdidIdFromInstanceName(instanceName);

                            // Get MonitorDisplayInfo from QueryDisplayConfig by matching EDID ID
                            // This provides MonitorNumber and GdiDeviceName for display settings APIs
                            var displayInfo = GetMonitorDisplayInfoByEdidId(edidId, monitorDisplayInfos);
                            int monitorNumber = displayInfo?.MonitorNumber ?? 0;
                            string gdiDeviceName = displayInfo?.GdiDeviceName ?? string.Empty;

                            // Generate unique ID: "WMI_{EdidId}_{MonitorNumber}"
                            string uniqueId = !string.IsNullOrEmpty(edidId)
                                ? $"WMI_{edidId}_{monitorNumber}"
                                : $"WMI_Unknown_{monitorNumber}";

                            // Get display name from PnP manufacturer ID (e.g., "Lenovo Built-in Display")
                            var displayName = PnpIdHelper.GetBuiltInDisplayName(edidId);

                            var monitor = new Monitor
                            {
                                Id = uniqueId,
                                Name = displayName,
                                CurrentBrightness = currentBrightness,
                                MinBrightness = 0,
                                MaxBrightness = 100,
                                IsAvailable = true,
                                InstanceName = instanceName,
                                Capabilities = MonitorCapabilities.Brightness | MonitorCapabilities.Wmi,
                                CommunicationMethod = "WMI",
                                SupportsColorTemperature = false,
                                MonitorNumber = monitorNumber,
                                GdiDeviceName = gdiDeviceName,
                            };

                            monitors.Add(monitor);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"Failed to create monitor from WMI data: {ex.Message}");
                        }
                    }
                }
                catch (WmiException ex)
                {
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

        // Extended features not supported by WMI
        public Task<MonitorOperationResult> SetContrastAsync(Monitor monitor, int contrast, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(MonitorOperationResult.Failure("Contrast control not supported via WMI"));
        }

        public Task<MonitorOperationResult> SetVolumeAsync(Monitor monitor, int volume, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(MonitorOperationResult.Failure("Volume control not supported via WMI"));
        }

        public Task<VcpFeatureValue> GetColorTemperatureAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(VcpFeatureValue.Invalid);
        }

        public Task<MonitorOperationResult> SetColorTemperatureAsync(Monitor monitor, int colorTemperature, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(MonitorOperationResult.Failure("Color temperature control not supported via WMI"));
        }

        public Task<VcpFeatureValue> GetInputSourceAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            // Input source switching not supported for internal displays
            return Task.FromResult(VcpFeatureValue.Invalid);
        }

        public Task<MonitorOperationResult> SetInputSourceAsync(Monitor monitor, int inputSource, CancellationToken cancellationToken = default)
        {
            // Input source switching not supported for internal displays
            return Task.FromResult(MonitorOperationResult.Failure("Input source switching not supported via WMI"));
        }

        public void Dispose()
        {
            // WmiLight objects are created per-operation and disposed immediately via using statements.
            // No instance-level resources require cleanup.
            GC.SuppressFinalize(this);
        }
    }
}
