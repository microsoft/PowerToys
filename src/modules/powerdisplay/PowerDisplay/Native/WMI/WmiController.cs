// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Core.Interfaces;
using PowerDisplay.Core.Models;
using Monitor = PowerDisplay.Core.Models.Monitor;

namespace PowerDisplay.Native.WMI
{
    /// <summary>
    /// WMI monitor controller for controlling internal laptop displays
    /// </summary>
    public class WmiController : IMonitorController, IDisposable
    {
        private const string WmiNamespace = "root\\WMI";
        private const string BrightnessQueryClass = "WmiMonitorBrightness";
        private const string BrightnessMethodClass = "WmiMonitorBrightnessMethods";
        private const string MonitorIdClass = "WmiMonitorID";

        private bool _disposed = false;

        public string Name => "WMI Monitor Controller";
        public MonitorType SupportedType => MonitorType.Internal;

        /// <summary>
        /// Check if the specified monitor can be controlled
        /// </summary>
        public async Task<bool> CanControlMonitorAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            if (monitor.Type != MonitorType.Internal)
                return false;

            return await Task.Run(() =>
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(WmiNamespace, $"SELECT * FROM {BrightnessQueryClass}");
                    using var collection = searcher.Get();
                    return collection.Count > 0;
                }
                catch (Exception)
                {
                    return false;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Get monitor brightness
        /// </summary>
        public async Task<BrightnessInfo> GetBrightnessAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(WmiNamespace, 
                        $"SELECT CurrentBrightness FROM {BrightnessQueryClass}");
                    using var collection = searcher.Get();

                    foreach (ManagementObject obj in collection)
                    {
                        using (obj)
                        {
                            var currentBrightness = Convert.ToInt32(obj["CurrentBrightness"]);
                            return new BrightnessInfo(currentBrightness, 0, 100);
                        }
                    }
                }
                catch (Exception)
                {
                    // Silent failure
                }

                return BrightnessInfo.Invalid;
            }, cancellationToken);
        }

        /// <summary>
        /// Set monitor brightness
        /// </summary>
        public async Task<MonitorOperationResult> SetBrightnessAsync(Monitor monitor, int brightness, CancellationToken cancellationToken = default)
        {
            // Validate brightness range
            brightness = Math.Clamp(brightness, 0, 100);

            return await Task.Run(() =>
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(WmiNamespace, 
                        $"SELECT * FROM {BrightnessMethodClass}");
                    using var collection = searcher.Get();

                    foreach (ManagementObject obj in collection)
                    {
                        using (obj)
                        {
                            // Call WmiSetBrightness method
                            var result = obj.InvokeMethod("WmiSetBrightness", new object[] { 0, (byte)brightness });
                            
                            // Check return value (0 indicates success)
                            var returnValue = result != null ? Convert.ToInt32(result) : -1;
                            
                            if (returnValue == 0)
                            {
                                return MonitorOperationResult.Success();
                            }
                            else
                            {
                                return MonitorOperationResult.Failure($"WMI method returned error code: {returnValue}", returnValue);
                            }
                        }
                    }

                    return MonitorOperationResult.Failure("No WMI brightness methods found");
                }
                catch (UnauthorizedAccessException)
                {
                    return MonitorOperationResult.Failure("Access denied. Administrator privileges may be required.", 5);
                }
                catch (ManagementException ex)
                {
                    return MonitorOperationResult.Failure($"WMI error: {ex.Message}", (int)ex.ErrorCode);
                }
                catch (Exception ex)
                {
                    return MonitorOperationResult.Failure($"Unexpected error: {ex.Message}");
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Discover supported monitors
        /// </summary>
        public async Task<IEnumerable<Monitor>> DiscoverMonitorsAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var monitors = new List<Monitor>();

                try
                {
                    // First check if WMI brightness support is available
                    using var brightnessSearcher = new ManagementObjectSearcher(WmiNamespace, 
                        $"SELECT * FROM {BrightnessQueryClass}");
                    using var brightnessCollection = brightnessSearcher.Get();

                    if (brightnessCollection.Count == 0)
                        return monitors;

                    // Get monitor information
                    using var idSearcher = new ManagementObjectSearcher(WmiNamespace, 
                        $"SELECT * FROM {MonitorIdClass}");
                    using var idCollection = idSearcher.Get();

                    var monitorInfos = new Dictionary<string, (string Name, string InstanceName)>();

                    foreach (ManagementObject obj in idCollection)
                    {
                        using (obj)
                        {
                            try
                            {
                                var instanceName = obj["InstanceName"]?.ToString() ?? "";
                                var userFriendlyName = GetUserFriendlyName(obj) ?? "Internal Display";

                                if (!string.IsNullOrEmpty(instanceName))
                                {
                                    monitorInfos[instanceName] = (userFriendlyName, instanceName);
                                }
                            }
                            catch
                            {
                                // Skip problematic entries
                            }
                        }
                    }

                    // Create monitor objects for each supported brightness instance
                    foreach (ManagementObject obj in brightnessCollection)
                    {
                        using (obj)
                        {
                            try
                            {
                                var instanceName = obj["InstanceName"]?.ToString() ?? "";
                                var currentBrightness = Convert.ToInt32(obj["CurrentBrightness"]);

                                var name = "Internal Display";
                                if (monitorInfos.TryGetValue(instanceName, out var info))
                                {
                                    name = info.Name;
                                }

                                var monitor = new Monitor
                                {
                                    Id = $"WMI_{instanceName}",
                                    Name = name,
                                    Type = MonitorType.Internal,
                                    CurrentBrightness = currentBrightness,
                                    MinBrightness = 0,
                                    MaxBrightness = 100,
                                    IsAvailable = true,
                                    InstanceName = instanceName,
                                    Capabilities = MonitorCapabilities.Brightness | MonitorCapabilities.Wmi,
                                    ConnectionType = "Internal",
                                    CommunicationMethod = "WMI",
                                    Manufacturer = "Internal",
                                    SupportsColorTemperature = false // Internal monitors don't support DDC/CI color temperature
                                };

                                monitors.Add(monitor);
                            }
                            catch
                            {
                                // Skip problematic monitors
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Return empty list instead of throwing exception
                }

                return monitors;
            }, cancellationToken);
        }

        /// <summary>
        /// Validate monitor connection status
        /// </summary>
        public async Task<bool> ValidateConnectionAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Try to read current brightness to validate connection
                    using var searcher = new ManagementObjectSearcher(WmiNamespace, 
                        $"SELECT CurrentBrightness FROM {BrightnessQueryClass} WHERE InstanceName='{monitor.InstanceName}'");
                    using var collection = searcher.Get();
                    return collection.Count > 0;
                }
                catch
                {
                    return false;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Get user-friendly name
        /// </summary>
        private static string? GetUserFriendlyName(ManagementObject monitorObject)
        {
            try
            {
                var userFriendlyName = monitorObject["UserFriendlyName"] as ushort[];
                if (userFriendlyName != null && userFriendlyName.Length > 0)
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
            catch
            {
                // Ignore conversion errors
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
                using var searcher = new ManagementObjectSearcher(WmiNamespace, 
                    $"SELECT * FROM {BrightnessQueryClass}");
                using var collection = searcher.Get();
                return collection.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if administrator privileges are required
        /// </summary>
        public static bool RequiresElevation()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(WmiNamespace, 
                    $"SELECT * FROM {BrightnessMethodClass}");
                using var collection = searcher.Get();

                foreach (ManagementObject obj in collection)
                {
                    using (obj)
                    {
                        // Try to call method to check permissions
                        try
                        {
                            obj.InvokeMethod("WmiSetBrightness", new object[] { 0, 50 });
                            return false; // If successful, no elevation required
                        }
                        catch (UnauthorizedAccessException)
                        {
                            return true; // Administrator privileges required
                        }
                        catch
                        {
                            // Other errors may not be permission issues
                            return false;
                        }
                    }
                }
            }
            catch
            {
                // Cannot determine, assume privileges required
                return true;
            }

            return false;
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
                // WMI objects are automatically cleaned up, no specific cleanup needed here
                _disposed = true;
            }
        }
    }
}