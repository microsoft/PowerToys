// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Microsoft.PowerToys.Settings.UI.Services;
using PowerToys.Interop;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class PowerDisplayViewModel : PageViewModelBase
    {
        protected override string ModuleName => PowerDisplaySettings.ModuleName;

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private ISettingsUtils SettingsUtils { get; set; }

        public ButtonClickCommand LaunchEventHandler => new ButtonClickCommand(Launch);

        public PowerDisplayViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<PowerDisplaySettings> powerDisplaySettingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            SettingsUtils = settingsUtils;
            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            _settings = powerDisplaySettingsRepository.SettingsConfig;

            InitializeEnabledValue();

            // Initialize monitors collection using property setter for proper subscription setup
            // Parse capabilities for each loaded monitor to ensure UI displays correctly
            var loadedMonitors = _settings.Properties.Monitors;

            Logger.LogInfo($"[Constructor] Initializing with {loadedMonitors.Count} monitors from settings");

            foreach (var monitor in loadedMonitors)
            {
                // Parse capabilities to determine feature support
                ParseFeatureSupportFromCapabilities(monitor);
            }

            Monitors = new ObservableCollection<MonitorInfo>(loadedMonitors);

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            // Listen for monitor refresh events from PowerDisplay.exe
            NativeEventWaiter.WaitForEventLoop(
                Constants.RefreshPowerDisplayMonitorsEvent(),
                () =>
                {
                    Logger.LogInfo("Received refresh monitors event from PowerDisplay.exe");
                    ReloadMonitorsFromSettings();
                });
        }

        private void InitializeEnabledValue()
        {
            _isEnabled = GeneralSettingsConfig.Enabled.PowerDisplay;
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));

                    GeneralSettingsConfig.Enabled.PowerDisplay = value;
                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());
                }
            }
        }

        public bool RestoreSettingsOnStartup
        {
            get => _settings.Properties.RestoreSettingsOnStartup;
            set => SetSettingsProperty(_settings.Properties.RestoreSettingsOnStartup, value, v => _settings.Properties.RestoreSettingsOnStartup = v);
        }

        public HotkeySettings ActivationShortcut
        {
            get => _settings.Properties.ActivationShortcut;
            set => SetSettingsProperty(_settings.Properties.ActivationShortcut, value, v => _settings.Properties.ActivationShortcut = v);
        }

        public string BrightnessUpdateRate
        {
            get => _settings.Properties.BrightnessUpdateRate;
            set => SetSettingsProperty(_settings.Properties.BrightnessUpdateRate, value, v => _settings.Properties.BrightnessUpdateRate = v);
        }

        private readonly List<string> _brightnessUpdateRateOptions = new List<string>
        {
            "never",
            "250ms",
            "500ms",
            "1s",
            "2s",
        };

        public List<string> BrightnessUpdateRateOptions => _brightnessUpdateRateOptions;

        public ObservableCollection<MonitorInfo> Monitors
        {
            get => _monitors;
            set
            {
                if (_monitors != null)
                {
                    _monitors.CollectionChanged -= Monitors_CollectionChanged;
                    UnsubscribeFromItemPropertyChanged(_monitors);
                }

                _monitors = value;

                if (_monitors != null)
                {
                    _monitors.CollectionChanged += Monitors_CollectionChanged;
                    SubscribeToItemPropertyChanged(_monitors);
                }

                OnPropertyChanged(nameof(Monitors));
                HasMonitors = _monitors?.Count > 0;
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

        private void Monitors_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            SubscribeToItemPropertyChanged(e.NewItems?.Cast<MonitorInfo>());
            UnsubscribeFromItemPropertyChanged(e.OldItems?.Cast<MonitorInfo>());

            HasMonitors = _monitors.Count > 0;
            _settings.Properties.Monitors = _monitors.ToList();
            NotifySettingsChanged();
        }

        public void UpdateMonitors(MonitorInfo[] monitors)
        {
            if (monitors == null)
            {
                Monitors = new ObservableCollection<MonitorInfo>();
                return;
            }

            // Create a lookup of existing monitors to preserve user settings
            var existingMonitors = _monitors.ToDictionary(m => GetMonitorKey(m), m => m);

            // Create new collection with merged settings
            var newCollection = new ObservableCollection<MonitorInfo>();
            foreach (var newMonitor in monitors)
            {
                var monitorKey = GetMonitorKey(newMonitor);

                // Parse capabilities to determine feature support
                ParseFeatureSupportFromCapabilities(newMonitor);

                // Check if we have an existing monitor with the same key
                if (existingMonitors.TryGetValue(monitorKey, out var existingMonitor))
                {
                    // Preserve user settings from existing monitor
                    newMonitor.EnableContrast = existingMonitor.EnableContrast;
                    newMonitor.EnableVolume = existingMonitor.EnableVolume;
                    newMonitor.IsHidden = existingMonitor.IsHidden;
                }

                newCollection.Add(newMonitor);
            }

            // Replace collection - property setter handles subscription management
            Monitors = newCollection;
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

        /// <summary>
        /// Parse feature support from capabilities VcpCodes list
        /// Sets support flags based on VCP code presence
        /// </summary>
        private void ParseFeatureSupportFromCapabilities(MonitorInfo monitor)
        {
            if (monitor == null)
            {
                return;
            }

            // Check capabilities status
            if (string.IsNullOrEmpty(monitor.CapabilitiesRaw))
            {
                monitor.CapabilitiesStatus = "unavailable";
                monitor.SupportsBrightness = false;
                monitor.SupportsContrast = false;
                monitor.SupportsColorTemperature = false;
                monitor.SupportsVolume = false;
                return;
            }

            monitor.CapabilitiesStatus = "available";

            // Parse VCP codes to determine feature support
            // VCP codes are stored as hex strings (e.g., "0x10", "10")
            var vcpCodes = monitor.VcpCodes ?? new List<string>();

            // Convert all VCP codes to integers for comparison
            var vcpCodeInts = new HashSet<int>();
            foreach (var code in vcpCodes)
            {
                if (int.TryParse(code.Replace("0x", string.Empty), System.Globalization.NumberStyles.HexNumber, null, out int codeInt))
                {
                    vcpCodeInts.Add(codeInt);
                }
            }

            // Check for feature support based on VCP codes
            // 0x10 (16): Brightness
            // 0x12 (18): Contrast
            // 0x14 (20): Color Temperature (Select Color Preset)
            // 0x62 (98): Volume
            monitor.SupportsBrightness = vcpCodeInts.Contains(0x10);
            monitor.SupportsContrast = vcpCodeInts.Contains(0x12);
            monitor.SupportsColorTemperature = vcpCodeInts.Contains(0x14);
            monitor.SupportsVolume = vcpCodeInts.Contains(0x62);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "Base class PageViewModelBase.Dispose() handles GC.SuppressFinalize")]
        public override void Dispose()
        {
            // Unsubscribe from monitor property changes
            UnsubscribeFromItemPropertyChanged(_monitors);

            // Unsubscribe from collection changes
            if (_monitors != null)
            {
                _monitors.CollectionChanged -= Monitors_CollectionChanged;
            }

            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);

            base.Dispose();
        }

        /// <summary>
        /// Subscribe to PropertyChanged events for items in the collection
        /// </summary>
        private void SubscribeToItemPropertyChanged(IEnumerable<MonitorInfo> items)
        {
            if (items != null)
            {
                foreach (var item in items)
                {
                    item.PropertyChanged += OnMonitorPropertyChanged;
                }
            }
        }

        /// <summary>
        /// Unsubscribe from PropertyChanged events for items in the collection
        /// </summary>
        private void UnsubscribeFromItemPropertyChanged(IEnumerable<MonitorInfo> items)
        {
            if (items != null)
            {
                foreach (var item in items)
                {
                    item.PropertyChanged -= OnMonitorPropertyChanged;
                }
            }
        }

        /// <summary>
        /// Handle PropertyChanged events from MonitorInfo objects
        /// </summary>
        private void OnMonitorPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is MonitorInfo monitor)
            {
                System.Diagnostics.Debug.WriteLine($"[PowerDisplayViewModel] Monitor {monitor.Name} property {e.PropertyName} changed");
            }

            // Update the settings object to keep it in sync
            _settings.Properties.Monitors = _monitors.ToList();

            // Save settings when any monitor property changes
            NotifySettingsChanged();
        }

        public void Launch()
        {
            var actionMessage = new PowerDisplayActionMessage
            {
                Action = new PowerDisplayActionMessage.ActionData
                {
                    PowerDisplay = new PowerDisplayActionMessage.PowerDisplayAction
                    {
                        ActionName = "Launch",
                        Value = string.Empty,
                    },
                },
            };

            SendConfigMSG(JsonSerializer.Serialize(actionMessage));
        }

        /// <summary>
        /// Trigger PowerDisplay.exe to apply color temperature from settings file
        /// Called after user confirms color temperature change in Settings UI
        /// </summary>
        public void TriggerApplyColorTemperature()
        {
            var actionMessage = new PowerDisplayActionMessage
            {
                Action = new PowerDisplayActionMessage.ActionData
                {
                    PowerDisplay = new PowerDisplayActionMessage.PowerDisplayAction
                    {
                        ActionName = "ApplyColorTemperature",
                        Value = string.Empty,
                    },
                },
            };

            SendConfigMSG(JsonSerializer.Serialize(actionMessage));
        }

        /// <summary>
        /// Reload monitor list from settings file (called when PowerDisplay.exe signals monitor changes)
        /// </summary>
        private void ReloadMonitorsFromSettings()
        {
            try
            {
                Logger.LogInfo("Reloading monitors from settings file");

                // Read fresh settings from file
                var updatedSettings = SettingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
                var updatedMonitors = updatedSettings.Properties.Monitors;

                Logger.LogInfo($"[ReloadMonitors] Loaded {updatedMonitors.Count} monitors from settings");

                // Parse capabilities for each monitor
                foreach (var monitor in updatedMonitors)
                {
                    ParseFeatureSupportFromCapabilities(monitor);
                }

                // Update existing MonitorInfo objects instead of replacing the collection
                // This preserves XAML x:Bind bindings which reference specific object instances
                if (Monitors == null)
                {
                    // First time initialization - create new collection
                    Monitors = new ObservableCollection<MonitorInfo>(updatedMonitors);
                }
                else
                {
                    // Create a dictionary for quick lookup by InternalName
                    var updatedMonitorsDict = updatedMonitors.ToDictionary(m => m.InternalName, m => m);

                    // Update existing monitors or remove ones that no longer exist
                    for (int i = Monitors.Count - 1; i >= 0; i--)
                    {
                        var existingMonitor = Monitors[i];
                        if (updatedMonitorsDict.TryGetValue(existingMonitor.InternalName, out var updatedMonitor))
                        {
                            // Monitor still exists - update its properties in place
                            Logger.LogInfo($"[ReloadMonitors] Updating existing monitor: {existingMonitor.InternalName}");
                            existingMonitor.UpdateFrom(updatedMonitor);
                            updatedMonitorsDict.Remove(existingMonitor.InternalName);
                        }
                        else
                        {
                            // Monitor no longer exists - remove from collection
                            Logger.LogInfo($"[ReloadMonitors] Removing monitor: {existingMonitor.InternalName}");
                            Monitors.RemoveAt(i);
                        }
                    }

                    // Add any new monitors that weren't in the existing collection
                    foreach (var newMonitor in updatedMonitorsDict.Values)
                    {
                        Logger.LogInfo($"[ReloadMonitors] Adding new monitor: {newMonitor.InternalName}");
                        Monitors.Add(newMonitor);
                    }
                }

                // Update internal settings reference
                _settings.Properties.Monitors = updatedMonitors;

                Logger.LogInfo($"Successfully reloaded {updatedMonitors.Count} monitors");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to reload monitors from settings: {ex.Message}");
            }
        }

        private Func<string, int> SendConfigMSG { get; }

        private bool _isEnabled;
        private PowerDisplaySettings _settings;
        private ObservableCollection<MonitorInfo> _monitors;
        private bool _hasMonitors;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }

        private bool SetSettingsProperty<T>(T currentValue, T newValue, Action<T> setter, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(currentValue, newValue))
            {
                return false;
            }

            setter(newValue);
            OnPropertyChanged(propertyName);
            NotifySettingsChanged();
            return true;
        }

        private void NotifySettingsChanged()
        {
            // Persist locally first so settings survive even if the module DLL isn't loaded yet.
            SettingsUtils.SaveSettings(_settings.ToJsonString(), PowerDisplaySettings.ModuleName);

            // Using InvariantCulture as this is an IPC message
            // This message will be intercepted by the runner, which passes the serialized JSON to
            // PowerDisplay Module Interface's set_config() method, which then applies it in-process.
            SendConfigMSG(
                   string.Format(
                       CultureInfo.InvariantCulture,
                       "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                       PowerDisplaySettings.ModuleName,
                       JsonSerializer.Serialize(_settings, SourceGenerationContextContext.Default.PowerDisplaySettings)));
        }
    }
}
