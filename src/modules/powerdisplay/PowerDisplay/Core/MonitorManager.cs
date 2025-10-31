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
using PowerDisplay.Core.Utils;
using PowerDisplay.Native.DDC;
using PowerDisplay.Native.WMI;
using Monitor = PowerDisplay.Core.Models.Monitor;

namespace PowerDisplay.Core
{
    /// <summary>
    /// Monitor manager for unified control of all monitors
    /// No interface abstraction - KISS principle (only one implementation needed)
    /// </summary>
    public partial class MonitorManager : IDisposable
    {
        private readonly List<Monitor> _monitors = new();
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
                    Logger.LogInfo("WMI brightness control not available on this system");
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
                foreach (var (controller, monitors) in results)
                {
                    foreach (var monitor in monitors)
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

                            newMonitors.Add(monitor);
                        }
                    }
                }

                // Update monitor list
                _monitors.Clear();
                _monitors.AddRange(newMonitors);

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

            var controller = GetControllerForMonitor(monitor);
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
            Logger.LogDebug($"[MonitorManager] SetBrightnessAsync called for {monitorId}, brightness={brightness}");

            var monitor = GetMonitor(monitorId);
            if (monitor == null)
            {
                Logger.LogError($"[MonitorManager] Monitor not found: {monitorId}");
                return MonitorOperationResult.Failure("Monitor not found");
            }

            Logger.LogDebug($"[MonitorManager] Monitor found: {monitor.Id}, Type={monitor.Type}, Handle=0x{monitor.Handle:X}, DeviceKey={monitor.DeviceKey}");

            var controller = GetControllerForMonitor(monitor);
            if (controller == null)
            {
                Logger.LogError($"[MonitorManager] No controller available for monitor {monitorId}, Type={monitor.Type}");
                return MonitorOperationResult.Failure("No controller available for this monitor");
            }

            Logger.LogDebug($"[MonitorManager] Controller found: {controller.GetType().Name}");

            try
            {
                Logger.LogDebug($"[MonitorManager] Calling controller.SetBrightnessAsync for {monitor.Id}");
                var result = await controller.SetBrightnessAsync(monitor, brightness, cancellationToken);
                Logger.LogDebug($"[MonitorManager] controller.SetBrightnessAsync returned: IsSuccess={result.IsSuccess}, ErrorMessage={result.ErrorMessage}");

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

            var controller = GetControllerForMonitor(monitor);
            if (controller == null)
            {
                return BrightnessInfo.Invalid;
            }

            try
            {
                return await controller.GetColorTemperatureAsync(monitor, cancellationToken);
            }
            catch (Exception)
            {
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
                        // Convert VCP value to approximate Kelvin temperature
                        // This is a rough mapping - actual values depend on monitor implementation
                        var kelvin = ConvertVcpValueToKelvin(tempInfo.Current, tempInfo.Maximum);
                        monitor.CurrentColorTemperature = kelvin;

                        Logger.LogInfo($"Initialized color temperature for {monitorId}: {kelvin}K (VCP: {tempInfo.Current}/{tempInfo.Maximum})");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to initialize color temperature for {monitorId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Convert VCP value to approximate Kelvin temperature (uses unified converter)
        /// </summary>
        private static int ConvertVcpValueToKelvin(int vcpValue, int maxVcpValue)
        {
            return ColorTemperatureConverter.VcpToKelvin(vcpValue, maxVcpValue);
        }

        /// <summary>
        /// Get monitor by ID
        /// </summary>
        public Monitor? GetMonitor(string monitorId)
        {
            return _monitors.FirstOrDefault(m => m.Id == monitorId);
        }

        /// <summary>
        /// Get controller for the monitor
        /// </summary>
        private IMonitorController? GetControllerForMonitor(Monitor monitor)
        {
            return _controllers.FirstOrDefault(c => c.SupportedType == monitor.Type);
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

            var controller = GetControllerForMonitor(monitor);
            if (controller == null)
            {
                Logger.LogError($"[MonitorManager] No controller available for monitor {monitorId}, Type={monitor.Type}");
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
                _disposed = true;
            }
        }
    }
}
