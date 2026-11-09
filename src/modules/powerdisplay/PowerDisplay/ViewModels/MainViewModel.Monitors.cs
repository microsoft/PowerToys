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
using PowerDisplay.Common.Services;
using PowerDisplay.Helpers;
using PowerDisplay.Models;
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

            // Forward the latest max-compatibility flag before each discovery so the
            // DDC/CI controller picks up toggle changes without a process restart.
            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
            _monitorManager.SetMaxCompatibilityMode(settings.Properties.MaxCompatibilityMode);

            // Discover monitors
            var monitors = await _monitorManager.DiscoverMonitorsAsync(cancellationToken);

            // Update UI on the dispatcher thread, then complete initialization asynchronously
            _dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    UpdateMonitorList(monitors);

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
            var discovered = Monitors
                .Where(monitor => !string.IsNullOrEmpty(monitor.Id))
                .Select(monitor => (monitor.Id, monitor.MonitorNumber))
                .ToList();

            await MigrateLegacySideFilesAsync(discovered, _cancellationTokenSource.Token);

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
    /// <param name="skipScanningCheck">If true, skip the IsScanning reentry guard. Used by the watcher path where IsScanning was already set upstream by <see cref="MainViewModel.OnDisplayChanging"/>.</param>
    public async Task RefreshMonitorsAsync(bool skipScanningCheck = false)
    {
        if (!skipScanningCheck && IsScanning)
        {
            return;
        }

        try
        {
            CancelPendingLinkedBrightnessCommit();
            IsScanning = true;

            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
            _monitorManager.SetMaxCompatibilityMode(settings.Properties.MaxCompatibilityMode);

            var monitors = await _monitorManager.DiscoverMonitorsAsync(_cancellationTokenSource.Token);

            _dispatcherQueue.TryEnqueue(() =>
            {
                UpdateMonitorList(monitors);
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

    private void UpdateMonitorList(IReadOnlyList<Monitor> monitors)
    {
        CancelPendingLinkedBrightnessCommit();

        // Dispose old ViewModels to unsubscribe PropertyChanged handlers
        foreach (var vm in Monitors)
        {
            vm.Dispose();
        }

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
        OnPropertyChanged(nameof(ShowLinkLevelsToggle));
        RecomputeLinkedBrightnessAvailability();

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
                .Select(m => m.Id),
            MonitorIdComparer.Instance);

    /// <summary>
    /// Returns the set of monitor IDs that are currently hidden in persisted settings.
    /// Uses a case-insensitive comparer consistent with the rest of the settings layer.
    /// Intended for IPC reads; does NOT trigger monitor hardware discovery.
    /// </summary>
    public IReadOnlySet<string> GetHiddenMonitorIds()
    {
        var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
        return GetHiddenMonitorIds(settings);
    }

    /// <summary>
    /// Returns a point-in-time copy of the app's already-discovered monitor list for IPC reads.
    /// Does NOT trigger hardware discovery. Hidden-monitor filtering is applied by the caller.
    /// A materialized copy (rather than the live <c>_monitorManager.Monitors</c> view) is returned so
    /// callers can iterate it safely even if a concurrent discovery rebuilds the underlying list.
    /// </summary>
    public IReadOnlyList<Monitor> SnapshotMonitors()
        => _monitorManager.Monitors.ToList();

    /// <summary>
    /// The app's live monitor manager exposed through the <see cref="IMonitorManager"/> hardware-write
    /// abstraction — used by the IPC set/apply-profile executors to perform hardware writes
    /// (SetBrightnessAsync, SetContrastAsync, etc.) on the single hardware-owning instance. Returning
    /// the interface (not the concrete <c>MonitorManager</c>) keeps the IPC path decoupled and fakeable,
    /// which is the reason the interface exists.
    /// </summary>
    public IMonitorManager MonitorManager => _monitorManager;
}
