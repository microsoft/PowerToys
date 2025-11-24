// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using PowerDisplay.Common.Drivers;
using PowerDisplay.Common.Drivers.DDC;
using PowerDisplay.Common.Drivers.WMI;
using PowerDisplay.Common.Interfaces;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;
using PowerDisplay.Core.Interfaces;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Core
{
    /// <summary>
    /// Monitor manager for unified control of all monitors
    /// No interface abstraction - KISS principle (only one implementation needed)
    /// </summary>
    public partial class MonitorManager : IDisposable
    {
        private readonly List<Monitor> _monitors = new();
        private readonly Dictionary<string, Monitor> _monitorLookup = new();
        private readonly List<IMonitorController> _controllers = new();
        private readonly SemaphoreSlim _discoveryLock = new(1, 1);
        private bool _disposed;

        public IReadOnlyList<Monitor> Monitors => _monitors.AsReadOnly();

        public event EventHandler<MonitorListChangedEventArgs>? MonitorsChanged;

        public MonitorManager()
        {
            // Initialize controllers
            InitializeControllers();
        }

        /// <summary>
        /// Initialize controllers
        /// </summary>
        private void InitializeControllers()
        {
            try
            {
                // DDC/CI controller (external monitors)
                _controllers.Add(new DdcCiController());
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to initialize DDC/CI controller: {ex.Message}");
            }

            try
            {
                // WMI controller (internal monitors)
                // First check if WMI is available
                if (WmiController.IsWmiAvailable())
                {
                    _controllers.Add(new WmiController());
                }
                else
                {
                    Logger.LogWarning("WMI brightness control not available on this system");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to initialize WMI controller: {ex.Message}");
            }
        }

        /// <summary>
        /// Discover all monitors
        /// </summary>
        public async Task<IReadOnlyList<Monitor>> DiscoverMonitorsAsync(CancellationToken cancellationToken = default)
        {
            await _discoveryLock.WaitAsync(cancellationToken);

            try
            {
                var oldMonitors = _monitors.ToList();
                var newMonitors = new List<Monitor>();

                // Discover monitors supported by all controllers in parallel
                var discoveryTasks = _controllers.Select(async controller =>
                {
                    try
                    {
                        var monitors = await controller.DiscoverMonitorsAsync(cancellationToken);
                        return (Controller: controller, Monitors: monitors.ToList());
                    }
                    catch (Exception ex)
                    {
                        // If a controller fails, log the error and return empty list
                        Logger.LogWarning($"Controller {controller.Name} discovery failed: {ex.Message}");
                        return (Controller: controller, Monitors: new List<Monitor>());
                    }
                });

                var results = await Task.WhenAll(discoveryTasks);

                // Collect all discovered monitors
                var allMonitors = new List<Monitor>();

                foreach (var (controller, monitors) in results)
                {
                    // Initialize monitors in parallel
                    var initTasks = monitors.Select(async monitor =>
                    {
                        // Verify if monitor can be controlled
                        if (await controller.CanControlMonitorAsync(monitor, cancellationToken))
                        {
                            // Get current brightness
                            try
                            {
                                var brightnessInfo = await controller.GetBrightnessAsync(monitor, cancellationToken);
                                if (brightnessInfo.IsValid)
                                {
                                    monitor.CurrentBrightness = brightnessInfo.ToPercentage();
                                    monitor.MinBrightness = brightnessInfo.Minimum;
                                    monitor.MaxBrightness = brightnessInfo.Maximum;
                                }
                            }
                            catch (Exception ex)
                            {
                                // If unable to get brightness, use default values
                                Logger.LogWarning($"Failed to get brightness for monitor {monitor.Id}: {ex.Message}");
                            }

                            // Get capabilities for DDC/CI monitors
                            // Check by CommunicationMethod instead of Type
                            if (monitor.CommunicationMethod?.Contains("DDC", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                try
                                {
                                    Logger.LogInfo($"Getting capabilities for monitor {monitor.Id}");
                                    var capsString = await controller.GetCapabilitiesStringAsync(monitor, cancellationToken);

                                    if (!string.IsNullOrEmpty(capsString))
                                    {
                                        monitor.CapabilitiesRaw = capsString;

                                        // Parse capabilities
                                        monitor.VcpCapabilitiesInfo = Common.Utils.VcpCapabilitiesParser.Parse(capsString);

                                        Logger.LogInfo($"Successfully parsed capabilities for {monitor.Id}: {monitor.VcpCapabilitiesInfo.SupportedVcpCodes.Count} VCP codes");

                                        // Update capability flags based on parsed VCP codes
                                        if (monitor.VcpCapabilitiesInfo.SupportedVcpCodes.Count > 0)
                                        {
                                            UpdateMonitorCapabilitiesFromVcp(monitor);
                                        }
                                    }
                                    else
                                    {
                                        Logger.LogWarning($"Got empty capabilities string for monitor {monitor.Id}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogWarning($"Failed to get capabilities for monitor {monitor.Id}: {ex.Message}");

                                    // Continue without capabilities - not critical
                                }
                            }

                            return monitor;
                        }

                        return null;
                    });

                    var initializedMonitors = await Task.WhenAll(initTasks);
                    var validMonitors = initializedMonitors.Where(m => m != null).Cast<Monitor>();
                    newMonitors.AddRange(validMonitors);
                }

                // Update monitor list and lookup dictionary
                _monitors.Clear();
                _monitorLookup.Clear();
                _monitors.AddRange(newMonitors);
                foreach (var monitor in newMonitors)
                {
                    _monitorLookup[monitor.Id] = monitor;
                }

                // Trigger change events
                var addedMonitors = newMonitors.Where(m => !oldMonitors.Any(o => o.Id == m.Id)).ToList();
                var removedMonitors = oldMonitors.Where(o => !newMonitors.Any(m => m.Id == o.Id)).ToList();

                if (addedMonitors.Count > 0 || removedMonitors.Count > 0)
                {
                    MonitorsChanged?.Invoke(this, new MonitorListChangedEventArgs(
                        addedMonitors.AsReadOnly(),
                        removedMonitors.AsReadOnly(),
                        _monitors.AsReadOnly()));
                }

                return _monitors.AsReadOnly();
            }
            finally
            {
                _discoveryLock.Release();
            }
        }

        /// <summary>
        /// Get brightness of the specified monitor
        /// </summary>
        public async Task<BrightnessInfo> GetBrightnessAsync(string monitorId, CancellationToken cancellationToken = default)
        {
            var monitor = GetMonitor(monitorId);
            if (monitor == null)
            {
                return BrightnessInfo.Invalid;
            }

            var controller = await GetControllerForMonitorAsync(monitor, cancellationToken);
            if (controller == null)
            {
                return BrightnessInfo.Invalid;
            }

            try
            {
                var brightnessInfo = await controller.GetBrightnessAsync(monitor, cancellationToken);

                // Update cached brightness value
                if (brightnessInfo.IsValid)
                {
                    monitor.UpdateStatus(brightnessInfo.ToPercentage(), true);
                }

                return brightnessInfo;
            }
            catch (Exception ex)
            {
                // Mark monitor as unavailable
                Logger.LogError($"Failed to get brightness for monitor {monitorId}: {ex.Message}");
                monitor.IsAvailable = false;
                return BrightnessInfo.Invalid;
            }
        }

        /// <summary>
        /// Set brightness of the specified monitor
        /// </summary>
        public async Task<MonitorOperationResult> SetBrightnessAsync(string monitorId, int brightness, CancellationToken cancellationToken = default)
        {
            var monitor = GetMonitor(monitorId);
            if (monitor == null)
            {
                Logger.LogError($"Monitor not found: {monitorId}");
                return MonitorOperationResult.Failure("Monitor not found");
            }

            var controller = await GetControllerForMonitorAsync(monitor, cancellationToken);
            if (controller == null)
            {
                Logger.LogError($"No controller available for monitor {monitorId}");
                return MonitorOperationResult.Failure("No controller available for this monitor");
            }

            try
            {
                var result = await controller.SetBrightnessAsync(monitor, brightness, cancellationToken);

                if (result.IsSuccess)
                {
                    // Update monitor status
                    monitor.UpdateStatus(brightness, true);
                }
                else
                {
                    // If setting fails, monitor may be unavailable
                    monitor.IsAvailable = false;
                }

                return result;
            }
            catch (Exception ex)
            {
                monitor.IsAvailable = false;
                return MonitorOperationResult.Failure($"Exception setting brightness: {ex.Message}");
            }
        }

        /// <summary>
        /// Set brightness of all monitors
        /// </summary>
        public async Task<IEnumerable<MonitorOperationResult>> SetAllBrightnessAsync(int brightness, CancellationToken cancellationToken = default)
        {
            var tasks = _monitors
                .Where(m => m.IsAvailable)
                .Select(async monitor =>
                {
                    try
                    {
                        return await SetBrightnessAsync(monitor.Id, brightness, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        return MonitorOperationResult.Failure($"Failed to set brightness for {monitor.Name}: {ex.Message}");
                    }
                });

            return await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Set contrast of the specified monitor
        /// </summary>
        public Task<MonitorOperationResult> SetContrastAsync(string monitorId, int contrast, CancellationToken cancellationToken = default)
            => ExecuteMonitorOperationAsync(
                monitorId,
                contrast,
                (ctrl, mon, val, ct) => ctrl.SetContrastAsync(mon, val, ct),
                (mon, val) => mon.CurrentContrast = val,
                cancellationToken);

        /// <summary>
        /// Set volume of the specified monitor
        /// </summary>
        public Task<MonitorOperationResult> SetVolumeAsync(string monitorId, int volume, CancellationToken cancellationToken = default)
            => ExecuteMonitorOperationAsync(
                monitorId,
                volume,
                (ctrl, mon, val, ct) => ctrl.SetVolumeAsync(mon, val, ct),
                (mon, val) => mon.CurrentVolume = val,
                cancellationToken);

        /// <summary>
        /// Get monitor color temperature
        /// </summary>
        public async Task<BrightnessInfo> GetColorTemperatureAsync(string monitorId, CancellationToken cancellationToken = default)
        {
            var monitor = GetMonitor(monitorId);
            if (monitor == null)
            {
                return BrightnessInfo.Invalid;
            }

            var controller = await GetControllerForMonitorAsync(monitor, cancellationToken);
            if (controller == null)
            {
                return BrightnessInfo.Invalid;
            }

            try
            {
                return await controller.GetColorTemperatureAsync(monitor, cancellationToken);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Logger.LogDebug($"GetColorTemperatureAsync failed: {ex.Message}");
                return BrightnessInfo.Invalid;
            }
        }

        /// <summary>
        /// Set monitor color temperature
        /// </summary>
        public Task<MonitorOperationResult> SetColorTemperatureAsync(string monitorId, int colorTemperature, CancellationToken cancellationToken = default)
            => ExecuteMonitorOperationAsync(
                monitorId,
                colorTemperature,
                (ctrl, mon, val, ct) => ctrl.SetColorTemperatureAsync(mon, val, ct),
                (mon, val) => mon.CurrentColorTemperature = val,
                cancellationToken);

        /// <summary>
        /// Initialize color temperature for a monitor (async operation)
        /// </summary>
        public async Task InitializeColorTemperatureAsync(string monitorId, CancellationToken cancellationToken = default)
        {
            try
            {
                var tempInfo = await GetColorTemperatureAsync(monitorId, cancellationToken);
                if (tempInfo.IsValid)
                {
                    var monitor = GetMonitor(monitorId);
                    if (monitor != null)
                    {
                        // Store raw VCP 0x14 preset value (e.g., 0x05 for 6500K)
                        // No Kelvin conversion - we use discrete presets
                        monitor.CurrentColorTemperature = tempInfo.Current;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to initialize color temperature for {monitorId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Get monitor by ID. Uses dictionary lookup for O(1) performance.
        /// </summary>
        public Monitor? GetMonitor(string monitorId)
        {
            return _monitorLookup.TryGetValue(monitorId, out var monitor) ? monitor : null;
        }

        /// <summary>
        /// Get controller for the monitor
        /// </summary>
        private async Task<IMonitorController?> GetControllerForMonitorAsync(Monitor monitor, CancellationToken cancellationToken = default)
        {
            // WMI monitors use WmiController, DDC/CI monitors use DdcCiController
            foreach (var controller in _controllers)
            {
                if (await controller.CanControlMonitorAsync(monitor, cancellationToken))
                {
                    return controller;
                }
            }

            return null;
        }

        /// <summary>
        /// Generic helper to execute monitor operations with common error handling.
        /// Eliminates code duplication across Set* methods.
        /// </summary>
        private async Task<MonitorOperationResult> ExecuteMonitorOperationAsync<T>(
            string monitorId,
            T value,
            Func<IMonitorController, Monitor, T, CancellationToken, Task<MonitorOperationResult>> operation,
            Action<Monitor, T> onSuccess,
            CancellationToken cancellationToken = default)
        {
            var monitor = GetMonitor(monitorId);
            if (monitor == null)
            {
                Logger.LogError($"[MonitorManager] Monitor not found: {monitorId}");
                return MonitorOperationResult.Failure("Monitor not found");
            }

            var controller = await GetControllerForMonitorAsync(monitor, cancellationToken);
            if (controller == null)
            {
                Logger.LogError($"[MonitorManager] No controller available for monitor {monitorId}");
                return MonitorOperationResult.Failure("No controller available for this monitor");
            }

            try
            {
                var result = await operation(controller, monitor, value, cancellationToken);

                if (result.IsSuccess)
                {
                    onSuccess(monitor, value);
                    monitor.LastUpdate = DateTime.Now;
                }
                else
                {
                    monitor.IsAvailable = false;
                }

                return result;
            }
            catch (Exception ex)
            {
                monitor.IsAvailable = false;
                Logger.LogError($"[MonitorManager] Operation failed for {monitorId}: {ex.Message}");
                return MonitorOperationResult.Failure($"Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Update monitor capability flags based on parsed VCP capabilities
        /// </summary>
        private void UpdateMonitorCapabilitiesFromVcp(Monitor monitor)
        {
            var vcpCaps = monitor.VcpCapabilitiesInfo;
            if (vcpCaps == null)
            {
                return;
            }

            // Check for Contrast support (VCP 0x12)
            if (vcpCaps.SupportsVcpCode(NativeConstants.VcpCodeContrast))
            {
                monitor.Capabilities |= MonitorCapabilities.Contrast;
                Logger.LogDebug($"[{monitor.Id}] Contrast support detected via VCP 0x12");
            }

            // Check for Volume support (VCP 0x62)
            if (vcpCaps.SupportsVcpCode(NativeConstants.VcpCodeVolume))
            {
                monitor.Capabilities |= MonitorCapabilities.Volume;
                Logger.LogDebug($"[{monitor.Id}] Volume support detected via VCP 0x62");
            }

            // Check for Color Temperature support (VCP 0x14)
            if (vcpCaps.SupportsVcpCode(NativeConstants.VcpCodeSelectColorPreset))
            {
                monitor.SupportsColorTemperature = true;
                Logger.LogDebug($"[{monitor.Id}] Color temperature support detected via VCP 0x14");
            }

            Logger.LogInfo($"[{monitor.Id}] Capabilities updated: Contrast={monitor.SupportsContrast}, Volume={monitor.SupportsVolume}, ColorTemp={monitor.SupportsColorTemperature}");
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
                _discoveryLock?.Dispose();

                // Release all controllers
                foreach (var controller in _controllers)
                {
                    controller?.Dispose();
                }

                _controllers.Clear();
                _monitors.Clear();
                _monitorLookup.Clear();
                _disposed = true;
            }
        }
    }
}
