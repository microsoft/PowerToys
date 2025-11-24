// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerDisplay.Common.Models;
using PowerDisplay.Core.Interfaces;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.ViewModels;

/// <summary>
/// MainViewModel - Monitor discovery and management methods
/// </summary>
public partial class MainViewModel
{
    private async Task InitializeAsync()
    {
        try
        {
            StatusText = "Scanning monitors...";
            IsScanning = true;

            // Discover monitors
            var monitors = await _monitorManager.DiscoverMonitorsAsync(_cancellationTokenSource.Token);

            // Update UI on the dispatcher thread
            _dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    UpdateMonitorList(monitors);
                    IsScanning = false;
                    IsInitialized = true;

                    if (monitors.Count > 0)
                    {
                        StatusText = $"Found {monitors.Count} monitors";
                    }
                    else
                    {
                        StatusText = "No controllable monitors found";
                    }
                }
                catch (Exception lambdaEx)
                {
                    Logger.LogError($"[InitializeAsync] UI update failed: {lambdaEx.Message}");
                    IsScanning = false;
                    StatusText = $"UI update failed: {lambdaEx.Message}";
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError($"[InitializeAsync] Monitor discovery failed: {ex.Message}");
            _dispatcherQueue.TryEnqueue(() =>
            {
                StatusText = $"Scan failed: {ex.Message}";
                IsScanning = false;
            });
        }
    }

    public async Task RefreshMonitorsAsync()
    {
        if (IsScanning)
        {
            return;
        }

        try
        {
            StatusText = "Refreshing monitor list...";
            IsScanning = true;

            var monitors = await _monitorManager.DiscoverMonitorsAsync(_cancellationTokenSource.Token);

            _dispatcherQueue.TryEnqueue(() =>
            {
                UpdateMonitorList(monitors);
                IsScanning = false;
                StatusText = $"Found {monitors.Count} monitors";
            });
        }
        catch (Exception ex)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                StatusText = $"Refresh failed: {ex.Message}";
                IsScanning = false;
            });
        }
    }

    private void UpdateMonitorList(IReadOnlyList<Monitor> monitors)
    {
        Monitors.Clear();

        // Load settings to check for hidden monitors
        var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
        var hiddenMonitorIds = GetHiddenMonitorIds(settings);

        var colorTempTasks = new List<Task>();
        foreach (var monitor in monitors)
        {
            // Skip monitors that are marked as hidden in settings
            if (hiddenMonitorIds.Contains(monitor.HardwareId))
            {
                continue;
            }

            var vm = new MonitorViewModel(monitor, _monitorManager, this);
            Monitors.Add(vm);

            // Asynchronously initialize color temperature for DDC/CI monitors
            if (monitor.SupportsColorTemperature && monitor.CommunicationMethod == "DDC/CI")
            {
                var task = InitializeColorTemperatureSafeAsync(monitor.Id, vm);
                colorTempTasks.Add(task);
            }
        }

        OnPropertyChanged(nameof(HasMonitors));
        OnPropertyChanged(nameof(ShowNoMonitorsMessage));

        // Wait for color temperature initialization to complete before saving
        // This ensures we save the actual scanned values instead of defaults
        if (colorTempTasks.Count > 0)
        {
            // Use fire-and-forget async method to avoid blocking UI thread
            _ = WaitForColorTempAndSaveAsync(colorTempTasks);
        }
        else
        {
            // No color temperature tasks, save immediately
            SaveMonitorsToSettings();

            // Restore saved settings if enabled (async, don't block)
            _ = ReloadMonitorSettingsAsync(null);
        }
    }

    private async Task WaitForColorTempAndSaveAsync(List<Task> colorTempTasks)
    {
        try
        {
            // Wait for all color temperature initialization tasks to complete
            await Task.WhenAll(colorTempTasks);

            // Save monitor information to settings.json and reload settings
            // Must be done on UI thread since these methods access UI properties and observable collections
            _dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    SaveMonitorsToSettings();

                    // Restore saved settings if enabled (async)
                    await ReloadMonitorSettingsAsync(null); // Tasks already completed, pass null
                }
                catch (Exception innerEx)
                {
                    Logger.LogError($"[WaitForColorTempAndSaveAsync] Error in UI thread operation: {innerEx.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[WaitForColorTempAndSaveAsync] Color temperature initialization failed: {ex.Message}");

            // Save anyway with whatever values we have
            _dispatcherQueue.TryEnqueue(() =>
            {
                SaveMonitorsToSettings();
            });
        }
    }

    public async Task SetAllBrightnessAsync(int brightness)
    {
        try
        {
            StatusText = $"Setting all monitors brightness to {brightness}%...";
            await _monitorManager.SetAllBrightnessAsync(brightness, _cancellationTokenSource.Token);
            StatusText = $"All monitors brightness set to {brightness}%";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to set brightness: {ex.Message}";
        }
    }

    private void OnMonitorsChanged(object? sender, MonitorListChangedEventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            // Load settings to check for hidden monitors
            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
            var hiddenMonitorIds = GetHiddenMonitorIds(settings);

            // Handle monitors being added or removed
            if (e.AddedMonitors.Count > 0)
            {
                foreach (var monitor in e.AddedMonitors)
                {
                    // Skip monitors that are marked as hidden
                    if (hiddenMonitorIds.Contains(monitor.HardwareId))
                    {
                        Logger.LogInfo($"[OnMonitorsChanged] Skipping hidden monitor (added): {monitor.Name} (HardwareId: {monitor.HardwareId})");
                        continue;
                    }

                    var existingVm = GetMonitorViewModel(monitor.Id);
                    if (existingVm == null)
                    {
                        var vm = new MonitorViewModel(monitor, _monitorManager, this);
                        Monitors.Add(vm);
                    }
                }
            }

            if (e.RemovedMonitors.Count > 0)
            {
                foreach (var monitor in e.RemovedMonitors)
                {
                    var vm = GetMonitorViewModel(monitor.Id);
                    if (vm != null)
                    {
                        Monitors.Remove(vm);
                        vm.Dispose();
                    }
                }
            }

            StatusText = $"Monitor list updated ({Monitors.Count} total)";

            // Note: SaveMonitorsToSettings() is called by UpdateMonitorList() after full scan completes
            // to avoid double-firing the refresh event during re-scan operations
        });
    }

    private MonitorViewModel? GetMonitorViewModel(string monitorId)
        => Monitors.FirstOrDefault(vm => vm.Id == monitorId);

    /// <summary>
    /// Get set of hidden monitor IDs from settings
    /// </summary>
    private HashSet<string> GetHiddenMonitorIds(PowerDisplaySettings settings)
        => new HashSet<string>(
            settings.Properties.Monitors
                .Where(m => m.IsHidden)
                .Select(m => m.HardwareId));

    /// <summary>
    /// Safe wrapper for initializing color temperature asynchronously
    /// </summary>
    private async Task InitializeColorTemperatureSafeAsync(string monitorId, MonitorViewModel vm)
    {
        try
        {
            // Read current color temperature from hardware
            await _monitorManager.InitializeColorTemperatureAsync(monitorId);

            // Get the monitor and use the hardware value as-is
            var monitor = _monitorManager.GetMonitor(monitorId);
            if (monitor != null)
            {
                Logger.LogInfo($"[{monitorId}] Read color temperature from hardware: {monitor.CurrentColorTemperature}");

                _dispatcherQueue.TryEnqueue(() =>
                {
                    // Update color temperature without triggering hardware write
                    // Use the hardware value directly, even if not in the preset list
                    // This will also update monitor_state.json via MonitorStateManager
                    vm.UpdatePropertySilently(nameof(vm.ColorTemperature), monitor.CurrentColorTemperature);
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to initialize color temperature for {monitorId}: {ex.Message}");
        }
    }
}
