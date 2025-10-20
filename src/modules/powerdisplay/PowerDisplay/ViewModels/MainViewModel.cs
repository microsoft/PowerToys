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
    private FileSystemWatcher? _settingsWatcher;

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

        // Setup settings file monitoring
        SetupSettingsFileWatcher();

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

            // Discover monitors
            var monitors = await _monitorManager.DiscoverMonitorsAsync(_cancellationTokenSource.Token);

            // Update UI on the dispatcher thread
            _dispatcherQueue.TryEnqueue(() =>
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
            });
        }
        catch (Exception ex)
        {
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

        var colorTempTasks = new List<Task>();
        foreach (var monitor in monitors)
        {
            var vm = new MonitorViewModel(monitor, _monitorManager, this);
            Monitors.Add(vm);

            // Asynchronously initialize color temperature for DDC/CI monitors
            if (monitor.SupportsColorTemperature && monitor.Type == MonitorType.External)
            {
                var task = InitializeColorTemperatureSafeAsync(monitor.Id, vm);
                colorTempTasks.Add(task);
            }
        }

        OnPropertyChanged(nameof(HasMonitors));
        OnPropertyChanged(nameof(ShowNoMonitorsMessage));

        // Send monitor information to Settings UI via IPC
        SendMonitorInfoToSettingsUI();

        // Restore saved settings if enabled (async, don't block)
        // Pass color temperature initialization tasks so we can wait for them if needed
        _ = ReloadMonitorSettingsAsync(colorTempTasks);
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
            // Handle monitors being added or removed
            if (e.AddedMonitors.Count > 0)
            {
                foreach (var monitor in e.AddedMonitors)
                {
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

            // Send updated monitor list to Settings UI via IPC
            SendMonitorInfoToSettingsUI();
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
    /// Setup settings file watcher
    /// </summary>
    private void SetupSettingsFileWatcher()
    {
        try
        {
            var settingsPath = _settingsUtils.GetSettingsFilePath("PowerDisplay");
            var directory = Path.GetDirectoryName(settingsPath);
            var fileName = Path.GetFileName(settingsPath);

            if (!string.IsNullOrEmpty(directory))
            {
                // Ensure directory exists
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                _settingsWatcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true,
                };

                _settingsWatcher.Changed += OnSettingsFileChanged;
                _settingsWatcher.Created += OnSettingsFileChanged;

                Logger.LogInfo($"Settings file watcher setup for: {settingsPath}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to setup settings file watcher: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle settings file changes - only monitors UI configuration changes from Settings UI
    /// (monitor_state.json is managed separately and doesn't trigger this)
    /// </summary>
    private void OnSettingsFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            Logger.LogInfo($"Settings file changed by Settings UI: {e.FullPath}");

            // Add small delay to ensure file write completion
            Task.Delay(200).ContinueWith(_ =>
            {
                try
                {
                    // Read updated settings
                    var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>("PowerDisplay");

                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        // Update feature visibility for each monitor (UI configuration only)
                        foreach (var monitorVm in Monitors)
                        {
                            // Use HardwareId for lookup (unified identification)
                            Logger.LogInfo($"[Settings Update] Looking for monitor settings with Hardware ID: '{monitorVm.HardwareId}'");

                            var monitorSettings = settings.Properties.Monitors.FirstOrDefault(m =>
                                m.HardwareId == monitorVm.HardwareId);

                            if (monitorSettings != null)
                            {
                                Logger.LogInfo($"[Settings Update] Found monitor settings for Hardware ID '{monitorVm.HardwareId}': ColorTemp={monitorSettings.EnableColorTemperature}, Contrast={monitorSettings.EnableContrast}, Volume={monitorSettings.EnableVolume}");

                                // Update visibility flags based on Settings UI toggles
                                monitorVm.ShowColorTemperature = monitorSettings.EnableColorTemperature;
                                monitorVm.ShowContrast = monitorSettings.EnableContrast;
                                monitorVm.ShowVolume = monitorSettings.EnableVolume;
                            }
                            else
                            {
                                Logger.LogWarning($"[Settings Update] No monitor settings found for Hardware ID '{monitorVm.HardwareId}'");
                                Logger.LogInfo($"[Settings Update] Available monitors in settings:");
                                foreach (var availableMonitor in settings.Properties.Monitors)
                                {
                                    Logger.LogInfo($"  - Hardware: '{availableMonitor.HardwareId}', Name: '{availableMonitor.Name}'");
                                }
                            }
                        }

                        // Trigger UI refresh for configuration changes
                        UIRefreshRequested?.Invoke(this, EventArgs.Empty);
                    });

                    Logger.LogInfo($"Settings UI configuration reloaded, monitor count: {settings.Properties.Monitors.Count}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to reload settings: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error handling settings file change: {ex.Message}");
        }
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
    /// Reload monitor settings from configuration
    /// </summary>
    /// <param name="colorTempInitTasks">Optional tasks for color temperature initialization to wait for</param>
    public async Task ReloadMonitorSettingsAsync(List<Task>? colorTempInitTasks = null)
    {
        // Prevent duplicate calls
        if (IsLoading)
        {
            Logger.LogInfo("[Startup] ReloadMonitorSettingsAsync already in progress, skipping");
            return;
        }

        try
        {
            // Set loading state to block UI interactions
            IsLoading = true;
            StatusText = "Loading settings...";

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

                        // Color temperature must be valid and within range
                        if (savedState.Value.ColorTemperature > 0 &&
                            savedState.Value.ColorTemperature >= monitorVm.MinColorTemperature &&
                            savedState.Value.ColorTemperature <= monitorVm.MaxColorTemperature)
                        {
                            monitorVm.UpdatePropertySilently(nameof(monitorVm.ColorTemperature), savedState.Value.ColorTemperature);
                        }
                        else
                        {
                            Logger.LogWarning($"[Startup] Invalid color temperature value {savedState.Value.ColorTemperature} for HardwareId '{hardwareId}', skipping");
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
                    Logger.LogInfo("[Startup] Waiting for color temperature initialization to complete...");
                    try
                    {
                        await Task.WhenAll(colorTempInitTasks);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"[Startup] Some color temperature initializations failed: {ex.Message}");
                    }
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
            Logger.LogError($"Failed to reload/save settings: {ex.Message}");
            StatusText = $"Failed to process settings: {ex.Message}";
        }
        finally
        {
            // Clear loading state to enable UI interactions
            IsLoading = false;
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
            Logger.LogInfo($"[Startup] Applying feature visibility for Hardware ID '{monitorVm.HardwareId}': ColorTemp={monitorSettings.EnableColorTemperature}, Contrast={monitorSettings.EnableContrast}, Volume={monitorSettings.EnableVolume}");

            monitorVm.ShowColorTemperature = monitorSettings.EnableColorTemperature;
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
                monitorVm.ColorTemperature = 6500;
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
    /// Send monitor information to Settings UI via IPC (using standard Model)
    /// </summary>
    private void SendMonitorInfoToSettingsUI()
    {
        try
        {
            if (Monitors.Count == 0)
            {
                Logger.LogInfo("[IPC] No monitors to send to Settings UI");
                return;
            }

            // Build monitor data list
            var monitorsData = new List<MonitorInfoData>();

            foreach (var vm in Monitors)
            {
                var monitorData = new MonitorInfoData
                {
                    Name = vm.Name,
                    InternalName = vm.Id,
                    HardwareId = vm.HardwareId,
                    CommunicationMethod = GetCommunicationMethodString(vm.Type),
                    MonitorType = vm.Type.ToString(),
                    CurrentBrightness = vm.Brightness,
                    ColorTemperature = vm.ColorTemperature,
                };

                monitorsData.Add(monitorData);
            }

            // Use standard IPC Response Model with JsonSerializer (source-generated for AOT)
            var response = new PowerDisplayMonitorsIPCResponse(monitorsData);
            string jsonMessage = System.Text.Json.JsonSerializer.Serialize(response, AppJsonContext.Default.PowerDisplayMonitorsIPCResponse);

            // Send to Settings UI via IPC
            App.SendIPCMessage(jsonMessage);

            Logger.LogInfo($"[IPC] Sent {Monitors.Count} monitors to Settings UI");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[IPC] Failed to send monitor info: {ex.Message}");
        }
    }

    /// <summary>
    /// Get communication method string based on monitor type
    /// </summary>
    private string GetCommunicationMethodString(MonitorType type)
    {
        return type switch
        {
            MonitorType.External => "DDC/CI",
            MonitorType.Internal => "WMI",
            _ => "Unknown",
        };
    }

    // IDisposable
    public void Dispose()
    {
        try
        {
            // Cancel all async operations first
            _cancellationTokenSource?.Cancel();

            // Stop file monitoring immediately
            _settingsWatcher?.Dispose();
            _settingsWatcher = null;

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
