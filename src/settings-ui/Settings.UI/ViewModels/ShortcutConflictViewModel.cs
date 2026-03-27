// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

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
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class ShortcutConflictViewModel : PageViewModelBase
    {
        private readonly SettingsFactory _settingsFactory;
        private readonly Func<string, int> _ipcMSGCallBackFunc;
        private readonly Microsoft.UI.Dispatching.DispatcherQueue? _dispatcherQueue;

        private bool _disposed;
        private AllHotkeyConflictsData _conflictsData = new();
        private ObservableCollection<HotkeyConflictGroupData> _conflictItems = new();
        private ResourceLoader resourceLoader;

        public ShortcutConflictViewModel(
            SettingsUtils settingsUtils,
            ISettingsRepository<GeneralSettings> settingsRepository,
            Func<string, int> ipcMSGCallBackFunc)
        {
            // Use WinUI 3 DispatcherQueue instead of WPF Dispatcher for AOT compatibility
            _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
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
            get => _conflictItems ?? new ObservableCollection<HotkeyConflictGroupData>();
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
                return null!;  // Suppress nullable warning - caller handles null
            }
        }

        protected override void OnConflictsUpdated(object sender, AllHotkeyConflictsEventArgs e)
        {
            // WinUI 3 DispatcherQueue uses TryEnqueue instead of BeginInvoke
            _dispatcherQueue?.TryEnqueue(() =>
            {
                ConflictsData = e.Conflicts ?? new AllHotkeyConflictsData();
            });
        }

        private void UpdateConflictItems()
        {
            var items = new ObservableCollection<HotkeyConflictGroupData>();

            ProcessConflicts(ConflictsData?.InAppConflicts ?? Enumerable.Empty<HotkeyConflictGroupData>(), false, items);
            ProcessConflicts(ConflictsData?.SystemConflicts ?? Enumerable.Empty<HotkeyConflictGroupData>(), true, items);

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

        private void OnModuleHotkeyDataPropertyChanged(object? sender, PropertyChangedEventArgs e)
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

                    // For PowerToys Run, we should set the 'HotkeyChanged' property here to avoid issue #41468
                    if (string.Equals(moduleName, PowerLauncherSettings.ModuleName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (settings is PowerLauncherSettings powerLauncherSettings)
                        {
                            powerLauncherSettings.Properties.HotkeyChanged = true;
                        }
                    }

                    // Send IPC notification using the same format as other ViewModels
                    SendConfigMSG(settingsConfig, moduleName);

                    // Request updated conflicts after changing a hotkey
                    GlobalHotkeyConflictManager.Instance?.RequestAllConflicts();
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
                // Use source-generated serializer for AOT compatibility (IL2026/IL3050)
                var serializedSettings = SerializeSettings(settingsConfig);

                string ipcMessage;
                if (string.Equals(moduleName, "GeneralSettings", StringComparison.OrdinalIgnoreCase))
                {
                    ipcMessage = string.Format(
                        CultureInfo.InvariantCulture,
                        "{{ \"general\": {0} }}",
                        serializedSettings);
                }
                else
                {
                    ipcMessage = string.Format(
                        CultureInfo.InvariantCulture,
                        "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                        moduleName,
                        serializedSettings);
                }

                var result = _ipcMSGCallBackFunc(ipcMessage);
                System.Diagnostics.Debug.WriteLine($"Sent IPC notification for {moduleName}, result: {result}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending IPC notification for {moduleName}: {ex.Message}");
            }
        }

        // AOT-compatible serialization using source-generated context
        private string SerializeSettings(ISettingsConfig settingsConfig)
        {
            return settingsConfig switch
            {
                GeneralSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.GeneralSettings),
                AdvancedPasteSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.AdvancedPasteSettings),
                AlwaysOnTopSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.AlwaysOnTopSettings),
                AwakeSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.AwakeSettings),
                CmdNotFoundSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.CmdNotFoundSettings),
                ColorPickerSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.ColorPickerSettings),
                CropAndLockSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.CropAndLockSettings),
                EnvironmentVariablesSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.EnvironmentVariablesSettings),
                FancyZonesSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.FancyZonesSettings),
                FileLocksmithSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.FileLocksmithSettings),
                HostsSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.HostsSettings),
                ImageResizerSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.ImageResizerSettings),
                KeyboardManagerSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.KeyboardManagerSettings),
                LightSwitchSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.LightSwitchSettings),
                MeasureToolSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.MeasureToolSettings),
                MouseWithoutBordersSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.MouseWithoutBordersSettings),
                NewPlusSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.NewPlusSettings),
                PeekSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.PeekSettings),
                PowerAccentSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.PowerAccentSettings),
                PowerDisplaySettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.PowerDisplaySettings),
                PowerLauncherSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.PowerLauncherSettings),
                PowerOcrSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.PowerOcrSettings),
                PowerPreviewSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.PowerPreviewSettings),
                PowerRenameSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.PowerRenameSettings),
                RegistryPreviewSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.RegistryPreviewSettings),
                ShortcutGuideSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.ShortcutGuideSettings),
                WorkspacesSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.WorkspacesSettings),
                ZoomItSettings s => JsonSerializer.Serialize(s, SettingsSerializationContext.Default.ZoomItSettings),

                // If we hit this case, SettingsSerializationContext is incomplete - this should be caught in development
                _ => throw new InvalidOperationException($"Settings type {settingsConfig.GetType().Name} is not registered in SettingsSerializationContext. Please add [JsonSerializable(typeof({settingsConfig.GetType().Name}))] to the context."),
            };
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
