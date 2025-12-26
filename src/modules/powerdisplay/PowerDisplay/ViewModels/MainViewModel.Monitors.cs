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

            // Update UI on the dispatcher thread, then complete initialization asynchronously
            _dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    UpdateMonitorList(monitors, isInitialLoad: true);

                    // Complete initialization asynchronously (restore settings if enabled)
                    // IsScanning remains true until restore completes
                    _ = CompleteInitializationAsync();
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

    /// <summary>
    /// Complete initialization by restoring settings (if enabled) and firing completion event.
    /// IsScanning remains true until this method completes, so user sees discovery UI during restore.
    /// </summary>
    private async Task CompleteInitializationAsync()
    {
        try
        {
            // Check if we should restore settings on startup
            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
            if (settings.Properties.RestoreSettingsOnStartup)
            {
                await RestoreMonitorSettingsAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[CompleteInitializationAsync] Failed to restore settings: {ex.Message}");
        }
        finally
        {
            // Always complete initialization, even if restore failed
            IsScanning = false;
            IsInitialized = true;

            // Start watching for display changes after initialization
            StartDisplayWatching();

            // Notify listeners that initialization is complete
            InitializationCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Refresh monitors list asynchronously.
    /// </summary>
    /// <param name="skipScanningCheck">If true, skip the IsScanning check (used by OnDisplayChanged which sets IsScanning before calling).</param>
    public async Task RefreshMonitorsAsync(bool skipScanningCheck = false)
    {
        if (!skipScanningCheck && IsScanning)
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

        // Note: RestoreMonitorSettingsAsync is now called from InitializeAsync/CompleteInitializationAsync
        // to ensure scanning state is maintained until restore completes
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
