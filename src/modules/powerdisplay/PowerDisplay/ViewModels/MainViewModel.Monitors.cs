// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerDisplay.Common.Models;
using PowerDisplay.Helpers;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.ViewModels;

/// <summary>
/// MainViewModel - Monitor discovery and management methods
/// </summary>
public partial class MainViewModel
{
    private async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsScanning = true;

            // Discover monitors
            var monitors = await _monitorManager.DiscoverMonitorsAsync(cancellationToken);

            // Update UI on the dispatcher thread
            _dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    UpdateMonitorList(monitors, isInitialLoad: true);
                    IsScanning = false;
                    IsInitialized = true;

                    // Start watching for display changes after initialization
                    StartDisplayWatching();
                }
                catch (Exception lambdaEx)
                {
                    Logger.LogError($"[InitializeAsync] UI update failed: {lambdaEx.Message}");
                    IsScanning = false;
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError($"[InitializeAsync] Monitor discovery failed: {ex.Message}");
            _dispatcherQueue.TryEnqueue(() =>
            {
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
            IsScanning = true;

            var monitors = await _monitorManager.DiscoverMonitorsAsync(_cancellationTokenSource.Token);

            _dispatcherQueue.TryEnqueue(() =>
            {
                UpdateMonitorList(monitors, isInitialLoad: false);
                IsScanning = false;
            });
        }
        catch (Exception ex)
        {
            Logger.LogError($"[RefreshMonitorsAsync] Refresh failed: {ex.Message}");
            _dispatcherQueue.TryEnqueue(() =>
            {
                IsScanning = false;
            });
        }
    }

    private void UpdateMonitorList(IReadOnlyList<Monitor> monitors, bool isInitialLoad)
    {
        Monitors.Clear();

        // Load settings to check for hidden monitors
        var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
        var hiddenMonitorIds = GetHiddenMonitorIds(settings);

        foreach (var monitor in monitors)
        {
            // Skip monitors that are marked as hidden in settings
            if (hiddenMonitorIds.Contains(monitor.Id))
            {
                Logger.LogInfo($"[UpdateMonitorList] Skipping hidden monitor: {monitor.Name} ({monitor.Id})");
                continue;
            }

            var vm = new MonitorViewModel(monitor, _monitorManager, this);
            ApplyFeatureVisibility(vm, settings);
            Monitors.Add(vm);
        }

        OnPropertyChanged(nameof(HasMonitors));
        OnPropertyChanged(nameof(ShowNoMonitorsMessage));

        // Save monitor information to settings
        SaveMonitorsToSettings();

        // Only restore settings on initial load, not on refresh
        if (isInitialLoad && settings.Properties.RestoreSettingsOnStartup)
        {
            RestoreMonitorSettings();
        }
    }

    /// <summary>
    /// Get set of hidden monitor IDs from settings
    /// </summary>
    private HashSet<string> GetHiddenMonitorIds(PowerDisplaySettings settings)
        => new HashSet<string>(
            settings.Properties.Monitors
                .Where(m => m.IsHidden)
                .Select(m => m.InternalName));
}
