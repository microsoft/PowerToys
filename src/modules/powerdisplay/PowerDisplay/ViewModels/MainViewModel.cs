// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PowerDisplay.Commands;
using PowerDisplay.Core;
using PowerDisplay.Core.Interfaces;
using PowerDisplay.Core.Models;
using PowerDisplay.Helpers;
using PowerDisplay.Serialization;
using PowerToys.Interop;
using Monitor = PowerDisplay.Core.Models.Monitor;

namespace PowerDisplay.ViewModels;

/// <summary>
/// Main ViewModel for the PowerDisplay application
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicMethods)]
public partial class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly MonitorManager _monitorManager;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly ISettingsUtils _settingsUtils;
    private readonly MonitorStateManager _stateManager;

    private ObservableCollection<MonitorViewModel> _monitors;
    private string _statusText;
    private bool _isScanning;
    private bool _isInitialized;
    private bool _isLoading;

    /// <summary>
    /// Event triggered when UI refresh is requested due to settings changes
    /// </summary>
    public event EventHandler? UIRefreshRequested;

    public MainViewModel()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _cancellationTokenSource = new CancellationTokenSource();
        _monitors = new ObservableCollection<MonitorViewModel>();
        _statusText = "Initializing...";
        _isScanning = true;

        // Initialize settings utils
        _settingsUtils = new SettingsUtils();
        _stateManager = new MonitorStateManager();

        // Initialize the monitor manager
        _monitorManager = new MonitorManager();

        // Subscribe to events
        _monitorManager.MonitorsChanged += OnMonitorsChanged;

        // Start initial discovery
        _ = InitializeAsync();
    }

    public ObservableCollection<MonitorViewModel> Monitors
    {
        get => _monitors;
        set
        {
            _monitors = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            _isScanning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasMonitors));
            OnPropertyChanged(nameof(ShowNoMonitorsMessage));
            OnPropertyChanged(nameof(IsInteractionEnabled));
        }
    }

    public bool HasMonitors => !IsScanning && Monitors.Count > 0;

    public bool ShowNoMonitorsMessage => !IsScanning && Monitors.Count == 0;

    public bool IsInitialized
    {
        get => _isInitialized;
        private set
        {
            _isInitialized = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            _isLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsInteractionEnabled));
        }
    }

    /// <summary>
    /// Gets a value indicating whether gets whether user interaction is enabled (not loading or scanning)
    /// </summary>
    public bool IsInteractionEnabled => !IsLoading && !IsScanning;

    public ICommand RefreshCommand => new RelayCommand(async () => await RefreshMonitorsAsync());

    public ICommand SetAllBrightnessCommand => new RelayCommand<int?>(async (brightness) =>
    {
        if (brightness.HasValue)
        {
            await SetAllBrightnessAsync(brightness.Value);
        }
    });

    private async Task InitializeAsync()
    {
        try
        {
            StatusText = "Scanning monitors...";
            IsScanning = true;
            Logger.LogInfo("[InitializeAsync] Starting monitor discovery");

            // Discover monitors
            var monitors = await _monitorManager.DiscoverMonitorsAsync(_cancellationTokenSource.Token);
            Logger.LogInfo($"[InitializeAsync] Discovery completed, found {monitors.Count} monitors");

            // Update UI on the dispatcher thread
            Logger.LogInfo("[InitializeAsync] Calling TryEnqueue to update UI");
            bool enqueued = _dispatcherQueue.TryEnqueue(() =>
            {
                Logger.LogInfo("[InitializeAsync] TryEnqueue lambda started");
                try
                {
                    Logger.LogInfo("[InitializeAsync] Calling UpdateMonitorList");
                    UpdateMonitorList(monitors);
                    Logger.LogInfo("[InitializeAsync] UpdateMonitorList returned");

                    IsScanning = false;
                    Logger.LogInfo("[InitializeAsync] IsScanning set to false");

                    IsInitialized = true;
                    Logger.LogInfo("[InitializeAsync] IsInitialized set to true");

                    if (monitors.Count > 0)
                    {
                        StatusText = $"Found {monitors.Count} monitors";
                    }
                    else
                    {
                        StatusText = "No controllable monitors found";
                    }

                    Logger.LogInfo($"[InitializeAsync] StatusText set to: {StatusText}");
                    Logger.LogInfo("[InitializeAsync] TryEnqueue lambda completed successfully");
                }
                catch (Exception lambdaEx)
                {
                    Logger.LogError($"[InitializeAsync] Exception in TryEnqueue lambda: {lambdaEx.Message}");
                    Logger.LogError($"[InitializeAsync] Stack trace: {lambdaEx.StackTrace}");
                    IsScanning = false;
                    throw;
                }
            });
            Logger.LogInfo($"[InitializeAsync] TryEnqueue returned: {enqueued}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[InitializeAsync] Exception in InitializeAsync: {ex.Message}");
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
        Logger.LogInfo($"[UpdateMonitorList] Starting with {monitors.Count} monitors");

        Monitors.Clear();
        Logger.LogInfo("[UpdateMonitorList] Cleared Monitors collection");

        // Load settings to check for hidden monitors
        var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
        var hiddenMonitorIds = new HashSet<string>(
            settings.Properties.Monitors
                .Where(m => m.IsHidden)
                .Select(m => m.HardwareId));
        Logger.LogInfo($"[UpdateMonitorList] Loaded settings, found {hiddenMonitorIds.Count} hidden monitor IDs");

        var colorTempTasks = new List<Task>();
        Logger.LogInfo("[UpdateMonitorList] Starting monitor loop");
        foreach (var monitor in monitors)
        {
            Logger.LogInfo($"[UpdateMonitorList] Processing monitor: {monitor.Name} (HardwareId: {monitor.HardwareId})");

            // Skip monitors that are marked as hidden in settings
            if (hiddenMonitorIds.Contains(monitor.HardwareId))
            {
                Logger.LogInfo($"[UpdateMonitorList] Skipping hidden monitor: {monitor.Name} (HardwareId: {monitor.HardwareId})");
                continue;
            }

            Logger.LogInfo($"[UpdateMonitorList] Creating MonitorViewModel for {monitor.Name}");
            var vm = new MonitorViewModel(monitor, _monitorManager, this);
            Monitors.Add(vm);
            Logger.LogInfo($"[UpdateMonitorList] Added MonitorViewModel, total count: {Monitors.Count}");

            // Asynchronously initialize color temperature for DDC/CI monitors
            if (monitor.SupportsColorTemperature && monitor.CommunicationMethod == "DDC/CI")
            {
                Logger.LogInfo($"[UpdateMonitorList] Initializing color temperature for {monitor.Name}");
                var task = InitializeColorTemperatureSafeAsync(monitor.Id, vm);
                colorTempTasks.Add(task);
                Logger.LogInfo($"[UpdateMonitorList] Color temp task added, total tasks: {colorTempTasks.Count}");
            }
        }

        Logger.LogInfo("[UpdateMonitorList] Monitor loop completed");

        Logger.LogInfo("[UpdateMonitorList] Calling OnPropertyChanged(HasMonitors)");
        OnPropertyChanged(nameof(HasMonitors));
        Logger.LogInfo("[UpdateMonitorList] Calling OnPropertyChanged(ShowNoMonitorsMessage)");
        OnPropertyChanged(nameof(ShowNoMonitorsMessage));
        Logger.LogInfo("[UpdateMonitorList] OnPropertyChanged calls completed");

        // Wait for color temperature initialization to complete before saving
        // This ensures we save the actual scanned values instead of defaults
        if (colorTempTasks.Count > 0)
        {
            Logger.LogInfo($"[UpdateMonitorList] Waiting for {colorTempTasks.Count} color temperature tasks to complete before saving...");

            // Use fire-and-forget async method to avoid blocking UI thread
            _ = WaitForColorTempAndSaveAsync(colorTempTasks);
        }
        else
        {
            // No color temperature tasks, save immediately
            Logger.LogInfo("[UpdateMonitorList] No color temperature tasks, calling SaveMonitorsToSettings immediately");
            SaveMonitorsToSettings();
            Logger.LogInfo("[UpdateMonitorList] SaveMonitorsToSettings completed");

            // Restore saved settings if enabled (async, don't block)
            Logger.LogInfo("[UpdateMonitorList] About to call ReloadMonitorSettingsAsync");
            _ = ReloadMonitorSettingsAsync(null);
            Logger.LogInfo("[UpdateMonitorList] ReloadMonitorSettingsAsync invoked (fire-and-forget)");
        }

        Logger.LogInfo("[UpdateMonitorList] Method returning");
    }

    private async Task WaitForColorTempAndSaveAsync(List<Task> colorTempTasks)
    {
        try
        {
            // Wait for all color temperature initialization tasks to complete
            await Task.WhenAll(colorTempTasks);
            Logger.LogInfo("[WaitForColorTempAndSaveAsync] Color temperature tasks completed");

            // Save monitor information to settings.json and reload settings
            // Must be done on UI thread since these methods access UI properties and observable collections
            Logger.LogInfo("[WaitForColorTempAndSaveAsync] Dispatching save and reload to UI thread");
            _dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    SaveMonitorsToSettings();
                    Logger.LogInfo("[WaitForColorTempAndSaveAsync] SaveMonitorsToSettings completed with actual color temp values");

                    // Restore saved settings if enabled (async)
                    Logger.LogInfo("[WaitForColorTempAndSaveAsync] About to call ReloadMonitorSettingsAsync");
                    await ReloadMonitorSettingsAsync(null); // Tasks already completed, pass null
                    Logger.LogInfo("[WaitForColorTempAndSaveAsync] ReloadMonitorSettingsAsync completed");
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
                Logger.LogInfo("[WaitForColorTempAndSaveAsync] SaveMonitorsToSettings completed (after error)");
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
            var hiddenMonitorIds = new HashSet<string>(
                settings.Properties.Monitors
                    .Where(m => m.IsHidden)
                    .Select(m => m.HardwareId));

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
    {
        foreach (var vm in Monitors)
        {
            if (vm.Id == monitorId)
            {
                return vm;
            }
        }

        return null;
    }

    /// <summary>
    /// Safe wrapper for initializing color temperature asynchronously
    /// </summary>
    private async Task InitializeColorTemperatureSafeAsync(string monitorId, MonitorViewModel vm)
    {
        try
        {
            await _monitorManager.InitializeColorTemperatureAsync(monitorId);

            // Update UI on dispatcher thread - get the monitor from manager
            var monitor = _monitorManager.GetMonitor(monitorId);
            if (monitor != null)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    // Update color temperature without triggering hardware write
                    vm.UpdatePropertySilently(nameof(vm.ColorTemperature), monitor.CurrentColorTemperature);
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to initialize color temperature for {monitorId}: {ex.Message}");
        }
    }

    // INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Apply all settings changes from Settings UI (IPC event handler entry point)
    /// Coordinates both UI configuration and hardware parameter updates
    /// </summary>
    public async void ApplySettingsFromUI()
    {
        try
        {
            Logger.LogInfo("[Settings] Processing settings update from Settings UI");

            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>("PowerDisplay");

            // 1. Apply UI configuration changes (synchronous, lightweight)
            ApplyUIConfiguration(settings);

            // 2. Apply hardware parameter changes (asynchronous, may involve DDC/CI calls)
            await ApplyHardwareParametersAsync(settings);

            Logger.LogInfo("[Settings] Settings update complete");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Settings] Failed to apply settings from UI: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply UI-only configuration changes (feature visibility toggles)
    /// Synchronous, lightweight operation
    /// </summary>
    private void ApplyUIConfiguration(PowerDisplaySettings settings)
    {
        try
        {
            Logger.LogInfo("[Settings] Applying UI configuration changes (feature visibility)");

            foreach (var monitorVm in Monitors)
            {
                ApplyFeatureVisibility(monitorVm, settings);
            }

            // Trigger UI refresh
            UIRefreshRequested?.Invoke(this, EventArgs.Empty);

            Logger.LogInfo("[Settings] UI configuration applied");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Settings] Failed to apply UI configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply hardware parameter changes (brightness, color temperature)
    /// Asynchronous operation that communicates with monitor hardware via DDC/CI
    /// Note: Contrast and volume are not currently adjustable from Settings UI
    /// </summary>
    private async Task ApplyHardwareParametersAsync(PowerDisplaySettings settings)
    {
        try
        {
            Logger.LogInfo("[Settings] Applying hardware parameter changes");

            var updateTasks = new List<Task>();

            foreach (var monitorVm in Monitors)
            {
                var hardwareId = monitorVm.HardwareId;
                var monitorSettings = settings.Properties.Monitors.FirstOrDefault(m => m.HardwareId == hardwareId);

                if (monitorSettings == null)
                {
                    continue;
                }

                // Apply brightness if changed
                if (monitorSettings.CurrentBrightness >= 0 &&
                    monitorSettings.CurrentBrightness != monitorVm.Brightness)
                {
                    Logger.LogInfo($"[Settings] Scheduling brightness update for {hardwareId}: {monitorSettings.CurrentBrightness}%");

                    var task = ApplyBrightnessAsync(monitorVm, monitorSettings.CurrentBrightness);
                    updateTasks.Add(task);
                }

                // Apply color temperature if changed
                // Note: ColorTemperature is now Settings UI-only (no flyout control)
                if (monitorSettings.ColorTemperature > 0 &&
                    monitorSettings.ColorTemperature != monitorVm.ColorTemperature)
                {
                    Logger.LogInfo($"[Settings] Scheduling color temperature update for {hardwareId}: 0x{monitorSettings.ColorTemperature:X2}");

                    var task = ApplyColorTemperatureAsync(monitorVm, monitorSettings.ColorTemperature);
                    updateTasks.Add(task);
                }

                // Note: Contrast and volume are adjusted in real-time via flyout UI,
                // not from Settings UI, so they don't need IPC handling here
            }

            // Wait for all hardware updates to complete
            if (updateTasks.Count > 0)
            {
                await Task.WhenAll(updateTasks);
                Logger.LogInfo($"[Settings] Completed {updateTasks.Count} hardware parameter updates");
            }
            else
            {
                Logger.LogInfo("[Settings] No hardware parameter changes detected");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Settings] Failed to apply hardware parameters: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply brightness to a specific monitor
    /// </summary>
    private async Task ApplyBrightnessAsync(MonitorViewModel monitorVm, int brightness)
    {
        // Use MonitorViewModel's unified method with immediate application (no debounce for IPC)
        await monitorVm.SetBrightnessAsync(brightness, immediate: true);
    }

    /// <summary>
    /// Apply color temperature to a specific monitor
    /// </summary>
    private async Task ApplyColorTemperatureAsync(MonitorViewModel monitorVm, int colorTemperature)
    {
        // Use MonitorViewModel's unified method
        await monitorVm.SetColorTemperatureAsync(colorTemperature);
    }

    /// <summary>
    /// Apply Settings UI configuration changes (feature visibility toggles only)
    /// OBSOLETE: Use ApplySettingsFromUI() instead
    /// </summary>
    [Obsolete("Use ApplySettingsFromUI() instead - this method only handles UI config, not hardware parameters")]
    public void ApplySettingsUIConfiguration()
    {
        try
        {
            Logger.LogInfo("[Settings] Applying Settings UI configuration changes (feature visibility only)");

            // Read current settings
            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>("PowerDisplay");

            // Update feature visibility for each monitor (UI configuration only)
            foreach (var monitorVm in Monitors)
            {
                ApplyFeatureVisibility(monitorVm, settings);
            }

            // Trigger UI refresh for configuration changes
            UIRefreshRequested?.Invoke(this, EventArgs.Empty);

            Logger.LogInfo($"[Settings] Settings UI configuration applied, monitor count: {settings.Properties.Monitors.Count}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Settings] Failed to apply Settings UI configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Reload monitor settings from configuration - ONLY called at startup
    /// </summary>
    /// <param name="colorTempInitTasks">Optional tasks for color temperature initialization to wait for</param>
    public async Task ReloadMonitorSettingsAsync(List<Task>? colorTempInitTasks = null)
    {
        Logger.LogInfo($"[ReloadMonitorSettingsAsync] Method called with {colorTempInitTasks?.Count ?? 0} color temp tasks");

        // Prevent duplicate calls
        if (IsLoading)
        {
            Logger.LogInfo("[Startup] ReloadMonitorSettingsAsync already in progress, skipping");
            return;
        }

        try
        {
            Logger.LogInfo("[ReloadMonitorSettingsAsync] Setting IsLoading = true");

            // Set loading state to block UI interactions
            IsLoading = true;
            Logger.LogInfo("[ReloadMonitorSettingsAsync] IsLoading set to true");

            StatusText = "Loading settings...";
            Logger.LogInfo("[ReloadMonitorSettingsAsync] StatusText updated");

            // Read current settings
            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>("PowerDisplay");

            if (settings.Properties.RestoreSettingsOnStartup)
            {
                // Restore saved settings from configuration file
                Logger.LogInfo("[Startup] RestoreSettingsOnStartup enabled - applying saved settings");

                foreach (var monitorVm in Monitors)
                {
                    var hardwareId = monitorVm.HardwareId;
                    Logger.LogInfo($"[Startup] Processing monitor: '{monitorVm.Name}', HardwareId: '{hardwareId}'");

                    // Find and apply corresponding saved settings from state file using stable HardwareId
                    var savedState = _stateManager.GetMonitorParameters(hardwareId);
                    if (savedState.HasValue)
                    {
                        Logger.LogInfo($"[Startup] Restoring state for HardwareId '{hardwareId}': Brightness={savedState.Value.Brightness}, ColorTemp={savedState.Value.ColorTemperature}");

                        // Validate and apply saved values (skip invalid values)
                        // Use UpdatePropertySilently to avoid triggering hardware updates during initialization
                        if (savedState.Value.Brightness >= monitorVm.MinBrightness && savedState.Value.Brightness <= monitorVm.MaxBrightness)
                        {
                            monitorVm.UpdatePropertySilently(nameof(monitorVm.Brightness), savedState.Value.Brightness);
                        }
                        else
                        {
                            Logger.LogWarning($"[Startup] Invalid brightness value {savedState.Value.Brightness} for HardwareId '{hardwareId}', skipping");
                        }

                        // Color temperature: VCP 0x14 preset value (discrete values, no range check needed)
                        // Note: ColorTemperature is now read-only in flyout UI, controlled via Settings UI
                        if (savedState.Value.ColorTemperature > 0)
                        {
                            // Validation will happen in Settings UI when applying preset values
                            monitorVm.UpdatePropertySilently(nameof(monitorVm.ColorTemperature), savedState.Value.ColorTemperature);
                        }

                        // Contrast validation - only apply if hardware supports it
                        if (monitorVm.ShowContrast &&
                            savedState.Value.Contrast >= monitorVm.MinContrast &&
                            savedState.Value.Contrast <= monitorVm.MaxContrast)
                        {
                            monitorVm.UpdatePropertySilently(nameof(monitorVm.Contrast), savedState.Value.Contrast);
                        }
                        else if (!monitorVm.ShowContrast)
                        {
                            Logger.LogInfo($"[Startup] Contrast not supported on HardwareId '{hardwareId}', skipping");
                        }

                        // Volume validation - only apply if hardware supports it
                        if (monitorVm.ShowVolume &&
                            savedState.Value.Volume >= monitorVm.MinVolume &&
                            savedState.Value.Volume <= monitorVm.MaxVolume)
                        {
                            monitorVm.UpdatePropertySilently(nameof(monitorVm.Volume), savedState.Value.Volume);
                        }
                        else if (!monitorVm.ShowVolume)
                        {
                            Logger.LogInfo($"[Startup] Volume not supported on HardwareId '{hardwareId}', skipping");
                        }
                    }
                    else
                    {
                        Logger.LogInfo($"[Startup] No saved state for HardwareId '{hardwareId}' - keeping current hardware values");
                    }

                    // Apply feature visibility settings using HardwareId
                    ApplyFeatureVisibility(monitorVm, settings);
                }

                StatusText = "Saved settings restored successfully";
            }
            else
            {
                // Save current hardware values to configuration file
                Logger.LogInfo("[Startup] RestoreSettingsOnStartup disabled - saving current hardware values");

                // Wait for color temperature initialization to complete (if any)
                if (colorTempInitTasks != null && colorTempInitTasks.Count > 0)
                {
                    Logger.LogInfo($"[Startup] Waiting for {colorTempInitTasks.Count} color temperature initialization tasks to complete...");
                    try
                    {
                        Logger.LogInfo("[Startup] Calling Task.WhenAll on color temp tasks");
                        await Task.WhenAll(colorTempInitTasks);
                        Logger.LogInfo("[Startup] Task.WhenAll completed successfully");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"[Startup] Some color temperature initializations failed: {ex.Message}");
                    }
                }
                else
                {
                    Logger.LogInfo("[Startup] No color temperature tasks to wait for");
                }

                foreach (var monitorVm in Monitors)
                {
                    // Save current hardware values to settings
                    SaveMonitorSettingDirect(monitorVm.HardwareId, "Brightness", monitorVm.Brightness);
                    SaveMonitorSettingDirect(monitorVm.HardwareId, "ColorTemperature", monitorVm.ColorTemperature);
                    SaveMonitorSettingDirect(monitorVm.HardwareId, "Contrast", monitorVm.Contrast);
                    SaveMonitorSettingDirect(monitorVm.HardwareId, "Volume", monitorVm.Volume);

                    Logger.LogInfo($"[Startup] Saved current values for Hardware ID '{monitorVm.HardwareId}': Brightness={monitorVm.Brightness}, ColorTemp={monitorVm.ColorTemperature}");

                    // Apply feature visibility settings
                    ApplyFeatureVisibility(monitorVm, settings);
                }

                // No need to flush - MonitorStateManager now saves directly!
                StatusText = "Current monitor values saved to state file";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[ReloadMonitorSettingsAsync] Exception caught: {ex.Message}");
            Logger.LogError($"[ReloadMonitorSettingsAsync] Stack trace: {ex.StackTrace}");
            StatusText = $"Failed to process settings: {ex.Message}";
        }
        finally
        {
            Logger.LogInfo("[ReloadMonitorSettingsAsync] In finally block, setting IsLoading = false");

            // Clear loading state to enable UI interactions
            IsLoading = false;
            Logger.LogInfo("[ReloadMonitorSettingsAsync] IsLoading set to false, method exiting");
        }
    }

    /// <summary>
    /// Apply feature visibility settings to a monitor ViewModel
    /// </summary>
    private void ApplyFeatureVisibility(MonitorViewModel monitorVm, PowerDisplaySettings settings)
    {
        var monitorSettings = settings.Properties.Monitors.FirstOrDefault(m =>
            m.HardwareId == monitorVm.HardwareId);

        if (monitorSettings != null)
        {
            Logger.LogInfo($"[Startup] Applying feature visibility for Hardware ID '{monitorVm.HardwareId}': Contrast={monitorSettings.EnableContrast}, Volume={monitorSettings.EnableVolume}");

            monitorVm.ShowContrast = monitorSettings.EnableContrast;
            monitorVm.ShowVolume = monitorSettings.EnableVolume;
        }
        else
        {
            Logger.LogWarning($"[Startup] No feature settings found for Hardware ID '{monitorVm.HardwareId}' - using defaults");
        }
    }

    /// <summary>
    /// Thread-safe save method that can be called from background threads.
    /// Does not access UI collections or update UI properties.
    /// </summary>
    public void SaveMonitorSettingDirect(string hardwareId, string property, int value)
    {
        try
        {
            // This is thread-safe - _stateManager has internal locking
            // No UI thread operations, no ObservableCollection access
            _stateManager.UpdateMonitorParameter(hardwareId, property, value);

            Logger.LogTrace($"[State] Queued setting change for HardwareId '{hardwareId}': {property}={value}");
        }
        catch (Exception ex)
        {
            // Only log, don't update UI from background thread
            Logger.LogError($"Failed to queue setting save for HardwareId '{hardwareId}': {ex.Message}");
        }
    }

    /// <summary>
    /// Reset a monitor to default values
    /// </summary>
    public void ResetMonitor(string monitorId)
    {
        try
        {
            var monitorVm = GetMonitorViewModel(monitorId);
            if (monitorVm != null)
            {
                // Apply default values
                monitorVm.Brightness = 30;

                // ColorTemperature is now read-only in flyout UI - controlled via Settings UI only
                monitorVm.Contrast = 50;
                monitorVm.Volume = 50;

                StatusText = $"Monitor {monitorVm.Name} reset to default values";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to reset monitor: {ex.Message}";
        }
    }

    /// <summary>
    /// Save monitor information to settings.json for Settings UI to read
    /// </summary>
    private void SaveMonitorsToSettings()
    {
        try
        {
            // Load current settings to preserve user preferences (including IsHidden)
            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);

            // Create lookup of existing monitors by HardwareId to preserve settings
            var existingMonitorSettings = settings.Properties.Monitors
                .ToDictionary(m => m.HardwareId, m => m);

            // Build monitor list using Settings UI's MonitorInfo model
            var monitors = new List<Microsoft.PowerToys.Settings.UI.Library.MonitorInfo>();

            foreach (var vm in Monitors)
            {
                var monitorInfo = new Microsoft.PowerToys.Settings.UI.Library.MonitorInfo(
                    name: vm.Name,
                    internalName: vm.Id,
                    hardwareId: vm.HardwareId,
                    communicationMethod: vm.CommunicationMethod,
                    monitorType: vm.IsInternal ? "Internal" : "External",
                    currentBrightness: vm.Brightness,
                    colorTemperature: vm.ColorTemperature)
                {
                    CapabilitiesRaw = vm.CapabilitiesRaw,
                    VcpCodes = vm.VcpCapabilitiesInfo?.SupportedVcpCodes
                        .OrderBy(kvp => kvp.Key)
                        .Select(kvp => $"0x{kvp.Key:X2}")
                        .ToList() ?? new List<string>(),
                    VcpCodesFormatted = vm.VcpCapabilitiesInfo?.SupportedVcpCodes
                        .OrderBy(kvp => kvp.Key)
                        .Select(kvp => FormatVcpCodeForDisplay(kvp.Key, kvp.Value))
                        .ToList() ?? new List<Microsoft.PowerToys.Settings.UI.Library.VcpCodeDisplayInfo>(),
                };

                // Preserve user settings from existing monitor if available
                if (existingMonitorSettings.TryGetValue(vm.HardwareId, out var existingMonitor))
                {
                    monitorInfo.IsHidden = existingMonitor.IsHidden;
                    monitorInfo.EnableContrast = existingMonitor.EnableContrast;
                    monitorInfo.EnableVolume = existingMonitor.EnableVolume;
                }

                monitors.Add(monitorInfo);
            }

            // Also add hidden monitors from existing settings (monitors that are hidden but still connected)
            foreach (var existingMonitor in settings.Properties.Monitors.Where(m => m.IsHidden))
            {
                // Only add if not already in the list (to avoid duplicates)
                if (!monitors.Any(m => m.HardwareId == existingMonitor.HardwareId))
                {
                    monitors.Add(existingMonitor);
                    Logger.LogInfo($"[SaveMonitorsToSettings] Preserving hidden monitor in settings: {existingMonitor.Name} (HardwareId: {existingMonitor.HardwareId})");
                }
            }

            // Update monitors list
            settings.Properties.Monitors = monitors;

            // Save back to settings.json using source-generated context for AOT
            _settingsUtils.SaveSettings(
                System.Text.Json.JsonSerializer.Serialize(settings, AppJsonContext.Default.PowerDisplaySettings),
                PowerDisplaySettings.ModuleName);

            Logger.LogInfo($"Saved {monitors.Count} monitors to settings.json ({Monitors.Count} visible, {monitors.Count - Monitors.Count} hidden)");

            // Signal Settings UI that monitor list has been updated
            SignalMonitorsRefreshEvent();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to save monitors to settings.json: {ex.Message}");
        }
    }

    /// <summary>
    /// Signal Settings UI that the monitor list has been refreshed
    /// </summary>
    private void SignalMonitorsRefreshEvent()
    {
        try
        {
            using (var eventHandle = new System.Threading.EventWaitHandle(
                false,
                System.Threading.EventResetMode.AutoReset,
                Constants.RefreshPowerDisplayMonitorsEvent()))
            {
                eventHandle.Set();
                Logger.LogInfo("Signaled refresh monitors event to Settings UI");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to signal refresh monitors event: {ex.Message}");
        }
    }

    /// <summary>
    /// Format VCP code information for display in Settings UI
    /// </summary>
    private Microsoft.PowerToys.Settings.UI.Library.VcpCodeDisplayInfo FormatVcpCodeForDisplay(byte code, VcpCodeInfo info)
    {
        var result = new Microsoft.PowerToys.Settings.UI.Library.VcpCodeDisplayInfo
        {
            Code = $"0x{code:X2}",
            Title = $"{info.Name} (0x{code:X2})",
        };

        if (info.IsContinuous)
        {
            result.Values = "Continuous range";
            result.HasValues = true;
        }
        else if (info.HasDiscreteValues)
        {
            var formattedValues = info.SupportedValues
                .Select(v => Core.Utils.VcpValueNames.GetFormattedName(code, v))
                .ToList();
            result.Values = $"Values: {string.Join(", ", formattedValues)}";
            result.HasValues = true;

            // Populate value list for Settings UI ComboBox
            // Store raw name (without formatting) so Settings UI can format it consistently
            result.ValueList = info.SupportedValues
                .Select(v => new Microsoft.PowerToys.Settings.UI.Library.VcpValueInfo
                {
                    Value = $"0x{v:X2}",
                    Name = Core.Utils.VcpValueNames.GetName(code, v),
                })
                .ToList();
        }
        else
        {
            result.HasValues = false;
        }

        return result;
    }

    // IDisposable
    public void Dispose()
    {
        try
        {
            // Cancel all async operations first
            _cancellationTokenSource?.Cancel();

            // No need to flush state - MonitorStateManager now saves directly on each update!
            // State is already persisted, no pending changes to wait for.

            // Quick cleanup of monitor view models
            try
            {
                foreach (var vm in Monitors)
                {
                    vm?.Dispose();
                }

                Monitors.Clear();
            }
            catch
            {
                /* Ignore cleanup errors */
            }

            // Release monitor manager
            try
            {
                _monitorManager?.Dispose();
            }
            catch
            {
                /* Ignore cleanup errors */
            }

            // Release state manager
            try
            {
                _stateManager?.Dispose();
            }
            catch
            {
                /* Ignore cleanup errors */
            }

            // Finally release cancellation token
            try
            {
                _cancellationTokenSource?.Dispose();
            }
            catch
            {
                /* Ignore cleanup errors */
            }

            GC.SuppressFinalize(this);
        }
        catch
        {
            // Ensure Dispose doesn't throw exceptions
        }
    }
}
