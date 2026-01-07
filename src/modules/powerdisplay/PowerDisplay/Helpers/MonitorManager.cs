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
using PowerDisplay.Common.Services;
using PowerDisplay.Common.Utils;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Helpers
{
    /// <summary>
    /// Monitor manager for unified control of all monitors
    /// No interface abstraction - KISS principle (only one implementation needed)
    /// </summary>
    public partial class MonitorManager : IDisposable
    {
        private readonly List<Monitor> _monitors = new();
        private readonly Dictionary<string, Monitor> _monitorLookup = new();
        private readonly SemaphoreSlim _discoveryLock = new(1, 1);
        private readonly DisplayRotationService _rotationService = new();

        // Controllers stored by type for O(1) lookup based on CommunicationMethod
        private DdcCiController? _ddcController;
        private WmiController? _wmiController;
        private bool _disposed;

        public IReadOnlyList<Monitor> Monitors => _monitors.AsReadOnly();

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
                _ddcController = new DdcCiController();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to initialize DDC/CI controller: {ex.Message}");
            }

            try
            {
                // WMI controller (internal monitors)
                // Always create - DiscoverMonitorsAsync returns empty list if WMI is unavailable
                _wmiController = new WmiController();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to initialize WMI controller: {ex.Message}");
            }
        }

        /// <summary>
        /// Discover all monitors from all controllers.
        /// Each controller is responsible for fully initializing its monitors
        /// (including brightness, capabilities, input source, color temperature, etc.)
        /// </summary>
        public async Task<IReadOnlyList<Monitor>> DiscoverMonitorsAsync(CancellationToken cancellationToken = default)
        {
            await _discoveryLock.WaitAsync(cancellationToken);

            try
            {
                var discoveredMonitors = await DiscoverFromAllControllersAsync(cancellationToken);

                // Update collections
                _monitors.Clear();
                _monitorLookup.Clear();

                var sortedMonitors = discoveredMonitors
                    .OrderBy(m => m.MonitorNumber)
                    .ToList();

                _monitors.AddRange(sortedMonitors);
                foreach (var monitor in sortedMonitors)
                {
                    _monitorLookup[monitor.Id] = monitor;
                }

                return _monitors.AsReadOnly();
            }
            finally
            {
                _discoveryLock.Release();
            }
        }

        /// <summary>
        /// Discover monitors from all registered controllers in parallel.
        /// </summary>
        private async Task<List<Monitor>> DiscoverFromAllControllersAsync(CancellationToken cancellationToken)
        {
            var tasks = new List<Task<IEnumerable<Monitor>>>();

            if (_ddcController != null)
            {
                tasks.Add(SafeDiscoverAsync(_ddcController, cancellationToken));
            }

            if (_wmiController != null)
            {
                tasks.Add(SafeDiscoverAsync(_wmiController, cancellationToken));
            }

            var results = await Task.WhenAll(tasks);
            return results.SelectMany(m => m).ToList();
        }

        /// <summary>
        /// Safely discover monitors from a controller, returning empty list on failure.
        /// </summary>
        private static async Task<IEnumerable<Monitor>> SafeDiscoverAsync(
            IMonitorController controller,
            CancellationToken cancellationToken)
        {
            try
            {
                return await controller.DiscoverMonitorsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Controller {controller.Name} discovery failed: {ex.Message}");
                return Enumerable.Empty<Monitor>();
            }
        }

        /// <summary>
        /// Get brightness of the specified monitor
        /// </summary>
        public async Task<VcpFeatureValue> GetBrightnessAsync(string monitorId, CancellationToken cancellationToken = default)
        {
            var monitor = GetMonitor(monitorId);
            if (monitor == null)
            {
                return VcpFeatureValue.Invalid;
            }

            var controller = GetControllerForMonitor(monitor);
            if (controller == null)
            {
                return VcpFeatureValue.Invalid;
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
                return VcpFeatureValue.Invalid;
            }
        }

        /// <summary>
        /// Set brightness of the specified monitor
        /// </summary>
        public Task<MonitorOperationResult> SetBrightnessAsync(string monitorId, int brightness, CancellationToken cancellationToken = default)
            => ExecuteMonitorOperationAsync(
                monitorId,
                brightness,
                (ctrl, mon, val, ct) => ctrl.SetBrightnessAsync(mon, val, ct),
                (mon, val) => mon.UpdateStatus(val, true),
                cancellationToken);

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
        public async Task<VcpFeatureValue> GetColorTemperatureAsync(string monitorId, CancellationToken cancellationToken = default)
        {
            var monitor = GetMonitor(monitorId);
            if (monitor == null)
            {
                return VcpFeatureValue.Invalid;
            }

            var controller = GetControllerForMonitor(monitor);
            if (controller == null)
            {
                return VcpFeatureValue.Invalid;
            }

            try
            {
                return await controller.GetColorTemperatureAsync(monitor, cancellationToken);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                return VcpFeatureValue.Invalid;
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
        /// Get current input source for a monitor
        /// </summary>
        public async Task<VcpFeatureValue> GetInputSourceAsync(string monitorId, CancellationToken cancellationToken = default)
        {
            var monitor = GetMonitor(monitorId);
            if (monitor == null)
            {
                return VcpFeatureValue.Invalid;
            }

            var controller = GetControllerForMonitor(monitor);
            if (controller == null)
            {
                return VcpFeatureValue.Invalid;
            }

            try
            {
                return await controller.GetInputSourceAsync(monitor, cancellationToken);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                return VcpFeatureValue.Invalid;
            }
        }

        /// <summary>
        /// Set input source for a monitor
        /// </summary>
        public Task<MonitorOperationResult> SetInputSourceAsync(string monitorId, int inputSource, CancellationToken cancellationToken = default)
            => ExecuteMonitorOperationAsync(
                monitorId,
                inputSource,
                (ctrl, mon, val, ct) => ctrl.SetInputSourceAsync(mon, val, ct),
                (mon, val) => mon.CurrentInputSource = val,
                cancellationToken);

        /// <summary>
        /// Set power state for a monitor using VCP 0xD6.
        /// Note: Setting any state other than On (0x01) will turn off the display.
        /// We don't update monitor state since the display will be off.
        /// </summary>
        public Task<MonitorOperationResult> SetPowerStateAsync(string monitorId, int powerState, CancellationToken cancellationToken = default)
            => ExecuteMonitorOperationAsync(
                monitorId,
                powerState,
                (ctrl, mon, val, ct) => ctrl.SetPowerStateAsync(mon, val, ct),
                (mon, val) => { }, // No state update - display will be off for non-On values
                cancellationToken);

        /// <summary>
        /// Set rotation/orientation for a monitor.
        /// Uses Windows ChangeDisplaySettingsEx API (not DDC/CI).
        /// After successful rotation, refreshes orientation for all monitors sharing the same GdiDeviceName
        /// (important for mirror/clone mode where multiple monitors share one display source).
        /// </summary>
        /// <param name="monitorId">Monitor ID</param>
        /// <param name="orientation">Orientation: 0=normal, 1=90°, 2=180°, 3=270°</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        public Task<MonitorOperationResult> SetRotationAsync(string monitorId, int orientation, CancellationToken cancellationToken = default)
        {
            var monitor = GetMonitor(monitorId);
            if (monitor == null)
            {
                Logger.LogError($"[MonitorManager] SetRotation: Monitor not found: {monitorId}");
                return Task.FromResult(MonitorOperationResult.Failure("Monitor not found"));
            }

            // Rotation uses Windows display settings API, not DDC/CI controller
            // Prefer using Monitor object which contains GdiDeviceName for accurate adapter targeting
            var result = _rotationService.SetRotation(monitor, orientation);

            if (result.IsSuccess)
            {
                // Refresh orientation for all monitors - rotation affects the GdiDeviceName (display source),
                // and in mirror mode multiple monitors may share the same GdiDeviceName
                RefreshAllOrientations();
            }
            else
            {
                Logger.LogError($"[MonitorManager] SetRotation: Failed for {monitorId}: {result.ErrorMessage}");
            }

            return Task.FromResult(result);
        }

        /// <summary>
        /// Refresh orientation values for all monitors by querying current display settings.
        /// This ensures all monitors reflect the actual system state, which is important
        /// in mirror mode where multiple monitors share the same GdiDeviceName.
        /// </summary>
        public void RefreshAllOrientations()
        {
            foreach (var monitor in _monitors)
            {
                if (string.IsNullOrEmpty(monitor.GdiDeviceName))
                {
                    continue;
                }

                var currentOrientation = _rotationService.GetCurrentOrientation(monitor.GdiDeviceName);
                if (currentOrientation >= 0 && currentOrientation != monitor.Orientation)
                {
                    monitor.Orientation = currentOrientation;
                    monitor.LastUpdate = DateTime.Now;
                }
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
        /// Get controller for the monitor based on CommunicationMethod.
        /// O(1) lookup - no async validation needed since controller type is determined at discovery.
        /// </summary>
        private IMonitorController? GetControllerForMonitor(Monitor monitor)
        {
            return monitor.CommunicationMethod switch
            {
                "WMI" => _wmiController,
                "DDC/CI" => _ddcController,
                _ => null,
            };
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

                // Release controllers
                _ddcController?.Dispose();
                _wmiController?.Dispose();

                _monitors.Clear();
                _monitorLookup.Clear();
                _disposed = true;
            }
        }
    }
}
