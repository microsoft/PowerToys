// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.Services;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public abstract class PageViewModelBase : Observable, IDisposable
    {
        private readonly Dictionary<string, bool> _hotkeyConflictStatus = new Dictionary<string, bool>();
        private readonly Dictionary<string, string> _hotkeyConflictTooltips = new Dictionary<string, string>();
        private bool _disposed;
        private GeneralSettings _lowMemoryGeneralSettings;
        private Func<string, int> _lowMemorySettingsCallback;
        private ModuleType? _lowMemoryModuleType;

        protected abstract string ModuleName { get; }

        protected PageViewModelBase()
        {
            if (GlobalHotkeyConflictManager.Instance != null)
            {
                GlobalHotkeyConflictManager.Instance.ConflictsUpdated += OnConflictsUpdated;
            }
        }

        public bool UseLowMemoryMode
        {
            get => _lowMemoryGeneralSettings != null && _lowMemoryModuleType.HasValue && ModuleHelper.GetLowMemoryMode(_lowMemoryGeneralSettings, _lowMemoryModuleType.Value);
            set
            {
                if (_lowMemoryGeneralSettings == null || !_lowMemoryModuleType.HasValue)
                {
                    return;
                }

                if (ModuleHelper.SetLowMemoryMode(_lowMemoryGeneralSettings, _lowMemoryModuleType.Value, value))
                {
                    _lowMemoryGeneralSettings.LowMemoryMode = ModuleHelper.AnyLowMemoryModeEnabled(_lowMemoryGeneralSettings);
                    OnPropertyChanged(nameof(UseLowMemoryMode));
                    _lowMemorySettingsCallback?.Invoke(new OutGoingGeneralSettings(_lowMemoryGeneralSettings).ToString());
                }
            }
        }

        protected void InitializeLowMemorySettings(GeneralSettings generalSettings, Func<string, int> sendConfigMessage, ModuleType moduleType)
        {
            _lowMemoryGeneralSettings = generalSettings;
            _lowMemorySettingsCallback = sendConfigMessage;
            _lowMemoryModuleType = moduleType;
            ModuleHelper.EnsureLowMemoryModuleSettings(_lowMemoryGeneralSettings);
        }

        public virtual void OnPageLoaded()
        {
            Debug.WriteLine($"=== PAGE LOADED: {ModuleName} ===");
            OnPropertyChanged(nameof(UseLowMemoryMode));
            GlobalHotkeyConflictManager.Instance?.RequestAllConflicts();
        }

        /// <summary>
        /// Handles updates to hotkey conflicts for the module. This method is called when the
        /// <see cref="GlobalHotkeyConflictManager"/> raises the <c>ConflictsUpdated</c> event.
        /// </summary>
        /// <param name="sender">The source of the event, typically the <see cref="GlobalHotkeyConflictManager"/> instance.</param>
        /// <param name="e">An <see cref="AllHotkeyConflictsEventArgs"/> object containing details about the hotkey conflicts.</param>
        /// <remarks>
        /// Derived classes can override this method to provide custom handling for hotkey conflicts.
        /// Ensure that the overridden method maintains the expected behavior of processing and logging conflict data.
        /// </remarks>
        protected virtual void OnConflictsUpdated(object sender, AllHotkeyConflictsEventArgs e)
        {
            UpdateHotkeyConflictStatus(e.Conflicts);
            var allHotkeySettings = GetAllHotkeySettings();

            void UpdateConflictProperties()
            {
                if (allHotkeySettings != null)
                {
                    foreach (KeyValuePair<string, HotkeySettings[]> kvp in allHotkeySettings)
                    {
                        var module = kvp.Key;
                        var hotkeySettingsList = kvp.Value;

                        for (int i = 0; i < hotkeySettingsList.Length; i++)
                        {
                            var key = $"{module.ToLowerInvariant()}_{i}";
                            hotkeySettingsList[i].HasConflict = GetHotkeyConflictStatus(key);
                            hotkeySettingsList[i].ConflictDescription = GetHotkeyConflictTooltip(key);
                        }
                    }
                }
            }

            _ = Task.Run(() =>
            {
                try
                {
                    var settingsWindow = App.GetSettingsWindow();
                    settingsWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, UpdateConflictProperties);
                }
                catch
                {
                    UpdateConflictProperties();
                }
            });
        }

        public virtual Dictionary<string, HotkeySettings[]> GetAllHotkeySettings()
        {
            return null;
        }

        protected ModuleConflictsData GetModuleRelatedConflicts(AllHotkeyConflictsData allConflicts)
        {
            var moduleConflicts = new ModuleConflictsData();

            if (allConflicts.InAppConflicts != null)
            {
                foreach (var conflict in allConflicts.InAppConflicts)
                {
                    if (IsModuleInvolved(conflict))
                    {
                        moduleConflicts.InAppConflicts.Add(conflict);
                    }
                }
            }

            if (allConflicts.SystemConflicts != null)
            {
                foreach (var conflict in allConflicts.SystemConflicts)
                {
                    if (IsModuleInvolved(conflict))
                    {
                        moduleConflicts.SystemConflicts.Add(conflict);
                    }
                }
            }

            return moduleConflicts;
        }

        private void ProcessMouseUtilsConflictGroup(HotkeyConflictGroupData conflict, HashSet<string> mouseUtilsModules, bool isSysConflict)
        {
            // Check if any of the modules in this conflict are MouseUtils submodules
            var involvedMouseUtilsModules = conflict.Modules
                .Where(module => mouseUtilsModules.Contains(module.ModuleName))
                .ToList();

            if (involvedMouseUtilsModules.Count != 0)
            {
                // For each involved MouseUtils module, mark the hotkey as having a conflict
                foreach (var module in involvedMouseUtilsModules)
                {
                    string hotkeyKey = $"{module.ModuleName.ToLowerInvariant()}_{module.HotkeyID}";
                    _hotkeyConflictStatus[hotkeyKey] = true;
                    _hotkeyConflictTooltips[hotkeyKey] = isSysConflict
                        ? ResourceLoaderInstance.ResourceLoader.GetString("SysHotkeyConflictTooltipText")
                        : ResourceLoaderInstance.ResourceLoader.GetString("InAppHotkeyConflictTooltipText");
                }
            }
        }

        protected virtual void UpdateHotkeyConflictStatus(AllHotkeyConflictsData allConflicts)
        {
            _hotkeyConflictStatus.Clear();
            _hotkeyConflictTooltips.Clear();

            // Since MouseUtils in Settings consolidates four modules: Find My Mouse, Mouse Highlighter, Mouse Pointer Crosshairs, and Mouse Jump
            // We need to handle this case separately here.
            if (string.Equals(ModuleName, "MouseUtils", StringComparison.OrdinalIgnoreCase))
            {
                var mouseUtilsModules = new HashSet<string>
                {
                    FindMyMouseSettings.ModuleName,
                    MouseHighlighterSettings.ModuleName,
                    MousePointerCrosshairsSettings.ModuleName,
                    MouseJumpSettings.ModuleName,
                };

                // Process in-app conflicts
                foreach (var conflict in allConflicts.InAppConflicts)
                {
                    ProcessMouseUtilsConflictGroup(conflict, mouseUtilsModules, false);
                }

                // Process system conflicts
                foreach (var conflict in allConflicts.SystemConflicts)
                {
                    ProcessMouseUtilsConflictGroup(conflict, mouseUtilsModules, true);
                }
            }
            else
            {
                if (allConflicts.InAppConflicts.Count > 0)
                {
                    foreach (var conflictGroup in allConflicts.InAppConflicts)
                    {
                        foreach (var conflict in conflictGroup.Modules)
                        {
                            if (string.Equals(conflict.ModuleName, ModuleName, StringComparison.OrdinalIgnoreCase))
                            {
                                var keyName = $"{conflict.ModuleName.ToLowerInvariant()}_{conflict.HotkeyID}";
                                _hotkeyConflictStatus[keyName] = true;
                                _hotkeyConflictTooltips[keyName] = ResourceLoaderInstance.ResourceLoader.GetString("InAppHotkeyConflictTooltipText");
                            }
                        }
                    }
                }

                if (allConflicts.SystemConflicts.Count > 0)
                {
                    foreach (var conflictGroup in allConflicts.SystemConflicts)
                    {
                        foreach (var conflict in conflictGroup.Modules)
                        {
                            if (string.Equals(conflict.ModuleName, ModuleName, StringComparison.OrdinalIgnoreCase))
                            {
                                var keyName = $"{conflict.ModuleName.ToLowerInvariant()}_{conflict.HotkeyID}";
                                _hotkeyConflictStatus[keyName] = true;
                                _hotkeyConflictTooltips[keyName] = ResourceLoaderInstance.ResourceLoader.GetString("SysHotkeyConflictTooltipText");
                            }
                        }
                    }
                }
            }
        }

        protected virtual bool GetHotkeyConflictStatus(string key)
        {
            return _hotkeyConflictStatus.ContainsKey(key) && _hotkeyConflictStatus[key];
        }

        protected virtual string GetHotkeyConflictTooltip(string key)
        {
            return _hotkeyConflictTooltips.TryGetValue(key, out string value) ? value : null;
        }

        private bool IsModuleInvolved(HotkeyConflictGroupData conflict)
        {
            if (conflict.Modules == null)
            {
                return false;
            }

            return conflict.Modules.Any(module =>
                string.Equals(module.ModuleName, ModuleName, StringComparison.OrdinalIgnoreCase));
        }

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (GlobalHotkeyConflictManager.Instance != null)
                    {
                        GlobalHotkeyConflictManager.Instance.ConflictsUpdated -= OnConflictsUpdated;
                    }
                }

                _disposed = true;
            }
        }
    }
}
