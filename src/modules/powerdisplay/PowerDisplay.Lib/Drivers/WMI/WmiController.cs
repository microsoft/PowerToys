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
using PowerDisplay.Models;
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
        /// Discover the panels the WMI brightness provider exposes, pairing each against the
        /// active-display inventory. A display present in <c>WmiMonitorBrightness</c> is treated
        /// as an internal panel by <see cref="MonitorManager"/>, regardless of the OutputTechnology
        /// the active GPU reports — this is what lets a built-in panel driven by the discrete GPU
        /// (reported as DisplayPort-External) still be found. See issue #48587.
        /// </summary>
        /// <param name="targets">The full active-display inventory from QueryDisplayConfig.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>WMI-managed monitors (those present in both WMI and the inventory).</returns>
        public async Task<IEnumerable<Monitor>> DiscoverMonitorsAsync(
            IReadOnlyList<MonitorDisplayInfo> targets,
            CancellationToken cancellationToken = default)
        {
            // No active displays at all — nothing to pair WMI brightness instances against.
            if (targets.Count == 0)
            {
                return Enumerable.Empty<Monitor>();
            }

            return await Task.Run(
                () =>
                {
                var monitors = new List<Monitor>();

                // Key the inventory by canonical Monitor.Id (FromDevicePath). A WMI InstanceName
                // reduces to the same Id via FromInstanceName, so pairing is a single exact lookup
                // that also disambiguates dual-internal-panel devices without a separate pass.
                var byId = targets
                    .Select(t => (Id: MonitorIdentity.FromDevicePath(t.DevicePath), Info: t))
                    .Where(p => !string.IsNullOrEmpty(p.Id))
                    .ToDictionary(p => p.Id, p => p.Info, MonitorIdComparer.Instance);

                try
                {
                    using var connection = new WmiConnection(WmiNamespace);

                    // System-wide query: returns every panel the driver exposes for WMI
                    // brightness, regardless of which GPU currently drives it.
                    var brightnessQuery = $"SELECT InstanceName, CurrentBrightness FROM {BrightnessQueryClass}";
                    var brightnessResults = connection.CreateQuery(brightnessQuery).ToList();

                    // Check cancellation per iteration: WMI work inside Task.Run doesn't
                    // respond to the token once the loop has started.
                    foreach (var obj in brightnessResults)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            var instanceName = obj.GetPropertyValue<string>("InstanceName") ?? string.Empty;
                            var currentBrightness = obj.GetPropertyValue<byte>("CurrentBrightness");

                            // Pair on the canonical Monitor.Id. A miss means this WMI instance is
                            // not an active display (e.g. a disconnected panel still cached by the
                            // provider) — skip it.
                            var lookupId = MonitorIdentity.FromInstanceName(instanceName);
                            if (string.IsNullOrEmpty(lookupId) || !byId.TryGetValue(lookupId, out var displayInfo))
                            {
                                Logger.LogInfo(
                                    $"WMI exposed brightness for instance '{instanceName}' with no matching " +
                                    "active display — skipping");
                                continue;
                            }

                            // Derive the Id from the matched entry's DevicePath, not the
                            // reconstructed lookupId. The persisted Monitor.Id ALWAYS comes from this
                            // single source (FromDevicePath), so a WMI panel's Id stays byte-identical
                            // to the DDC route and to prior releases. FromInstanceName is only the
                            // lookup key; every Id comparison/key elsewhere goes through MonitorIdComparer
                            // (case-insensitive), so an InstanceName/DevicePath casing difference can
                            // never orphan per-monitor settings.
                            // Name is left blank: MonitorViewModel injects a localized
                            // "Built-in Display" string for internal displays.
                            var monitor = new Monitor
                            {
                                Id = MonitorIdentity.FromDevicePath(displayInfo.DevicePath),
                                Name = string.Empty,
                                CurrentBrightness = currentBrightness,
                                InstanceName = instanceName,
                                Capabilities = MonitorCapabilities.Brightness | MonitorCapabilities.Wmi,
                                CommunicationMethod = "WMI",
                                SupportsColorTemperature = false,
                                MonitorNumber = displayInfo.MonitorNumber,
                                GdiDeviceName = displayInfo.GdiDeviceName ?? string.Empty,
                            };

                            monitor.ReadValues |= MonitorReadFlags.Brightness;

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
                    // On a system with no WMI-controllable panel the provider may be absent or
                    // throw 0x1068 ("feature not supported"); those displays are handled by
                    // DDC/CI instead, so this is informational rather than an error.
                    Logger.LogInfo($"WMI brightness query unavailable: {ex.Message} (HResult: 0x{ex.HResult:X})");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Logger.LogError($"WMI DiscoverMonitors failed: {ex.Message}");
                }

                return monitors;
            },
                cancellationToken);
        }

        public void Dispose()
        {
            // WmiLight objects are created per-operation and disposed immediately via using statements.
            // No instance-level resources require cleanup.
            GC.SuppressFinalize(this);
        }
    }
}
