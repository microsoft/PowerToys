// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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
using PowerDisplay.Core;
using PowerDisplay.Core.Interfaces;
using PowerDisplay.Core.Models;
using PowerDisplay.Helpers;
using Monitor = PowerDisplay.Core.Models.Monitor;

namespace PowerDisplay.ViewModels;

/// <summary>
/// Main ViewModel for the PowerDisplay application
/// </summary>
public class MainViewModel : INotifyPropertyChanged, IDisposable
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

    /// <summary>
    /// Event triggered when theme change is requested
    /// </summary>
    public event EventHandler<ElementTheme>? ThemeChangeRequested;

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
        _monitorManager.MonitorStatusChanged += OnMonitorStatusChanged;

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
            
            // Update all monitors' interaction state
            foreach (var monitor in Monitors)
            {
                monitor.IsInteractionEnabled = !value;
            }
        }
    }

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

        foreach (var monitor in monitors)
        {
            var vm = new MonitorViewModel(monitor, _monitorManager, this);
            Monitors.Add(vm);
            
            // Asynchronously initialize color temperature for DDC/CI monitors
            if (monitor.SupportsColorTemperature && monitor.Type == MonitorType.External)
            {
                _ = InitializeColorTemperatureSafeAsync(monitor.Id, vm);
            }
        }

        OnPropertyChanged(nameof(HasMonitors));
        OnPropertyChanged(nameof(ShowNoMonitorsMessage));

        // Restore saved settings if enabled (async, don't block)
        _ = ReloadMonitorSettingsAsync();
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
        });
    }

    private void OnMonitorStatusChanged(object? sender, MonitorStatusChangedEventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            var vm = GetMonitorViewModel(e.Monitor.Id);
            vm?.UpdateFromModel(e.Monitor);
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
                    EnableRaisingEvents = true
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
            Task.Delay(500).ContinueWith(_ =>
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
                            // Use converted internal name for lookup
                            var internalName = GetInternalName(monitorVm);
                            Logger.LogInfo($"[Settings Update] Looking for monitor settings with internal name: '{internalName}', Hardware ID: '{monitorVm.HardwareId}'");
                            
                            var monitorSettings = settings.Properties.Monitors.FirstOrDefault(m =>
                                m.InternalName == internalName || m.HardwareId == monitorVm.HardwareId);

                            if (monitorSettings != null)
                            {
                                Logger.LogInfo($"[Settings Update] Found monitor settings for '{internalName}': ColorTemp={monitorSettings.EnableColorTemperature}, Contrast={monitorSettings.EnableContrast}, Volume={monitorSettings.EnableVolume}");
                                
                                // Update visibility flags based on Settings UI toggles
                                monitorVm.ShowColorTemperature = monitorSettings.EnableColorTemperature;
                                monitorVm.ShowContrast = monitorSettings.EnableContrast;
                                monitorVm.ShowVolume = monitorSettings.EnableVolume;
                            }
                            else
                            {
                                Logger.LogWarning($"[Settings Update] No monitor settings found for '{internalName}' with Hardware ID '{monitorVm.HardwareId}'");
                                Logger.LogInfo($"[Settings Update] Available monitors in settings:");
                                foreach (var availableMonitor in settings.Properties.Monitors)
                                {
                                    Logger.LogInfo($"  - Internal: '{availableMonitor.InternalName}', Hardware: '{availableMonitor.HardwareId}', Name: '{availableMonitor.Name}'");
                                }
                            }
                        }

                        // Check for theme changes and apply them
                        var newTheme = PowerDisplay.Helpers.ThemeManager.GetThemeFromPowerToysSettings();
                        if (newTheme != ElementTheme.Default)
                        {
                            ThemeChangeRequested?.Invoke(this, newTheme);
                            Logger.LogInfo($"Theme change requested: {newTheme}");
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
                _dispatcherQueue.TryEnqueue(() => vm.UpdateFromModel(monitor));
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
    public async Task ReloadMonitorSettingsAsync()
    {
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
                    var internalName = GetInternalName(monitorVm);
                    Logger.LogInfo($"[Startup] Processing monitor: '{monitorVm.Name}', InternalName: '{internalName}'");

                    // Find and apply corresponding saved settings from state file
                    var savedState = _stateManager.GetMonitorParameters(internalName);
                    if (savedState.HasValue)
                    {
                        Logger.LogInfo($"[Startup] Restoring state for '{internalName}': Brightness={savedState.Value.Brightness}, ColorTemp={savedState.Value.ColorTemperature}");
                        
                        // Apply saved parameter values to UI and hardware
                        monitorVm.Brightness = savedState.Value.Brightness;
                        monitorVm.ColorTemperature = savedState.Value.ColorTemperature;
                        monitorVm.Contrast = savedState.Value.Contrast;
                        monitorVm.Volume = savedState.Value.Volume;
                    }
                    else
                    {
                        Logger.LogInfo($"[Startup] No saved state for '{internalName}' - keeping current hardware values");
                    }

                    // Apply feature visibility settings
                    ApplyFeatureVisibility(monitorVm, settings, internalName);
                }

                StatusText = "Applying settings...";
                
                // Wait for all hardware updates to complete
                await Task.WhenAll(Monitors.Select(m => m.FlushAllUpdatesAsync()));
                
                StatusText = "Saved settings restored successfully";
            }
            else
            {
                // Save current hardware values to configuration file
                Logger.LogInfo("[Startup] RestoreSettingsOnStartup disabled - saving current hardware values");
                
                foreach (var monitorVm in Monitors)
                {
                    var internalName = GetInternalName(monitorVm);
                    
                    // Save current hardware values to settings
                    SaveMonitorSetting(monitorVm.Id, "Brightness", monitorVm.Brightness);
                    SaveMonitorSetting(monitorVm.Id, "ColorTemperature", monitorVm.ColorTemperature);
                    SaveMonitorSetting(monitorVm.Id, "Contrast", monitorVm.Contrast);
                    SaveMonitorSetting(monitorVm.Id, "Volume", monitorVm.Volume);
                    
                    Logger.LogInfo($"[Startup] Saved current values for '{internalName}': Brightness={monitorVm.Brightness}, ColorTemp={monitorVm.ColorTemperature}");

                    // Apply feature visibility settings
                    ApplyFeatureVisibility(monitorVm, settings, internalName);
                }

                // Flush pending changes immediately
                await _stateManager.FlushAsync();
                
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
    private void ApplyFeatureVisibility(MonitorViewModel monitorVm, PowerDisplaySettings settings, string internalName)
    {
        var monitorSettings = settings.Properties.Monitors.FirstOrDefault(m =>
            m.InternalName == internalName || m.HardwareId == monitorVm.HardwareId);
        
        if (monitorSettings != null)
        {
            Logger.LogInfo($"[Startup] Applying feature visibility for '{internalName}': ColorTemp={monitorSettings.EnableColorTemperature}, Contrast={monitorSettings.EnableContrast}, Volume={monitorSettings.EnableVolume}");
            
            monitorVm.ShowColorTemperature = monitorSettings.EnableColorTemperature;
            monitorVm.ShowContrast = monitorSettings.EnableContrast;
            monitorVm.ShowVolume = monitorSettings.EnableVolume;
        }
        else
        {
            Logger.LogWarning($"[Startup] No feature settings found for '{internalName}' - using defaults");
        }
    }

    /// <summary>
    /// Save monitor settings to configuration file (one-way: save only, no read during runtime)
    /// </summary>
    public void SaveMonitorSetting(string monitorId, string property, int value)
    {
        try
        {
            // Find the monitor VM to get the converted internal name
            var monitorVm = GetMonitorViewModel(monitorId);
            if (monitorVm == null)
            {
                Logger.LogError($"Monitor not found for ID: {monitorId}");
                return;
            }

            // Use converted internal name for consistency with Settings UI
            var internalName = GetInternalName(monitorVm);

            // Update parameter in state file (lock-free, non-blocking)
            _stateManager.UpdateMonitorParameter(internalName, property, value);
            
            Logger.LogTrace($"[State] Queued setting change for '{internalName}': {property}={value}");
        }
        catch (Exception ex)
        {
            // Log error but don't interrupt user operation
            Logger.LogError($"Failed to queue setting save: {ex.Message}");
            StatusText = $"Warning: Failed to save settings: {ex.Message}";
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
    /// Convert monitor ID to the internal name format used by Settings UI
    /// </summary>
    public string GetInternalName(MonitorViewModel monitor)
    {
        // For internal displays, use "Internal Display"
        if (monitor.Type == MonitorType.Internal)
        {
            return "Internal Display";
        }

        // For external monitors, remove technical prefix to match SettingsManager logic
        var id = monitor.Id;
        if (!string.IsNullOrEmpty(id) && id.StartsWith("DDC_", StringComparison.Ordinal))
        {
            return id.Substring(4); // Remove "DDC_" prefix to match SettingsManager.GetInternalName
        }

        // Use the full ID if no prefix found
        if (!string.IsNullOrEmpty(id))
        {
            return id;
        }

        // Use hardware ID as secondary option if unique ID is not available
        if (!string.IsNullOrEmpty(monitor.HardwareId))
        {
            return monitor.HardwareId;
        }

        // For external monitors, try to use a clean identifier
        if (!string.IsNullOrEmpty(monitor.Name))
        {
            return monitor.Name;
        }

        // Fall back to a default identifier if nothing else works
        return "Unknown Monitor";
    }

    // IDisposable
    public void Dispose()
    {
        try
        {
            // 首先取消所有异步操作
            _cancellationTokenSource?.Cancel();

            // 立即停止文件监控
            _settingsWatcher?.Dispose();
            _settingsWatcher = null;

            // Flush any unsaved state immediately (synchronously wait)
            try
            {
                // Use Task.Run to avoid deadlock and wait with timeout
                if (_stateManager != null)
                {
                    var flushTask = _stateManager.FlushAsync();
                    if (!flushTask.Wait(TimeSpan.FromSeconds(2)))
                    {
                        Logger.LogWarning("State flush timed out during dispose");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to flush state during dispose: {ex.Message}");
            }

            // 快速清理监控器视图模型
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
                /* 忽略清理错误 */
            }

            // 释放监控器管理器
            try
            {
                _monitorManager?.Dispose();
            }
            catch
            {
                /* 忽略清理错误 */
            }

            // 释放状态管理器
            try
            {
                _stateManager?.Dispose();
            }
            catch
            {
                /* 忽略清理错误 */
            }

            // 最后释放取消令牌
            try
            {
                _cancellationTokenSource?.Dispose();
            }
            catch
            {
                /* 忽略清理错误 */
            }
        }
        catch
        {
            // 确保 Dispose 不会抛出异常
        }
    }

    /// <summary>
    /// ViewModel for individual monitor
    /// </summary>
    public class MonitorViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly Monitor _monitor;
        private readonly MonitorManager _monitorManager;
        private readonly MainViewModel _mainViewModel;
        // Property managers for preventing race conditions
        private readonly MonitorPropertyManager _brightnessManager;
        private readonly MonitorPropertyManager _colorTemperatureManager;
        private readonly MonitorPropertyManager _contrastManager;
        private readonly MonitorPropertyManager _volumeManager;
        private int _brightness;
        private int _colorTemperature;
        private int _contrast;
        private int _volume;
        private bool _isAvailable;
        private bool _isUpdating;
        private bool _isInteractionEnabled = true;

        // Visibility settings (controlled by Settings UI)
        private bool _showColorTemperature;
        private bool _showContrast;
        private bool _showVolume;

        // User intent tracking for smooth slider operation
        private int _targetBrightness = -1;
        private int _targetColorTemperature = -1;
        private int _targetContrast = -1;
        private int _targetVolume = -1;
        private DateTime _lastUserInteraction = DateTime.MinValue;

        public MonitorViewModel(Monitor monitor, MonitorManager monitorManager, MainViewModel mainViewModel)
        {
            _monitor = monitor;
            _monitorManager = monitorManager;
            _mainViewModel = mainViewModel;

            // Initialize property managers
            _brightnessManager = new MonitorPropertyManager(monitor.Id, nameof(Brightness));
            _colorTemperatureManager = new MonitorPropertyManager(monitor.Id, nameof(ColorTemperature));
            _contrastManager = new MonitorPropertyManager(monitor.Id, nameof(Contrast));
            _volumeManager = new MonitorPropertyManager(monitor.Id, nameof(Volume));

            // Initialize Show properties based on hardware capabilities
            _showColorTemperature = monitor.SupportsColorTemperature; // Only show for DDC/CI monitors that support it
            _showContrast = monitor.SupportsContrast;
            _showVolume = monitor.SupportsVolume;

            // Try to get current color temperature via DDC/CI, use default if failed
            try
            {
                // For DDC/CI monitors that support color temperature, use 6500K as default
                // The actual temperature will be loaded asynchronously after construction
                if (monitor.SupportsColorTemperature)
                {
                    _colorTemperature = 6500; // Default neutral temperature for DDC monitors
                }
                else
                {
                    _colorTemperature = 6500; // Default for unsupported monitors
                }
                monitor.CurrentColorTemperature = _colorTemperature;
                Logger.LogDebug($"Initialized {monitor.Id} with default color temperature {_colorTemperature}K");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to initialize color temperature for {monitor.Id}: {ex.Message}");
                _colorTemperature = 6500; // Default neutral temperature
                monitor.CurrentColorTemperature = 6500;
            }

            UpdateFromModel(monitor);
        }

        public string Id => _monitor.Id;

        public string HardwareId => _monitor.HardwareId;

        public string Name => _monitor.Name;

        public string Manufacturer => _monitor.Manufacturer;

        public MonitorType Type => _monitor.Type;

        public string TypeDisplay => Type == MonitorType.Internal ? "Internal" : "External";

        // Monitor property ranges
        public int MinBrightness => _monitor.MinBrightness;
        public int MaxBrightness => _monitor.MaxBrightness;
        public int MinColorTemperature => _monitor.MinColorTemperature;
        public int MaxColorTemperature => _monitor.MaxColorTemperature;
        public int MinContrast => _monitor.MinContrast;
        public int MaxContrast => _monitor.MaxContrast;
        public int MinVolume => _monitor.MinVolume;
        public int MaxVolume => _monitor.MaxVolume;

        // Advanced control display logic
        public bool HasAdvancedControls => ShowColorTemperature || ShowContrast || ShowVolume;

        public bool ShowColorTemperature
        {
            get => _showColorTemperature;
            set
            {
                if (_showColorTemperature != value)
                {
                    _showColorTemperature = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasAdvancedControls));
                }
            }
        }

        public bool ShowContrast
        {
            get => _showContrast;
            set
            {
                if (_showContrast != value)
                {
                    _showContrast = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasAdvancedControls));
                }
            }
        }

        public bool ShowVolume
        {
            get => _showVolume;
            set
            {
                if (_showVolume != value)
                {
                    _showVolume = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasAdvancedControls));
                }
            }
        }


        public int Brightness
        {
            get => _brightness;
            set
            {
                if (_brightness != value)
                {
                    // 立即更新UI状态 - 保持滑块流畅
                    _brightness = value;
                    _targetBrightness = value; // Record user intent
                    _lastUserInteraction = DateTime.Now;
                    OnPropertyChanged(); // UI立即响应
                    
                    // 队列硬件更新 - 智能错误处理：只在队列最后失败时回滚
                    _brightnessManager.QueueUpdate(value, async (brightness, cancellationToken) =>
                    {
                        try
                        {
                            IsUpdating = true;
                            await _monitorManager.SetBrightnessAsync(Id, brightness, cancellationToken);
                            
                            // 硬件更新成功后保存配置（异步，不阻塞UI）
                            _mainViewModel?._dispatcherQueue.TryEnqueue(() =>
                            {
                                _mainViewModel.SaveMonitorSetting(Id, "Brightness", brightness);
                            });
                            
                            return true; // 成功
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to set brightness for {Id}: {ex.Message}");
                            return false; // 失败
                        }
                        finally
                        {
                            IsUpdating = false;
                        }
                    });
                }
            }
        }

        public int ColorTemperature
        {
            get => _colorTemperature;
            set
            {
                if (_colorTemperature != value)
                {
                    _colorTemperature = value;
                    _targetColorTemperature = value; // Record user intent
                    _lastUserInteraction = DateTime.Now;
                    OnPropertyChanged();
                    
                    // 队列硬件更新 - 智能错误处理：只在队列最后失败时回滚
                    _colorTemperatureManager.QueueUpdate(value, async (temperature, cancellationToken) =>
                    {
                        try
                        {
                            IsUpdating = true;
                            Logger.LogDebug($"[{Id}] Setting color temperature to {temperature}K via DDC/CI");
                            
                            // 直接使用MonitorManager的DDC/CI色温控制
                            var result = await _monitorManager.SetColorTemperatureAsync(Id, temperature, cancellationToken);
                            
                            if (result.IsSuccess)
                            {
                                _monitor.CurrentColorTemperature = temperature;
                                Logger.LogInfo($"[{Id}] Successfully set color temperature to {temperature}K via DDC/CI");
                                
                                // 硬件更新成功后保存配置（异步，不阻塞UI）
                                _mainViewModel?._dispatcherQueue.TryEnqueue(() =>
                                {
                                    _mainViewModel.SaveMonitorSetting(Id, "ColorTemperature", temperature);
                                });
                                
                                return true; // 成功
                            }
                            else
                            {
                                Logger.LogError($"[{Id}] Failed to set color temperature via DDC/CI: {result.ErrorMessage}");
                                return false; // 失败
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to set color temperature for {Id}: {ex.Message}");
                            return false; // 失败
                        }
                        finally
                        {
                            IsUpdating = false;
                        }
                    });
                }
            }
        }

        public int Contrast
        {
            get => _contrast;
            set
            {
                if (_contrast != value)
                {
                    _contrast = value;
                    _targetContrast = value; // Record user intent
                    _lastUserInteraction = DateTime.Now;
                    OnPropertyChanged();
                    
                    // 队列硬件更新 - 智能错误处理：只在队列最后失败时回滚
                    _contrastManager.QueueUpdate(value, async (contrast, cancellationToken) =>
                    {
                        try
                        {
                            IsUpdating = true;
                            await _monitorManager.SetContrastAsync(Id, contrast, cancellationToken);
                            
                            // 硬件更新成功后保存配置（异步，不阻塞UI）
                            _mainViewModel?._dispatcherQueue.TryEnqueue(() =>
                            {
                                _mainViewModel.SaveMonitorSetting(Id, "Contrast", contrast);
                            });
                            
                            return true; // 成功
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to set contrast for {Id}: {ex.Message}");
                            return false; // 失败
                        }
                        finally
                        {
                            IsUpdating = false;
                        }
                    });
                }
            }
        }

        public int Volume
        {
            get => _volume;
            set
            {
                if (_volume != value)
                {
                    _volume = value;
                    _targetVolume = value; // Record user intent
                    _lastUserInteraction = DateTime.Now;
                    OnPropertyChanged();
                    
                    // 队列硬件更新 - 智能错误处理：只在队列最后失败时回滚
                    _volumeManager.QueueUpdate(value, async (volume, cancellationToken) =>
                    {
                        try
                        {
                            IsUpdating = true;
                            await _monitorManager.SetVolumeAsync(Id, volume, cancellationToken);
                            
                            // 硬件更新成功后保存配置（异步，不阻塞UI）
                            _mainViewModel?._dispatcherQueue.TryEnqueue(() =>
                            {
                                _mainViewModel.SaveMonitorSetting(Id, "Volume", volume);
                            });
                            
                            return true; // 成功
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to set volume for {Id}: {ex.Message}");
                            return false; // 失败
                        }
                        finally
                        {
                            IsUpdating = false;
                        }
                    });
                }
            }
        }

        public bool IsAvailable
        {
            get => _isAvailable;
            set
            {
                _isAvailable = value;
                OnPropertyChanged();
            }
        }

        public bool IsUpdating
        {
            get => _isUpdating;
            set
            {
                _isUpdating = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets whether user interaction is enabled (not loading)
        /// </summary>
        public bool IsInteractionEnabled
        {
            get => _isInteractionEnabled;
            set
            {
                if (_isInteractionEnabled != value)
                {
                    _isInteractionEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand SetBrightnessCommand => new RelayCommand<int?>((brightness) =>
        {
            if (brightness.HasValue)
            {
                Brightness = brightness.Value;
            }
        });

        public ICommand SetColorTemperatureCommand => new RelayCommand<int?>((temperature) =>
        {
            if (temperature.HasValue && _monitor.SupportsColorTemperature)
            {
                Logger.LogDebug($"[{Id}] Color temperature command: {temperature.Value}K (DDC/CI)");
                ColorTemperature = temperature.Value;
            }
            else if (temperature.HasValue && !_monitor.SupportsColorTemperature)
            {
                Logger.LogWarning($"[{Id}] Color temperature not supported on this monitor");
            }
        });

        public ICommand SetContrastCommand => new RelayCommand<int?>((contrast) =>
        {
            if (contrast.HasValue)
            {
                Contrast = contrast.Value;
            }
        });

        public ICommand SetVolumeCommand => new RelayCommand<int?>((volume) =>
        {
            if (volume.HasValue)
            {
                Volume = volume.Value;
            }
        });

        public void UpdateFromModel(Monitor monitor)
        {
            bool brightnessChanged = false;
            bool colorTemperatureChanged = false;
            bool contrastChanged = false;
            bool volumeChanged = false;

            // Smart update: only update if necessary to prevent slider bouncing
            if (ShouldUpdateValue(_brightness, monitor.CurrentBrightness, ref _targetBrightness, nameof(Brightness)))
            {
                _brightness = monitor.CurrentBrightness;
                brightnessChanged = true;
            }

            if (ShouldUpdateValue(_colorTemperature, monitor.CurrentColorTemperature, ref _targetColorTemperature, nameof(ColorTemperature)))
            {
                _colorTemperature = monitor.CurrentColorTemperature;
                colorTemperatureChanged = true;
            }

            if (ShouldUpdateValue(_contrast, monitor.CurrentContrast, ref _targetContrast, nameof(Contrast)))
            {
                _contrast = monitor.CurrentContrast;
                contrastChanged = true;
            }

            if (ShouldUpdateValue(_volume, monitor.CurrentVolume, ref _targetVolume, nameof(Volume)))
            {
                _volume = monitor.CurrentVolume;
                volumeChanged = true;
            }

            // Always update availability
            if (_isAvailable != monitor.IsAvailable)
            {
                _isAvailable = monitor.IsAvailable;
                OnPropertyChanged(nameof(IsAvailable));
            }

            // Notify property changes only for values that actually changed
            if (brightnessChanged) OnPropertyChanged(nameof(Brightness));
            if (colorTemperatureChanged) OnPropertyChanged(nameof(ColorTemperature));
            if (contrastChanged) OnPropertyChanged(nameof(Contrast));
            if (volumeChanged) OnPropertyChanged(nameof(Volume));
        }

        private async Task UpdateBrightnessAsync(int brightness)
        {
            if (IsUpdating)
            {
                return;
            }

            try
            {
                IsUpdating = true;
                await _monitorManager.SetBrightnessAsync(Id, brightness);
            }
            catch
            {
                // Revert on error
                _brightness = _monitor.CurrentBrightness;
                OnPropertyChanged(nameof(Brightness));
            }
            finally
            {
                IsUpdating = false;
            }
        }

        private async Task UpdateColorTemperatureAsync(int temperature)
        {
            if (IsUpdating)
            {
                return;
            }

            try
            {
                IsUpdating = true;
                Logger.LogDebug($"[{Id}] Updating color temperature to {temperature}K via DDC/CI");
                
                var result = await _monitorManager.SetColorTemperatureAsync(Id, temperature);
                if (result.IsSuccess)
                {
                    _monitor.CurrentColorTemperature = temperature;
                    Logger.LogDebug($"[{Id}] Successfully updated color temperature to {temperature}K");
                }
                else
                {
                    Logger.LogError($"[{Id}] Failed to update color temperature: {result.ErrorMessage}");
                    // Revert on error
                    _colorTemperature = _monitor.CurrentColorTemperature;
                    OnPropertyChanged(nameof(ColorTemperature));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[{Id}] Exception updating color temperature: {ex.Message}");
                // Revert on error
                _colorTemperature = _monitor.CurrentColorTemperature;
                OnPropertyChanged(nameof(ColorTemperature));
            }
            finally
            {
                IsUpdating = false;
            }
        }

        private async Task UpdateContrastAsync(int contrast)
        {
            if (IsUpdating)
            {
                return;
            }

            try
            {
                IsUpdating = true;
                await _monitorManager.SetContrastAsync(_monitor.Id, contrast);
            }
            catch
            {
                // Revert on error
                _contrast = _monitor.CurrentContrast;
                OnPropertyChanged(nameof(Contrast));
            }
            finally
            {
                IsUpdating = false;
            }
        }

        private async Task UpdateVolumeAsync(int volume)
        {
            if (IsUpdating)
            {
                return;
            }

            try
            {
                IsUpdating = true;
                await _monitorManager.SetVolumeAsync(_monitor.Id, volume);
            }
            catch
            {
                // Revert on error
                _volume = _monitor.CurrentVolume;
                OnPropertyChanged(nameof(Volume));
            }
            finally
            {
                IsUpdating = false;
            }
        }

        /// <summary>
        /// Determines if a UI value should be updated based on hardware feedback
        /// to prevent slider bouncing during user interaction
        /// </summary>
        private bool ShouldUpdateValue(int currentValue, int hardwareValue, ref int targetValue, string propertyName)
        {
            // 1. If user just interacted (800ms), don't update to preserve smooth dragging
            //    增加了时间窗口，因为现在有渐进式更新需要更多时间
            if ((DateTime.Now - _lastUserInteraction).TotalMilliseconds < 800)
            {
                return false;
            }

            // 2. If hardware value reached target, reset target and don't update UI
            if (targetValue != -1 && Math.Abs(hardwareValue - targetValue) <= 2)
            {
                targetValue = -1; // Reset target
                return false;
            }

            // 3. Only update if there's a significant difference and user isn't actively dragging
            if (Math.Abs(currentValue - hardwareValue) > 3)
            {
                return true;
            }

            return false;
        }

        // Percentage-based properties for uniform slider behavior
        public int ColorTemperaturePercent
        {
            get => MapToPercent(_colorTemperature, MinColorTemperature, MaxColorTemperature);
            set
            {
                var actualValue = MapFromPercent(value, MinColorTemperature, MaxColorTemperature);
                ColorTemperature = actualValue;
            }
        }

        public int ContrastPercent
        {
            get => MapToPercent(_contrast, MinContrast, MaxContrast);
            set
            {
                var actualValue = MapFromPercent(value, MinContrast, MaxContrast);
                Contrast = actualValue;
            }
        }

        // Mapping functions for percentage conversion
        private int MapToPercent(int value, int min, int max)
        {
            if (max <= min) return 0;
            return (int)Math.Round((value - min) * 100.0 / (max - min));
        }

        private int MapFromPercent(int percent, int min, int max)
        {
            if (max <= min) return min;
            percent = Math.Clamp(percent, 0, 100);
            return min + (int)Math.Round(percent * (max - min) / 100.0);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            // Notify percentage properties when actual values change
            if (propertyName == nameof(ColorTemperature))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorTemperaturePercent)));
            }
            else if (propertyName == nameof(Contrast))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContrastPercent)));
            }
        }

        /// <summary>
        /// Wait for all pending property updates to complete
        /// </summary>
        public async Task FlushAllUpdatesAsync()
        {
            await Task.WhenAll(
                _brightnessManager.FlushAsync(),
                _colorTemperatureManager.FlushAsync(),
                _contrastManager.FlushAsync(),
                _volumeManager.FlushAsync());
        }

        public void Dispose()
        {
            // Dispose property managers
            _brightnessManager?.Dispose();
            _colorTemperatureManager?.Dispose();
            _contrastManager?.Dispose();
            _volumeManager?.Dispose();
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Generic relay command implementation
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;

        public void Execute(object? parameter) => _execute((T?)parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
