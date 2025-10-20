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
            Monitors = new ObservableCollection<MonitorInfo>(_settings.Properties.Monitors);

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            // Subscribe to monitor information updates
            IPCResponseService.PowerDisplayMonitorsReceived += OnMonitorsReceived;
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
            set => SetSettingsProperty(_settings.Properties.LaunchAtStartup, value, v => _settings.Properties.LaunchAtStartup = v);
        }

        public bool RestoreSettingsOnStartup
        {
            get => _settings.Properties.RestoreSettingsOnStartup;
            set => SetSettingsProperty(_settings.Properties.RestoreSettingsOnStartup, value, v => _settings.Properties.RestoreSettingsOnStartup = v);
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
            "2s"
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

        private void OnMonitorsReceived(object sender, MonitorInfo[] monitors)
        {
            UpdateMonitors(monitors);
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

                // Check if we have an existing monitor with the same key
                if (existingMonitors.TryGetValue(monitorKey, out var existingMonitor))
                {
                    // Preserve user settings from existing monitor
                    newMonitor.EnableColorTemperature = existingMonitor.EnableColorTemperature;
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

        public void Dispose()
        {
            // Unsubscribe from monitor property changes
            UnsubscribeFromItemPropertyChanged(_monitors);

            // Unsubscribe from collection changes
            if (_monitors != null)
            {
                _monitors.CollectionChanged -= Monitors_CollectionChanged;
            }

            // Unsubscribe from events
            IPCResponseService.PowerDisplayMonitorsReceived -= OnMonitorsReceived;
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
                        Value = string.Empty
                    }
                }
            };

            SendConfigMSG(JsonSerializer.Serialize(actionMessage));
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
            // Using InvariantCulture as this is an IPC message
            SendConfigMSG(
                   string.Format(
                       CultureInfo.InvariantCulture,
                       "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                       PowerDisplaySettings.ModuleName,
                       JsonSerializer.Serialize(_settings, SourceGenerationContextContext.Default.PowerDisplaySettings)));

            // Save settings using the standard settings utility
            SettingsUtils.SaveSettings(_settings.ToJsonString(), PowerDisplaySettings.ModuleName);
        }
    }
}
