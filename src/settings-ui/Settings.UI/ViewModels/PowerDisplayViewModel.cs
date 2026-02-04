// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;
using PowerDisplay.Common.Utils;
using PowerToys.Interop;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class PowerDisplayViewModel : PageViewModelBase
    {
        protected override string ModuleName => PowerDisplaySettings.ModuleName;

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private SettingsUtils SettingsUtils { get; set; }

        public ButtonClickCommand LaunchEventHandler => new ButtonClickCommand(Launch);

        public PowerDisplayViewModel(SettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<PowerDisplaySettings> powerDisplaySettingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            // Set up localized VCP code names for UI display
            VcpNames.LocalizedCodeNameProvider = GetLocalizedVcpCodeName;

            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            SettingsUtils = settingsUtils;
            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            _settings = powerDisplaySettingsRepository.SettingsConfig;

            InitializeEnabledValue();

            // Initialize monitors collection using property setter for proper subscription setup
            var loadedMonitors = _settings.Properties.Monitors;

            Logger.LogInfo($"[Constructor] Initializing with {loadedMonitors.Count} monitors from settings");

            Monitors = new ObservableCollection<MonitorInfo>(loadedMonitors);

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            // Subscribe to collection changes for HasProfiles binding
            _profiles.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasProfiles));

            // Load profiles
            LoadProfiles();

            // Load custom VCP mappings
            LoadCustomVcpMappings();

            // Listen for monitor refresh events from PowerDisplay.exe
            NativeEventWaiter.WaitForEventLoop(
                Constants.RefreshPowerDisplayMonitorsEvent(),
                () =>
                {
                    Logger.LogInfo("Received refresh monitors event from PowerDisplay.exe");
                    ReloadMonitorsFromSettings();
                });
        }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredPowerDisplayEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.PowerDisplay;
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_enabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

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

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        public bool RestoreSettingsOnStartup
        {
            get => _settings.Properties.RestoreSettingsOnStartup;
            set => SetSettingsProperty(_settings.Properties.RestoreSettingsOnStartup, value, v => _settings.Properties.RestoreSettingsOnStartup = v);
        }

        public bool ShowSystemTrayIcon
        {
            get => _settings.Properties.ShowSystemTrayIcon;
            set
            {
                if (SetSettingsProperty(_settings.Properties.ShowSystemTrayIcon, value, v => _settings.Properties.ShowSystemTrayIcon = v))
                {
                    // Explicitly signal PowerDisplay to refresh tray icon
                    // This is needed because set_config() doesn't signal SettingsUpdatedEvent to avoid UI refresh issues
                    SignalSettingsUpdated();
                    Logger.LogInfo($"ShowSystemTrayIcon changed to {value}");
                }
            }
        }

        public bool ShowProfileSwitcher
        {
            get => _settings.Properties.ShowProfileSwitcher;
            set
            {
                if (SetSettingsProperty(_settings.Properties.ShowProfileSwitcher, value, v => _settings.Properties.ShowProfileSwitcher = v))
                {
                    SignalSettingsUpdated();
                    Logger.LogInfo($"ShowProfileSwitcher changed to {value}");
                }
            }
        }

        public bool ShowIdentifyMonitorsButton
        {
            get => _settings.Properties.ShowIdentifyMonitorsButton;
            set
            {
                if (SetSettingsProperty(_settings.Properties.ShowIdentifyMonitorsButton, value, v => _settings.Properties.ShowIdentifyMonitorsButton = v))
                {
                    SignalSettingsUpdated();
                    Logger.LogInfo($"ShowIdentifyMonitorsButton changed to {value}");
                }
            }
        }

        public HotkeySettings ActivationShortcut
        {
            get => _settings.Properties.ActivationShortcut;
            set
            {
                if (SetSettingsProperty(_settings.Properties.ActivationShortcut, value, v => _settings.Properties.ActivationShortcut = v))
                {
                    // Signal PowerDisplay.exe to re-register the hotkey
                    EventHelper.SignalEvent(Constants.HotkeyUpdatedPowerDisplayEvent());
                    Logger.LogInfo($"ActivationShortcut changed, signaled HotkeyUpdatedPowerDisplayEvent");
                }
            }
        }

        public override Dictionary<string, HotkeySettings[]> GetAllHotkeySettings()
        {
            var hotkeysDict = new Dictionary<string, HotkeySettings[]>
            {
                [ModuleName] = [ActivationShortcut],
            };

            return hotkeysDict;
        }

        /// <summary>
        /// Gets or sets the delay in seconds before refreshing monitors after display changes.
        /// </summary>
        public int MonitorRefreshDelay
        {
            get => _settings.Properties.MonitorRefreshDelay;
            set => SetSettingsProperty(_settings.Properties.MonitorRefreshDelay, value, v => _settings.Properties.MonitorRefreshDelay = v);
        }

        private readonly List<int> _monitorRefreshDelayOptions = new List<int> { 1, 2, 3, 5, 10 };

        public List<int> MonitorRefreshDelayOptions => _monitorRefreshDelayOptions;

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

                // Update TotalMonitorCount for dynamic DisplayName
                UpdateTotalMonitorCount();
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

            // Update TotalMonitorCount for dynamic DisplayName
            UpdateTotalMonitorCount();
        }

        /// <summary>
        /// Update TotalMonitorCount on all monitors for dynamic DisplayName formatting.
        /// When multiple monitors exist, DisplayName shows "Name N" format.
        /// </summary>
        private void UpdateTotalMonitorCount()
        {
            if (_monitors == null)
            {
                return;
            }

            var count = _monitors.Count;
            foreach (var monitor in _monitors)
            {
                monitor.TotalMonitorCount = count;
            }
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
                Logger.LogDebug($"[PowerDisplayViewModel] Monitor {monitor.Name} property {e.PropertyName} changed");
            }

            // Update the settings object to keep it in sync
            _settings.Properties.Monitors = _monitors.ToList();

            // Save settings when any monitor property changes
            NotifySettingsChanged();

            // For feature visibility properties, explicitly signal PowerDisplay to refresh
            // This is needed because set_config() doesn't signal SettingsUpdatedEvent to avoid UI refresh issues
            if (e.PropertyName == nameof(MonitorInfo.EnableContrast) ||
                e.PropertyName == nameof(MonitorInfo.EnableVolume) ||
                e.PropertyName == nameof(MonitorInfo.EnableInputSource) ||
                e.PropertyName == nameof(MonitorInfo.EnableRotation) ||
                e.PropertyName == nameof(MonitorInfo.EnableColorTemperature) ||
                e.PropertyName == nameof(MonitorInfo.EnablePowerState) ||
                e.PropertyName == nameof(MonitorInfo.IsHidden))
            {
                SignalSettingsUpdated();
            }
        }

        /// <summary>
        /// Signal PowerDisplay.exe that settings have been updated and need to be applied
        /// </summary>
        private void SignalSettingsUpdated()
        {
            EventHelper.SignalEvent(Constants.SettingsUpdatedPowerDisplayEvent());
            Logger.LogInfo("Signaled SettingsUpdatedPowerDisplayEvent for feature visibility change");
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

            SendConfigMSG(JsonSerializer.Serialize(actionMessage, SettingsSerializationContext.Default.PowerDisplayActionMessage));
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

                // Update existing MonitorInfo objects instead of replacing the collection
                // This preserves XAML x:Bind bindings which reference specific object instances
                if (Monitors == null)
                {
                    // First time initialization - create new collection
                    Monitors = new ObservableCollection<MonitorInfo>(updatedMonitors);
                }
                else
                {
                    // Create a dictionary for quick lookup by Id
                    var updatedMonitorsDict = updatedMonitors.ToDictionary(m => m.Id, m => m);

                    // Update existing monitors or remove ones that no longer exist
                    for (int i = Monitors.Count - 1; i >= 0; i--)
                    {
                        var existingMonitor = Monitors[i];
                        if (updatedMonitorsDict.TryGetValue(existingMonitor.Id, out var updatedMonitor)
                            && updatedMonitor != null)
                        {
                            // Monitor still exists - update its properties in place
                            Logger.LogInfo($"[ReloadMonitors] Updating existing monitor: {existingMonitor.Id}");
                            existingMonitor.UpdateFrom(updatedMonitor);

                            updatedMonitorsDict.Remove(existingMonitor.Id);
                        }
                        else
                        {
                            // Monitor no longer exists - remove from collection
                            Logger.LogInfo($"[ReloadMonitors] Removing monitor: {existingMonitor.Id}");
                            Monitors.RemoveAt(i);
                        }
                    }

                    // Add any new monitors that weren't in the existing collection
                    foreach (var newMonitor in updatedMonitorsDict.Values)
                    {
                        Logger.LogInfo($"[ReloadMonitors] Adding new monitor: {newMonitor.Id}");
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

        // Profile-related fields
        private ObservableCollection<PowerDisplayProfile> _profiles = new ObservableCollection<PowerDisplayProfile>();

        // Custom VCP mapping fields
        private ObservableCollection<CustomVcpValueMapping> _customVcpMappings;

        /// <summary>
        /// Gets collection of custom VCP value name mappings
        /// </summary>
        public ObservableCollection<CustomVcpValueMapping> CustomVcpMappings => _customVcpMappings;

        /// <summary>
        /// Gets whether there are any custom VCP mappings (for UI binding)
        /// </summary>
        public bool HasCustomVcpMappings => _customVcpMappings?.Count > 0;

        /// <summary>
        /// Gets collection of available profiles (for button display)
        /// </summary>
        public ObservableCollection<PowerDisplayProfile> Profiles => _profiles;

        /// <summary>
        /// Gets whether there are any profiles (for UI binding)
        /// </summary>
        public bool HasProfiles => _profiles?.Count > 0;

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

        /// <summary>
        /// Load profiles from disk
        /// </summary>
        private void LoadProfiles()
        {
            try
            {
                var profilesData = ProfileService.LoadProfiles();

                // Load profile objects (no Custom - it's not a profile anymore)
                Profiles.Clear();
                foreach (var profile in profilesData.Profiles)
                {
                    Profiles.Add(profile);
                }

                Logger.LogInfo($"Loaded {Profiles.Count} profiles");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load profiles: {ex.Message}");
                Profiles.Clear();
            }
        }

        /// <summary>
        /// Apply a profile to monitors
        /// </summary>
        public void ApplyProfile(PowerDisplayProfile profile)
        {
            try
            {
                if (profile == null || !profile.IsValid())
                {
                    Logger.LogWarning("Invalid profile");
                    return;
                }

                Logger.LogInfo($"Applying profile: {profile.Name}");

                // Send custom action to trigger profile application
                // The profile name is passed via Named Pipe IPC to PowerDisplay.exe
                var actionMessage = new PowerDisplayActionMessage
                {
                    Action = new PowerDisplayActionMessage.ActionData
                    {
                        PowerDisplay = new PowerDisplayActionMessage.PowerDisplayAction
                        {
                            ActionName = "ApplyProfile",
                            Value = profile.Name,
                        },
                    },
                };

                SendConfigMSG(JsonSerializer.Serialize(actionMessage, SettingsSerializationContext.Default.PowerDisplayActionMessage));

                Logger.LogInfo($"Profile '{profile.Name}' applied successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to apply profile: {ex.Message}");
            }
        }

        /// <summary>
        /// Create a new profile
        /// </summary>
        public void CreateProfile(PowerDisplayProfile profile)
        {
            try
            {
                if (profile == null || !profile.IsValid())
                {
                    Logger.LogWarning("Invalid profile");
                    return;
                }

                Logger.LogInfo($"Creating profile: {profile.Name}");

                var profilesData = ProfileService.LoadProfiles();
                profilesData.SetProfile(profile);
                ProfileService.SaveProfiles(profilesData);

                // Reload profile list
                LoadProfiles();

                // Signal PowerDisplay to reload profiles
                SignalSettingsUpdated();

                Logger.LogInfo($"Profile '{profile.Name}' created successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create profile: {ex.Message}");
            }
        }

        /// <summary>
        /// Update an existing profile
        /// </summary>
        public void UpdateProfile(string oldName, PowerDisplayProfile newProfile)
        {
            try
            {
                if (newProfile == null || !newProfile.IsValid())
                {
                    Logger.LogWarning("Invalid profile");
                    return;
                }

                Logger.LogInfo($"Updating profile: {oldName} -> {newProfile.Name}");

                var profilesData = ProfileService.LoadProfiles();

                // Remove old profile and add updated one
                profilesData.RemoveProfile(oldName);
                profilesData.SetProfile(newProfile);
                ProfileService.SaveProfiles(profilesData);

                // Reload profile list
                LoadProfiles();

                // Signal PowerDisplay to reload profiles
                SignalSettingsUpdated();

                Logger.LogInfo($"Profile updated to '{newProfile.Name}' successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to update profile: {ex.Message}");
            }
        }

        /// <summary>
        /// Delete a profile
        /// </summary>
        public void DeleteProfile(string profileName)
        {
            try
            {
                if (string.IsNullOrEmpty(profileName))
                {
                    return;
                }

                Logger.LogInfo($"Deleting profile: {profileName}");

                var profilesData = ProfileService.LoadProfiles();
                profilesData.RemoveProfile(profileName);
                ProfileService.SaveProfiles(profilesData);

                // Reload profile list
                LoadProfiles();

                // Signal PowerDisplay to reload profiles
                SignalSettingsUpdated();

                Logger.LogInfo($"Profile '{profileName}' deleted successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to delete profile: {ex.Message}");
            }
        }

        /// <summary>
        /// Load custom VCP mappings from settings
        /// </summary>
        private void LoadCustomVcpMappings()
        {
            List<CustomVcpValueMapping> mappings;
            try
            {
                mappings = _settings.Properties.CustomVcpMappings ?? new List<CustomVcpValueMapping>();
                Logger.LogInfo($"Loaded {mappings.Count} custom VCP mappings");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load custom VCP mappings: {ex.Message}");
                mappings = new List<CustomVcpValueMapping>();
            }

            _customVcpMappings = new ObservableCollection<CustomVcpValueMapping>(mappings);
            _customVcpMappings.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasCustomVcpMappings));
            OnPropertyChanged(nameof(CustomVcpMappings));
            OnPropertyChanged(nameof(HasCustomVcpMappings));
        }

        /// <summary>
        /// Add a new custom VCP mapping.
        /// No duplicate checking - mappings are resolved by order (first match wins in VcpNames).
        /// </summary>
        public void AddCustomVcpMapping(CustomVcpValueMapping mapping)
        {
            if (mapping == null)
            {
                return;
            }

            CustomVcpMappings.Add(mapping);
            Logger.LogInfo($"Added custom VCP mapping: VCP=0x{mapping.VcpCode:X2}, Value=0x{mapping.Value:X2} -> {mapping.CustomName}");
            SaveCustomVcpMappings();
        }

        /// <summary>
        /// Update an existing custom VCP mapping
        /// </summary>
        public void UpdateCustomVcpMapping(CustomVcpValueMapping oldMapping, CustomVcpValueMapping newMapping)
        {
            if (oldMapping == null || newMapping == null)
            {
                return;
            }

            var index = CustomVcpMappings.IndexOf(oldMapping);
            if (index >= 0)
            {
                CustomVcpMappings[index] = newMapping;
                Logger.LogInfo($"Updated custom VCP mapping at index {index}");
                SaveCustomVcpMappings();
            }
        }

        /// <summary>
        /// Delete a custom VCP mapping
        /// </summary>
        public void DeleteCustomVcpMapping(CustomVcpValueMapping mapping)
        {
            if (mapping == null)
            {
                return;
            }

            if (CustomVcpMappings.Remove(mapping))
            {
                Logger.LogInfo($"Deleted custom VCP mapping: VCP=0x{mapping.VcpCode:X2}, Value=0x{mapping.Value:X2}");
                SaveCustomVcpMappings();
            }
        }

        /// <summary>
        /// Save custom VCP mappings to settings
        /// </summary>
        private void SaveCustomVcpMappings()
        {
            _settings.Properties.CustomVcpMappings = CustomVcpMappings.ToList();
            NotifySettingsChanged();

            // Signal PowerDisplay to reload settings
            SignalSettingsUpdated();
        }

        /// <summary>
        /// Provides localized VCP code names for UI display.
        /// Looks for resource string with pattern "PowerDisplay_VcpCode_Name_0xXX".
        /// Returns null for unknown codes to use the default MCCS name.
        /// </summary>
#nullable enable
        private static string? GetLocalizedVcpCodeName(byte vcpCode)
        {
            var resourceKey = $"PowerDisplay_VcpCode_Name_0x{vcpCode:X2}";
            var localizedName = ResourceLoaderInstance.ResourceLoader.GetString(resourceKey);

            // ResourceLoader returns empty string if key not found
            return string.IsNullOrEmpty(localizedName) ? null : localizedName;
        }
#nullable restore

        private void NotifySettingsChanged()
        {
            // Skip during initialization when SendConfigMSG is not yet set
            if (SendConfigMSG == null)
            {
                return;
            }

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
                       _settings.ToJsonString()));
        }
    }
}
