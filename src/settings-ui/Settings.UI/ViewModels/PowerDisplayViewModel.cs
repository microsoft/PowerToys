// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Microsoft.PowerToys.Settings.UI.Services;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class PowerDisplayViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }
        private ISettingsRepository<PowerDisplaySettings> _powerDisplayRepository;
        private FileSystemWatcher _settingsWatcher;

        public ButtonClickCommand LaunchEventHandler => new ButtonClickCommand(Launch);

        public ButtonClickCommand GetResetMonitorCommand(MonitorInfo monitor) => new ButtonClickCommand(() => ResetMonitorSettings(monitor));

        public PowerDisplayViewModel(ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<PowerDisplaySettings> powerDisplaySettingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            _powerDisplayRepository = powerDisplaySettingsRepository;
            _settings = powerDisplaySettingsRepository.SettingsConfig;

            InitializeEnabledValue();

            // Initialize monitors collection
            _monitors = new ObservableCollection<MonitorInfo>(_settings.Properties.Monitors);
            _hasMonitors = _monitors.Count > 0;

            // Subscribe to PropertyChanged events for existing monitors
            SubscribeToAllMonitorChanges();

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            // Subscribe to monitor information updates
            IPCResponseService.PowerDisplayMonitorsReceived += OnMonitorsReceived;

            // Don't setup settings file watcher for PowerDisplay's settings.json
            // as it creates circular dependencies and the two apps use different formats
            // SetupSettingsWatcher();
        }

        private void InitializeEnabledValue()
        {
            _isPowerDisplayEnabled = GeneralSettingsConfig.Enabled.PowerDisplay;
        }

        public bool IsPowerDisplayEnabled
        {
            get => _isPowerDisplayEnabled;
            set
            {
                if (_isPowerDisplayEnabled != value)
                {
                    _isPowerDisplayEnabled = value;
                    OnPropertyChanged(nameof(IsPowerDisplayEnabled));

                    GeneralSettingsConfig.Enabled.PowerDisplay = value;
                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());
                }
            }
        }

        public bool IsLaunchAtStartupEnabled
        {
            get => _settings.Properties.LaunchAtStartup;
            set
            {
                if (_settings.Properties.LaunchAtStartup != value)
                {
                    _settings.Properties.LaunchAtStartup = value;
                    OnPropertyChanged(nameof(IsLaunchAtStartupEnabled));

                    NotifySettingsChanged();
                }
            }
        }

        public bool RestoreSettingsOnStartup
        {
            get => _settings.Properties.RestoreSettingsOnStartup;
            set
            {
                if (_settings.Properties.RestoreSettingsOnStartup != value)
                {
                    _settings.Properties.RestoreSettingsOnStartup = value;
                    OnPropertyChanged(nameof(RestoreSettingsOnStartup));

                    NotifySettingsChanged();
                }
            }
        }

        public string Theme
        {
            get => _settings.Properties.Theme;
            set
            {
                if (_settings.Properties.Theme != value)
                {
                    _settings.Properties.Theme = value;
                    OnPropertyChanged(nameof(Theme));

                    NotifySettingsChanged();
                }
            }
        }

        public string BrightnessUpdateRate
        {
            get => _settings.Properties.BrightnessUpdateRate;
            set
            {
                if (_settings.Properties.BrightnessUpdateRate != value)
                {
                    _settings.Properties.BrightnessUpdateRate = value;
                    OnPropertyChanged(nameof(BrightnessUpdateRate));

                    NotifySettingsChanged();
                }
            }
        }

        public bool EnableMcpServer
        {
            get => _settings.Properties.EnableMcpServer;
            set
            {
                if (_settings.Properties.EnableMcpServer != value)
                {
                    _settings.Properties.EnableMcpServer = value;
                    OnPropertyChanged(nameof(EnableMcpServer));

                    NotifySettingsChanged();
                }
            }
        }

        public int McpServerPort
        {
            get => _settings.Properties.McpServerPort;
            set
            {
                if (_settings.Properties.McpServerPort != value)
                {
                    _settings.Properties.McpServerPort = value;
                    OnPropertyChanged(nameof(McpServerPort));

                    NotifySettingsChanged();
                }
            }
        }

        public bool McpAutoStart
        {
            get => _settings.Properties.McpAutoStart;
            set
            {
                if (_settings.Properties.McpAutoStart != value)
                {
                    _settings.Properties.McpAutoStart = value;
                    OnPropertyChanged(nameof(McpAutoStart));

                    NotifySettingsChanged();
                }
            }
        }

        private readonly List<string> _brightnessUpdateRateOptions = new List<string>
        {
            "never",
            "250ms",
            "500ms",
            "1s",
            "2s"
        };

        public List<string> BrightnessUpdateRateOptions => _brightnessUpdateRateOptions;

        private readonly List<string> _themeOptions = new List<string>
        {
            "Light",
            "Dark"
        };

        public List<string> ThemeOptions => _themeOptions;

        public ObservableCollection<MonitorInfo> Monitors
        {
            get => _monitors;
            set
            {
                if (_monitors != value)
                {
                    // Unsubscribe from old collection
                    if (_monitors != null)
                    {
                        UnsubscribeFromAllMonitorChanges();
                    }

                    _monitors = value;

                    // Subscribe to new collection
                    if (_monitors != null)
                    {
                        SubscribeToAllMonitorChanges();
                    }

                    OnPropertyChanged();
                }
            }
        }

        public bool HasMonitors
        {
            get => _hasMonitors;
            set
            {
                if (_hasMonitors != value)
                {
                    _hasMonitors = value;
                    OnPropertyChanged();
                }
            }
        }

        private void OnMonitorsReceived(object sender, MonitorInfo[] monitors)
        {
            UpdateMonitors(monitors);
        }

        public void UpdateMonitors(MonitorInfo[] monitors)
        {
            _isUpdatingSettings = true;
            try
            {
                if (monitors == null)
                {
                    // Unsubscribe from all existing monitors
                    UnsubscribeFromAllMonitorChanges();

                    _monitors.Clear();
                    HasMonitors = false;
                    _settings.Properties.Monitors = new List<MonitorInfo>();
                    NotifySettingsChanged();
                    return;
                }

                // Unsubscribe from all existing monitors
                UnsubscribeFromAllMonitorChanges();

                // Create a lookup of existing monitors to preserve user settings
                var existingMonitors = _monitors.ToDictionary(m => GetMonitorKey(m), m => m);

                _monitors.Clear();
                foreach (var newMonitor in monitors)
                {
                    var monitorKey = GetMonitorKey(newMonitor);

                    // Check if we have an existing monitor with the same key
                    if (existingMonitors.TryGetValue(monitorKey, out var existingMonitor))
                    {
                        // Preserve user settings from existing monitor
                        newMonitor.EnableColorTemperature = existingMonitor.EnableColorTemperature;
                        newMonitor.EnableContrast = existingMonitor.EnableContrast;
                        newMonitor.EnableVolume = existingMonitor.EnableVolume;
                        newMonitor.IsHidden = existingMonitor.IsHidden;
                    }

                    _monitors.Add(newMonitor);

                    // Subscribe to PropertyChanged for the new monitor
                    SubscribeToMonitorChanges(newMonitor);
                }

                // Update HasMonitors property
                HasMonitors = monitors.Length > 0;

                // Update settings
                _settings.Properties.Monitors = _monitors.ToList();
                NotifySettingsChanged();
            }
            finally
            {
                _isUpdatingSettings = false;
            }
        }

        /// <summary>
        /// Generate a unique key for monitor matching based on hardware ID and internal name
        /// </summary>
        private string GetMonitorKey(MonitorInfo monitor)
        {
            // Use hardware ID if available, otherwise fall back to internal name
            if (!string.IsNullOrEmpty(monitor.HardwareId))
            {
                return monitor.HardwareId;
            }

            return monitor.InternalName ?? monitor.Name ?? string.Empty;
        }

        public void Dispose()
        {
            // Unsubscribe from monitor property changes
            UnsubscribeFromAllMonitorChanges();

            // Unsubscribe from events
            IPCResponseService.PowerDisplayMonitorsReceived -= OnMonitorsReceived;

            // Clean up settings file watcher
            if (_settingsWatcher != null)
            {
                _settingsWatcher.Dispose();
            }
        }

        /// <summary>
        /// Subscribe to PropertyChanged events for all monitors in the collection
        /// </summary>
        private void SubscribeToAllMonitorChanges()
        {
            foreach (var monitor in _monitors)
            {
                monitor.PropertyChanged += OnMonitorPropertyChanged;
            }
        }

        /// <summary>
        /// Unsubscribe from PropertyChanged events for all monitors in the collection
        /// </summary>
        private void UnsubscribeFromAllMonitorChanges()
        {
            foreach (var monitor in _monitors)
            {
                monitor.PropertyChanged -= OnMonitorPropertyChanged;
            }
        }

        /// <summary>
        /// Subscribe to PropertyChanged event for a specific monitor
        /// </summary>
        private void SubscribeToMonitorChanges(MonitorInfo monitor)
        {
            monitor.PropertyChanged += OnMonitorPropertyChanged;
        }

        /// <summary>
        /// Unsubscribe from PropertyChanged event for a specific monitor
        /// </summary>
        private void UnsubscribeFromMonitorChanges(MonitorInfo monitor)
        {
            monitor.PropertyChanged -= OnMonitorPropertyChanged;
        }

        private bool _isUpdatingSettings = false;

        /// <summary>
        /// Handle PropertyChanged events from MonitorInfo objects
        /// </summary>
        private void OnMonitorPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Prevent infinite loops during settings updates
            if (_isUpdatingSettings) return;

            if (sender is MonitorInfo monitor)
            {
                System.Diagnostics.Debug.WriteLine($"[PowerDisplayViewModel] Monitor {monitor.Name} property {e.PropertyName} changed to: EnableColorTemp={monitor.EnableColorTemperature}, EnableContrast={monitor.EnableContrast}, EnableVolume={monitor.EnableVolume}");
            }
            
            // Update the settings object to keep it in sync
            _settings.Properties.Monitors = _monitors.ToList();

            // Save settings when any monitor property changes
            NotifySettingsChanged();

            System.Diagnostics.Debug.WriteLine($"[PowerDisplayViewModel] Monitor property changed: {e.PropertyName}");
        }

        public void Launch()
        {
            var actionName = "Launch";

            SendConfigMSG("{\"action\":{\"PowerDisplay\":{\"action_name\":\"" + actionName + "\", \"value\":\"\"}}}");
        }

        public void ResetMonitorSettings(MonitorInfo monitor)
        {
            if (monitor == null) return;

            try
            {
                // Reset monitor values to defaults
                monitor.CurrentBrightness = 30;
                monitor.ColorTemperature = 6500;

                // Update the saved settings with default values
                if (_settings.Properties.SavedMonitorSettings == null)
                {
                    _settings.Properties.SavedMonitorSettings = new Dictionary<string, MonitorSavedSettings>();
                }

                _settings.Properties.SavedMonitorSettings[monitor.InternalName] = new MonitorSavedSettings
                {
                    Brightness = 30,
                    ColorTemperature = 6500,
                    Contrast = 50,
                    Volume = 50,
                    LastUpdated = DateTime.Now
                };

                // Save settings - this will trigger PowerDisplay's file watcher to apply the reset values
                NotifySettingsChanged();
            }
            catch (Exception ex)
            {
                // Handle error gracefully
                System.Diagnostics.Debug.WriteLine($"Failed to reset monitor settings: {ex.Message}");
            }
        }

        private Func<string, int> SendConfigMSG { get; }

        private bool _isPowerDisplayEnabled;
        private PowerDisplaySettings _settings;
        private ObservableCollection<MonitorInfo> _monitors;
        private bool _hasMonitors;

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsPowerDisplayEnabled));
        }

        private void NotifySettingsChanged()
        {
            // Using InvariantCulture as this is an IPC message
            SendConfigMSG(
                   string.Format(
                       CultureInfo.InvariantCulture,
                       "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                       PowerDisplaySettings.ModuleName,
                       JsonSerializer.Serialize(_settings, SourceGenerationContextContext.Default.PowerDisplaySettings)));

            // Also save directly to PowerDisplay's settings file for immediate pickup
            SaveToPowerDisplaySettingsFile();
        }

        /// <summary>
        /// Save settings directly to PowerDisplay's settings.json file
        /// </summary>
        private void SaveToPowerDisplaySettingsFile()
        {
            try
            {
                var settingsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "PowerToys", "PowerDisplay", "settings.json");

                var directory = Path.GetDirectoryName(settingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var jsonString = JsonSerializer.Serialize(_settings, SourceGenerationContextContext.Default.PowerDisplaySettings);
                
                // Use retry logic to handle file access conflicts
                int retryCount = 3;
                for (int i = 0; i < retryCount; i++)
                {
                    try
                    {
                        File.WriteAllText(settingsPath, jsonString);
                        System.Diagnostics.Debug.WriteLine($"[PowerDisplayViewModel] Settings saved to PowerDisplay file: {settingsPath}");
                        break; // Success, exit retry loop
                    }
                    catch (IOException) when (i < retryCount - 1)
                    {
                        // File is locked, wait and retry
                        System.Threading.Thread.Sleep(100);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PowerDisplayViewModel] Failed to save to PowerDisplay settings file: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置设置文件监视器，当 PowerDisplay.exe 更新设置文件时自动刷新 UI
        /// </summary>
        private void SetupSettingsWatcher()
        {
            try
            {
                var settingsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "PowerToys", "PowerDisplay", "settings.json");

                var directory = Path.GetDirectoryName(settingsPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    // 确保目录存在
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    _settingsWatcher = new FileSystemWatcher(directory)
                    {
                        Filter = "settings.json",
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                        EnableRaisingEvents = true
                    };

                    _settingsWatcher.Changed += OnSettingsFileChanged;
                    _settingsWatcher.Created += OnSettingsFileChanged;

                    System.Diagnostics.Debug.WriteLine($"[PowerDisplayViewModel] Settings file watcher setup for: {settingsPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PowerDisplayViewModel] Failed to setup settings file watcher: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理设置文件变化事件
        /// </summary>
        private void OnSettingsFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[PowerDisplayViewModel] Settings file changed: {e.FullPath}");

                // 添加延迟确保文件写入完成
                Task.Delay(500).ContinueWith(_ =>
                {
                    try
                    {
                        Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            // 重新加载设置
                            if (_powerDisplayRepository.ReloadSettings())
                            {
                                var newSettings = _powerDisplayRepository.SettingsConfig;

                                // 更新监视器列表
                                UpdateMonitors(newSettings.Properties.Monitors.ToArray());

                                System.Diagnostics.Debug.WriteLine($"[PowerDisplayViewModel] Settings reloaded, monitor count: {newSettings.Properties.Monitors.Count}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PowerDisplayViewModel] Failed to reload settings: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PowerDisplayViewModel] Error handling settings file change: {ex.Message}");
            }
        }

    }
}
