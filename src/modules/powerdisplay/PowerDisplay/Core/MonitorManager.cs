// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using PowerDisplay.Core.Interfaces;
using PowerDisplay.Core.Models;
using PowerDisplay.Native.DDC;
using PowerDisplay.Native.WMI;
using Monitor = PowerDisplay.Core.Models.Monitor;

namespace PowerDisplay.Core
{
    /// <summary>
    /// Monitor manager for unified control of all monitors
    /// </summary>
    public class MonitorManager : IMonitorManager, IDisposable
    {
        private readonly List<Monitor> _monitors = new();
        private readonly List<IMonitorController> _controllers = new();
        private readonly SemaphoreSlim _discoveryLock = new(1, 1);
        private readonly Timer _refreshTimer;
        private bool _disposed;

        public IReadOnlyList<Monitor> Monitors => _monitors.AsReadOnly();

        public event EventHandler<MonitorListChangedEventArgs>? MonitorsChanged;

        public event EventHandler<MonitorStatusChangedEventArgs>? MonitorStatusChanged;

        public MonitorManager()
        {
            // Initialize controllers
            InitializeControllers();

            // Set up periodic refresh timer (check every 30 seconds)
            // Use synchronous callback to avoid async void issues
            _refreshTimer = new Timer(_ => _ = RefreshMonitorStatusSafeAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// Safe wrapper for RefreshMonitorStatusAsync to catch exceptions in timer callback
        /// </summary>
        private async Task RefreshMonitorStatusSafeAsync()
        {
            try
            {
                await RefreshMonitorStatusAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Periodic refresh failed: {ex.Message}");
            }
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
                    catch (Exception)
                    {
                        // If a controller fails, return empty list
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
                            catch
                            {
                                // If unable to get brightness, use default values
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
                    var oldBrightness = monitor.CurrentBrightness;
                    monitor.UpdateStatus(brightnessInfo.ToPercentage(), true);

                    // Trigger status change event
                    if (oldBrightness != monitor.CurrentBrightness)
                    {
                        MonitorStatusChanged?.Invoke(this, new MonitorStatusChangedEventArgs(
                            monitor, oldBrightness, monitor.CurrentBrightness, true, true));
                    }
                }

                return brightnessInfo;
            }
            catch
            {
                // Mark monitor as unavailable
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
                return MonitorOperationResult.Failure("Monitor not found");
            }

            var controller = GetControllerForMonitor(monitor);
            if (controller == null)
            {
                return MonitorOperationResult.Failure("No controller available for this monitor");
            }

            try
            {
                var oldBrightness = monitor.CurrentBrightness;
                var result = await controller.SetBrightnessAsync(monitor, brightness, cancellationToken);

                if (result.IsSuccess)
                {
                    // Update monitor status
                    monitor.UpdateStatus(brightness, true);

                    // Trigger status change event
                    MonitorStatusChanged?.Invoke(this, new MonitorStatusChangedEventArgs(
                        monitor, oldBrightness, brightness, true, true));
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
        public async Task<MonitorOperationResult> SetContrastAsync(string monitorId, int contrast, CancellationToken cancellationToken = default)
        {
            var monitor = GetMonitor(monitorId);
            if (monitor == null)
            {
                return MonitorOperationResult.Failure("Monitor not found");
            }

            var controller = GetControllerForMonitor(monitor) as IExtendedMonitorController;
            if (controller == null)
            {
                return MonitorOperationResult.Failure("No extended controller available for this monitor");
            }

            try
            {
                var oldContrast = monitor.CurrentContrast;
                var result = await controller.SetContrastAsync(monitor, contrast, cancellationToken);

                if (result.IsSuccess)
                {
                    monitor.CurrentContrast = contrast;
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
                return MonitorOperationResult.Failure($"Exception setting contrast: {ex.Message}");
            }
        }

        /// <summary>
        /// Set volume of the specified monitor
        /// </summary>
        public async Task<MonitorOperationResult> SetVolumeAsync(string monitorId, int volume, CancellationToken cancellationToken = default)
        {
            var monitor = GetMonitor(monitorId);
            if (monitor == null)
            {
                return MonitorOperationResult.Failure("Monitor not found");
            }

            var controller = GetControllerForMonitor(monitor) as IExtendedMonitorController;
            if (controller == null)
            {
                return MonitorOperationResult.Failure("No extended controller available for this monitor");
            }

            try
            {
                var oldVolume = monitor.CurrentVolume;
                var result = await controller.SetVolumeAsync(monitor, volume, cancellationToken);

                if (result.IsSuccess)
                {
                    monitor.CurrentVolume = volume;
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
                return MonitorOperationResult.Failure($"Exception setting volume: {ex.Message}");
            }
        }

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

            var controller = GetControllerForMonitor(monitor) as DdcCiController;
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
        public async Task<MonitorOperationResult> SetColorTemperatureAsync(string monitorId, int colorTemperature, CancellationToken cancellationToken = default)
        {
            var monitor = GetMonitor(monitorId);
            if (monitor == null)
            {
                return MonitorOperationResult.Failure("Monitor not found");
            }

            var controller = GetControllerForMonitor(monitor) as DdcCiController;
            if (controller == null)
            {
                return MonitorOperationResult.Failure("DDC/CI controller not available for this monitor");
            }

            try
            {
                var oldTemperature = monitor.CurrentColorTemperature;
                var result = await controller.SetColorTemperatureAsync(monitor, colorTemperature, cancellationToken);

                if (result.IsSuccess)
                {
                    monitor.CurrentColorTemperature = colorTemperature;
                    monitor.LastUpdate = DateTime.Now;

                    // Trigger status change event
                    MonitorStatusChanged?.Invoke(this, new MonitorStatusChangedEventArgs(
                        monitor,
                        $"Color temperature changed from {oldTemperature}K to {colorTemperature}K",
                        MonitorStatusChangedEventArgs.ChangeType.ColorTemperature
                    ));
                }
                else
                {
                    monitor.IsAvailable = false;
                }

                return result;
            }
            catch (Exception ex)
            {
                return MonitorOperationResult.Failure($"Exception setting color temperature: {ex.Message}");
            }
        }

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
        /// Convert VCP value to approximate Kelvin temperature
        /// </summary>
        private static int ConvertVcpValueToKelvin(int vcpValue, int maxVcpValue)
        {
            // Standard color temperature range mapping
            const int minKelvin = 2000; // Warm
            const int maxKelvin = 10000; // Cool
            
            // Normalize VCP value to 0-1 range
            double normalizedVcp = maxVcpValue > 0 ? (double)vcpValue / maxVcpValue : 0.5;
            
            // Map to Kelvin range
            int kelvin = (int)(minKelvin + (normalizedVcp * (maxKelvin - minKelvin)));
            
            return Math.Clamp(kelvin, minKelvin, maxKelvin);
        }

        /// <summary>
        /// Refresh monitor status
        /// </summary>
        public async Task RefreshMonitorStatusAsync(CancellationToken cancellationToken = default)
        {
            var tasks = _monitors.Select(async monitor =>
            {
                try
                {
                    var controller = GetControllerForMonitor(monitor);
                    if (controller == null)
                    {
                        return;
                    }

                    // Validate connection status
                    var isConnected = await controller.ValidateConnectionAsync(monitor, cancellationToken);
                    var oldAvailability = monitor.IsAvailable;

                    if (isConnected)
                    {
                        // 获取当前亮度
                        var brightnessInfo = await controller.GetBrightnessAsync(monitor, cancellationToken);
                        if (brightnessInfo.IsValid)
                        {
                            var oldBrightness = monitor.CurrentBrightness;
                            monitor.UpdateStatus(brightnessInfo.ToPercentage(), true);

                            // Trigger status change event
                            if (oldBrightness != monitor.CurrentBrightness || oldAvailability != monitor.IsAvailable)
                            {
                                MonitorStatusChanged?.Invoke(this, new MonitorStatusChangedEventArgs(
                                    monitor, oldBrightness, monitor.CurrentBrightness, oldAvailability, monitor.IsAvailable));
                            }
                        }
                        else
                        {
                            monitor.IsAvailable = false;
                        }
                    }
                    else
                    {
                        monitor.IsAvailable = false;

                        // Trigger availability change event
                        if (oldAvailability != monitor.IsAvailable)
                        {
                            MonitorStatusChanged?.Invoke(this, new MonitorStatusChangedEventArgs(
                                monitor, monitor.CurrentBrightness, monitor.CurrentBrightness, oldAvailability, monitor.IsAvailable));
                        }
                    }
                }
                catch
                {
                    // Refresh failed, mark as unavailable
                    monitor.IsAvailable = false;
                }
            });

            await Task.WhenAll(tasks);
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _refreshTimer?.Dispose();
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
