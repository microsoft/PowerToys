// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Windows.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class ShortcutConflictViewModel : PageViewModelBase
    {
        private readonly SettingsFactory _settingsFactory;
        private readonly Func<string, int> _ipcMSGCallBackFunc;
        private readonly Dispatcher _dispatcher;

        private bool _disposed;
        private AllHotkeyConflictsData _conflictsData = new();
        private ObservableCollection<HotkeyConflictGroupData> _conflictItems = new();
        private ResourceLoader resourceLoader;

        public ShortcutConflictViewModel(
            SettingsUtils settingsUtils,
            ISettingsRepository<GeneralSettings> settingsRepository,
            Func<string, int> ipcMSGCallBackFunc)
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _ipcMSGCallBackFunc = ipcMSGCallBackFunc ?? throw new ArgumentNullException(nameof(ipcMSGCallBackFunc));
            resourceLoader = ResourceLoaderInstance.ResourceLoader;

            // Create SettingsFactory
            _settingsFactory = new SettingsFactory(settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils)));
        }

        public AllHotkeyConflictsData ConflictsData
        {
            get => _conflictsData;
            set
            {
                if (Set(ref _conflictsData, value))
                {
                    UpdateConflictItems();
                }
            }
        }

        public ObservableCollection<HotkeyConflictGroupData> ConflictItems
        {
            get => _conflictItems;
            private set => Set(ref _conflictItems, value);
        }

        protected override string ModuleName => "ShortcutConflictsWindow";

        /// <summary>
        /// Ignore a specific HotkeySettings
        /// </summary>
        /// <param name="hotkeySettings">The HotkeySettings to ignore</param>
        public void IgnoreShortcut(HotkeySettings hotkeySettings)
        {
            if (hotkeySettings == null)
            {
                return;
            }

            HotkeyConflictIgnoreHelper.AddToIgnoredList(hotkeySettings);
            GlobalHotkeyConflictManager.Instance?.RequestAllConflicts();
        }

        /// <summary>
        /// Remove a HotkeySettings from the ignored list
        /// </summary>
        /// <param name="hotkeySettings">The HotkeySettings to unignore</param>
        public void UnignoreShortcut(HotkeySettings hotkeySettings)
        {
            if (hotkeySettings == null)
            {
                return;
            }

            HotkeyConflictIgnoreHelper.RemoveFromIgnoredList(hotkeySettings);
            GlobalHotkeyConflictManager.Instance?.RequestAllConflicts();
        }

        private IHotkeyConfig GetModuleSettings(string moduleKey)
        {
            try
            {
                // MouseWithoutBorders and Peek settings may be changed by the logic in the utility as machines connect.
                // We need to get a fresh version every time instead of using a repository.
                if (string.Equals(moduleKey, MouseWithoutBordersSettings.ModuleName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(moduleKey, PeekSettings.ModuleName, StringComparison.OrdinalIgnoreCase))
                {
                    return _settingsFactory.GetFreshSettings(moduleKey);
                }

                // For other modules, get the settings from SettingsRepository
                return _settingsFactory.GetSettings(moduleKey);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings for {moduleKey}: {ex.Message}");
                return null;
            }
        }

        protected override void OnConflictsUpdated(object sender, AllHotkeyConflictsEventArgs e)
        {
            _dispatcher.BeginInvoke(() =>
            {
                ConflictsData = e.Conflicts ?? new AllHotkeyConflictsData();
            });
        }

        private void UpdateConflictItems()
        {
            var items = new ObservableCollection<HotkeyConflictGroupData>();

            ProcessConflicts(ConflictsData?.InAppConflicts, false, items);
            ProcessConflicts(ConflictsData?.SystemConflicts, true, items);

            ConflictItems = items;
            OnPropertyChanged(nameof(ConflictItems));
        }

        private void ProcessConflicts(IEnumerable<HotkeyConflictGroupData> conflicts, bool isSystemConflict, ObservableCollection<HotkeyConflictGroupData> items)
        {
            if (conflicts == null)
            {
                return;
            }

            foreach (var conflict in conflicts)
            {
                HotkeySettings hotkey = new(conflict.Hotkey.Win, conflict.Hotkey.Ctrl, conflict.Hotkey.Alt, conflict.Hotkey.Shift, conflict.Hotkey.Key);
                var isIgnored = HotkeyConflictIgnoreHelper.IsIgnoringConflicts(hotkey);
                conflict.ConflictIgnored = isIgnored;

                ProcessConflictGroup(conflict, isSystemConflict, isIgnored);
                items.Add(conflict);
            }
        }

        private void ProcessConflictGroup(HotkeyConflictGroupData conflict, bool isSystemConflict, bool isIgnored)
        {
            foreach (var module in conflict.Modules)
            {
                SetupModuleData(module, isSystemConflict, isIgnored);
            }
        }

        private void SetupModuleData(ModuleHotkeyData module, bool isSystemConflict, bool isIgnored)
        {
            try
            {
                var settings = GetModuleSettings(module.ModuleName);
                var allHotkeyAccessors = settings.GetAllHotkeyAccessors();
                var hotkeyAccessor = allHotkeyAccessors[module.HotkeyID];

                if (hotkeyAccessor != null)
                {
                    // Get current hotkey settings (fresh from file) using the accessor's getter
                    module.HotkeySettings = hotkeyAccessor.Value;
                    module.HotkeySettings.ConflictDescription = isSystemConflict
                        ? ResourceLoaderInstance.ResourceLoader.GetString("SysHotkeyConflictTooltipText")
                        : ResourceLoaderInstance.ResourceLoader.GetString("InAppHotkeyConflictTooltipText");

                    // Set header using localization key
                    module.Header = GetHotkeyLocalizationHeader(module.ModuleName, module.HotkeyID, hotkeyAccessor.LocalizationHeaderKey);
                    module.IsSystemConflict = isSystemConflict;

                    // Set module display info
                    var moduleType = settings.GetModuleType();
                    module.ModuleType = moduleType;
                    var displayName = resourceLoader.GetString(ModuleHelper.GetModuleLabelResourceName(moduleType));
                    module.DisplayName = displayName;
                    module.IconPath = ModuleHelper.GetModuleTypeFluentIconName(moduleType);

                    if (module.HotkeySettings != null)
                    {
                        SetConflictProperties(module.HotkeySettings, isSystemConflict);
                    }

                    module.PropertyChanged -= OnModuleHotkeyDataPropertyChanged;
                    module.PropertyChanged += OnModuleHotkeyDataPropertyChanged;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Could not find hotkey accessor for {module.ModuleName}.{module.HotkeyID}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting up module data for {module.ModuleName}: {ex.Message}");
            }
        }

        private void SetConflictProperties(HotkeySettings settings, bool isSystemConflict)
        {
            settings.HasConflict = true;
            settings.IsSystemConflict = isSystemConflict;
        }

        private void OnModuleHotkeyDataPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is ModuleHotkeyData moduleData && e.PropertyName == nameof(ModuleHotkeyData.HotkeySettings))
            {
                UpdateModuleHotkeySettings(moduleData.ModuleName, moduleData.HotkeyID, moduleData.HotkeySettings);
            }
        }

        private void UpdateModuleHotkeySettings(string moduleName, int hotkeyID, HotkeySettings newHotkeySettings)
        {
            try
            {
                var settings = GetModuleSettings(moduleName);
                var accessors = settings.GetAllHotkeyAccessors();

                var hotkeyAccessor = accessors[hotkeyID];

                // Use the accessor's setter to update the hotkey settings
                hotkeyAccessor.Value = newHotkeySettings;

                if (settings is ISettingsConfig settingsConfig)
                {
                    // No need to save settings here, the runner will call module interface to save it
                    // SaveSettingsToFile(settings);

                    // Send IPC notification using the same format as other ViewModels
                    SendConfigMSG(settingsConfig, moduleName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating hotkey settings for {moduleName}.{hotkeyID}: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends IPC notification using the same format as other ViewModels
        /// </summary>
        private void SendConfigMSG(ISettingsConfig settingsConfig, string moduleName)
        {
            try
            {
                var jsonTypeInfo = GetJsonTypeInfo(settingsConfig.GetType());
                var serializedSettings = jsonTypeInfo != null
                    ? JsonSerializer.Serialize(settingsConfig, jsonTypeInfo)
                    : JsonSerializer.Serialize(settingsConfig);

                var ipcMessage = string.Format(
                    CultureInfo.InvariantCulture,
                    "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                    moduleName,
                    serializedSettings);

                var result = _ipcMSGCallBackFunc(ipcMessage);
                System.Diagnostics.Debug.WriteLine($"Sent IPC notification for {moduleName}, result: {result}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending IPC notification for {moduleName}: {ex.Message}");
            }
        }

        private JsonTypeInfo GetJsonTypeInfo(Type settingsType)
        {
            try
            {
                var contextType = typeof(SourceGenerationContextContext);
                var defaultProperty = contextType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static);
                var defaultContext = defaultProperty?.GetValue(null) as JsonSerializerContext;

                if (defaultContext != null)
                {
                    var typeInfoProperty = contextType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(p => p.PropertyType.IsGenericType &&
                                           p.PropertyType.GetGenericTypeDefinition() == typeof(JsonTypeInfo<>) &&
                                           p.PropertyType.GetGenericArguments()[0] == settingsType);

                    return typeInfoProperty?.GetValue(defaultContext) as JsonTypeInfo;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting JsonTypeInfo for {settingsType.Name}: {ex.Message}");
            }

            return null;
        }

        private string GetHotkeyLocalizationHeader(string moduleName, int hotkeyID, string headerKey)
        {
            // Handle AdvancedPaste custom actions
            if (string.Equals(moduleName, AdvancedPasteSettings.ModuleName, StringComparison.OrdinalIgnoreCase)
                && hotkeyID > 9)
            {
                return headerKey;
            }

            try
            {
                return resourceLoader.GetString($"{headerKey}/Header");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting hotkey header for {moduleName}.{hotkeyID}: {ex.Message}");
                return headerKey; // Return the key itself as fallback
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    UnsubscribeFromEvents();
                }

                _disposed = true;
            }

            base.Dispose(disposing);
        }

        private void UnsubscribeFromEvents()
        {
            try
            {
                if (ConflictItems != null)
                {
                    foreach (var conflictGroup in ConflictItems)
                    {
                        if (conflictGroup?.Modules != null)
                        {
                            foreach (var module in conflictGroup.Modules)
                            {
                                if (module != null)
                                {
                                    module.PropertyChanged -= OnModuleHotkeyDataPropertyChanged;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error unsubscribing from events: {ex.Message}");
            }
        }
    }
}
