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
using PowerDisplay.Common.Drivers;
using PowerDisplay.Common.Interfaces;
using PowerDisplay.Common.Models;
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
        /// Extracts the PnP hardware-instance segment from a WMI InstanceName so it can
        /// be substring-matched against MonitorDisplayInfo.DevicePath.
        /// InstanceName form: "DISPLAY\BOE0900\4&amp;40f4dee&amp;0&amp;UID8388688_0"
        /// DevicePath  form: "\\?\DISPLAY#BOE0900#4&amp;40f4dee&amp;0&amp;UID8388688#{GUID}"
        /// Returns "BOE0900#4&amp;40f4dee&amp;0&amp;UID8388688" — the unique PnP hardware ID
        /// segment shared by both representations. Returns empty if extraction fails.
        /// </summary>
        private static string ExtractPnpHardwareKey(string instanceName)
        {
            if (string.IsNullOrEmpty(instanceName))
            {
                return string.Empty;
            }

            // Split by backslash: ["DISPLAY", "BOE0900", "4&40f4dee&0&UID8388688_0"]
            var parts = instanceName.Split('\\');
            if (parts.Length < 3 || string.IsNullOrEmpty(parts[1]) || string.IsNullOrEmpty(parts[2]))
            {
                return string.Empty;
            }

            // The third segment may carry a "_N" WMI-instance suffix (e.g. "..._0"); strip it.
            var instanceSegment = parts[2];
            var underscore = instanceSegment.LastIndexOf('_');
            if (underscore > 0)
            {
                instanceSegment = instanceSegment[..underscore];
            }

            // DevicePath joins the segments with '#', so produce the same form.
            return $"{parts[1]}#{instanceSegment}";
        }

        /// <summary>
        /// Picks the MonitorDisplayInfo whose DevicePath uniquely matches the WMI InstanceName.
        /// Used to disambiguate dual-internal-panel devices (e.g. foldable laptops) where
        /// two physical panels share the same EdidId but differ in PnP UID.
        /// Falls back to the first candidate if exact disambiguation isn't possible — the
        /// caller logs a warning so the ambiguity is observable in field telemetry.
        /// </summary>
        private static MonitorDisplayInfo DisambiguateByInstanceName(
            List<MonitorDisplayInfo> candidates,
            string instanceName)
        {
            var pnpKey = ExtractPnpHardwareKey(instanceName);
            if (!string.IsNullOrEmpty(pnpKey))
            {
                foreach (var info in candidates)
                {
                    if (!string.IsNullOrEmpty(info.DevicePath)
                        && info.DevicePath.Contains(pnpKey, StringComparison.OrdinalIgnoreCase))
                    {
                        return info;
                    }
                }
            }

            Logger.LogWarning(
                $"WMI: Could not disambiguate instance '{instanceName}' among " +
                $"{candidates.Count} internal targets sharing EdidId — using first match");
            return candidates[0];
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

                                using (outParams)
                                {
                                    // Check return value (0 indicates success)
                                    if (result == 0)
                                    {
                                        return MonitorOperationResult.Success();
                                    }

                                    return MonitorOperationResult.Failure($"WMI method returned error code: {result}", (int)result);
                                }
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
        /// WMI brightness control is typically only available on internal laptop displays.
        /// The monitor Name is left blank here; the ViewModel layer fills in a localized
        /// "Built-in Display" string so it can be translated for the user's UI language.
        /// </summary>
        /// <param name="targets">Internal-only display targets (pre-filtered by MonitorManager Phase 0).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of WMI-managed internal monitors.</returns>
        public async Task<IEnumerable<Monitor>> DiscoverMonitorsAsync(
            IReadOnlyList<MonitorDisplayInfo> targets,
            CancellationToken cancellationToken = default)
        {
            // Short-circuit: with no internal displays classified there is nothing for WMI
            // brightness control to do. Skipping the query also avoids the WmiMonitorBrightness
            // class throwing WMI 0x1068 ("feature not supported") on systems without an
            // internal panel — that exception is otherwise caught and logged as Error.
            if (targets.Count == 0)
            {
                Logger.LogInfo("WMI: No internal displays classified — skipping WmiMonitorBrightness query");
                return Enumerable.Empty<Monitor>();
            }

            return await Task.Run(
                () =>
                {
                var monitors = new List<Monitor>();

                // Build EdidId -> List<MonitorDisplayInfo> lookup. List-valued because dual-internal-panel
                // devices (e.g. Yoga Book 9i, Zenbook Duo) can ship two identical panels with the same
                // EdidId; in that case the WMI loop disambiguates by InstanceName UID.
                var monitorDisplayInfos = targets
                    .Where(t => !string.IsNullOrEmpty(t.EdidId))
                    .GroupBy(t => t.EdidId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

                // Track which internal targets (keyed by DevicePath, the unique target id) were
                // observed via WmiMonitorBrightness so we can warn about any that were classified
                // internal but not exposed.
                var seenDevicePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    using var connection = new WmiConnection(WmiNamespace);

                    // Query WMI brightness support - only internal displays typically support this
                    var brightnessQuery = $"SELECT InstanceName, CurrentBrightness FROM {BrightnessQueryClass}";
                    var brightnessResults = connection.CreateQuery(brightnessQuery).ToList();

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

                            // Look up the matching MonitorDisplayInfo in the internal-only targets list.
                            if (string.IsNullOrEmpty(edidId) || !monitorDisplayInfos.TryGetValue(edidId, out var candidates))
                            {
                                Logger.LogWarning(
                                    $"WMI returned brightness for EdidId={edidId} but it was not classified as internal — skipping");
                                continue;
                            }

                            // One target is the common case; multiple means dual-internal-panel — disambiguate.
                            var displayInfo = candidates.Count == 1
                                ? candidates[0]
                                : DisambiguateByInstanceName(candidates, instanceName);

                            if (!string.IsNullOrEmpty(displayInfo.DevicePath))
                            {
                                seenDevicePaths.Add(displayInfo.DevicePath);
                            }

                            int monitorNumber = displayInfo.MonitorNumber;
                            string gdiDeviceName = displayInfo.GdiDeviceName ?? string.Empty;

                            // Generate stable monitor Id from the DevicePath (Windows PnP instance path).
                            // If DevicePath is missing we cannot produce a stable Id, so the
                            // monitor is skipped — better to drop one entry than to persist
                            // settings under a key that won't survive the next reboot.
                            if (string.IsNullOrEmpty(displayInfo.DevicePath))
                            {
                                Logger.LogWarning(
                                    $"WMI: Skipping monitor (instance='{instanceName}', edid='{edidId}', monitorNumber={monitorNumber}) — DevicePath unavailable, cannot generate stable Id");
                                continue;
                            }

                            string uniqueId = MonitorIdentity.FromDevicePath(displayInfo.DevicePath);

                            // Name is left blank: MonitorViewModel injects a localized
                            // "Built-in Display" string for internal displays.
                            var monitor = new Monitor
                            {
                                Id = uniqueId,
                                Name = string.Empty,
                                CurrentBrightness = currentBrightness,
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

                // Post-loop: warn about every internal target the driver didn't expose via WMI.
                // Keyed on DevicePath (per-target unique) so dual-internal-panel devices report
                // each missing panel individually instead of being collapsed by EdidId.
                foreach (var target in targets)
                {
                    if (string.IsNullOrEmpty(target.DevicePath) || seenDevicePaths.Contains(target.DevicePath))
                    {
                        continue;
                    }

                    Logger.LogWarning(
                        $"Internal display {target.EdidId} (\"{target.FriendlyName}\") was classified internal " +
                        "but is not exposed via WmiMonitorBrightness — driver may not support brightness control");
                }

                return monitors;
            },
                cancellationToken);
        }

        // Extended features not supported by WMI (internal laptop displays expose only brightness via WMI).
        public Task<VcpFeatureValue> GetContrastAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(VcpFeatureValue.Invalid);
        }

        public Task<MonitorOperationResult> SetContrastAsync(Monitor monitor, int contrast, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(MonitorOperationResult.Failure("Contrast control not supported via WMI"));
        }

        public Task<VcpFeatureValue> GetVolumeAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(VcpFeatureValue.Invalid);
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

        public Task<MonitorOperationResult> SetPowerStateAsync(Monitor monitor, int powerState, CancellationToken cancellationToken = default)
        {
            // Power state control not supported for internal displays via WMI
            return Task.FromResult(MonitorOperationResult.Failure("Power state control not supported via WMI"));
        }

        public Task<VcpFeatureValue> GetPowerStateAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            // Power state control not supported for internal displays via WMI
            return Task.FromResult(VcpFeatureValue.Invalid);
        }

        public void Dispose()
        {
            // WmiLight objects are created per-operation and disposed immediately via using statements.
            // No instance-level resources require cleanup.
            GC.SuppressFinalize(this);
        }
    }
}
