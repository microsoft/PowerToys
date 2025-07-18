// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.Services;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public abstract class PageViewModelBase : Observable
    {
        private readonly Dictionary<string, bool> _hotkeyConflictStatus = new Dictionary<string, bool>();
        private readonly Dictionary<string, string> _hotkeyConflictTooltips = new Dictionary<string, string>();

        protected abstract string ModuleName { get; }

        protected PageViewModelBase()
        {
            if (GlobalHotkeyConflictManager.Instance != null)
            {
                GlobalHotkeyConflictManager.Instance.ConflictsUpdated += OnConflictsUpdated;
            }
        }

        public virtual void OnPageLoaded()
        {
            Debug.WriteLine($"=== PAGE LOADED: {ModuleName} ===");
            GlobalHotkeyConflictManager.Instance?.RequestAllConflicts();
        }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

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
            try
            {
                Debug.WriteLine($"=== {ModuleName}: HOTKEY CONFLICTS DATA RECEIVED ===");

                var moduleRelatedConflicts = GetModuleRelatedConflicts(e.Conflicts);

                if (moduleRelatedConflicts.HasConflicts)
                {
                    var filteredData = JsonSerializer.Serialize(moduleRelatedConflicts, JsonOptions);
                    Debug.WriteLine($"{ModuleName} - Filtered JSON Data:\n{filteredData}");

                    Debug.WriteLine($"{ModuleName} - Module Related InApp Conflicts: {moduleRelatedConflicts.InAppConflicts.Count}");
                    Debug.WriteLine($"{ModuleName} - Module Related System Conflicts: {moduleRelatedConflicts.SystemConflicts.Count}");

                    PrintModuleConflictDetails(moduleRelatedConflicts);
                }
                else
                {
                    Debug.WriteLine($"{ModuleName} - No conflicts found for this module.");
                }

                Debug.WriteLine($"=== {ModuleName}: END HOTKEY CONFLICTS DATA ===\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{ModuleName} - Error printing IPC hotkey conflicts data: {ex.Message}");
                Debug.WriteLine($"{ModuleName} - Exception details: {ex}");
            }
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

        protected virtual void UpdateHotkeyConflictStatus(AllHotkeyConflictsData allConflicts)
        {
            _hotkeyConflictStatus.Clear();
            _hotkeyConflictTooltips.Clear();

            var moduleRelatedConflicts = GetModuleRelatedConflicts(allConflicts);

            if (moduleRelatedConflicts.InAppConflicts.Count > 0)
            {
                foreach (var conflictGroup in moduleRelatedConflicts.InAppConflicts)
                {
                    foreach (var conflict in conflictGroup.Modules)
                    {
                        _hotkeyConflictStatus[conflict.HotkeyName] = true;
                        _hotkeyConflictTooltips[conflict.HotkeyName] = ResourceLoaderInstance.ResourceLoader.GetString("InAppHotkeyConflictTooltipText");
                    }
                }
            }

            if (moduleRelatedConflicts.SystemConflicts.Count > 0)
            {
                foreach (var conflictGroup in moduleRelatedConflicts.SystemConflicts)
                {
                    foreach (var conflict in conflictGroup.Modules)
                    {
                        _hotkeyConflictStatus[conflict.HotkeyName] = true;
                        _hotkeyConflictTooltips[conflict.HotkeyName] = ResourceLoaderInstance.ResourceLoader.GetString("SysHotkeyConflictTooltipText");
                    }
                }
            }
        }

        protected virtual bool GetHotkeyConflictStatus(string hotkeyName)
        {
            return _hotkeyConflictStatus.ContainsKey(hotkeyName) && _hotkeyConflictStatus[hotkeyName];
        }

        protected virtual string GetHotkeyConflictTooltip(string hotkeyName)
        {
            return _hotkeyConflictTooltips.TryGetValue(hotkeyName, out string value) ? value : null;
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

        private void PrintModuleConflictDetails(ModuleConflictsData conflicts)
        {
            if (conflicts.InAppConflicts.Count > 0)
            {
                Debug.WriteLine($"\n{ModuleName} - InApp Conflicts:");
                for (int i = 0; i < conflicts.InAppConflicts.Count; i++)
                {
                    var conflict = conflicts.InAppConflicts[i];
                    Debug.WriteLine($"  Conflict #{i + 1}:");
                    Debug.WriteLine($"    Hotkey: Win={conflict.Hotkey?.Win}, Ctrl={conflict.Hotkey?.Ctrl}, Alt={conflict.Hotkey?.Alt}, Shift={conflict.Hotkey?.Shift}, Key={conflict.Hotkey?.Key}");

                    if (conflict.Modules != null)
                    {
                        Debug.WriteLine($"    Involved Modules ({conflict.Modules.Count}):");
                        foreach (var module in conflict.Modules)
                        {
                            var isCurrentModule = string.Equals(module.ModuleName, ModuleName, StringComparison.OrdinalIgnoreCase);
                            var marker = isCurrentModule ? " [THIS MODULE]" : string.Empty;
                            Debug.WriteLine($"      - {module.ModuleName}:{module.HotkeyName}{marker}");
                        }
                    }
                }
            }

            if (conflicts.SystemConflicts.Count > 0)
            {
                Debug.WriteLine($"\n{ModuleName} - System Conflicts:");
                for (int i = 0; i < conflicts.SystemConflicts.Count; i++)
                {
                    var conflict = conflicts.SystemConflicts[i];
                    Debug.WriteLine($"  Conflict #{i + 1}:");
                    Debug.WriteLine($"    Hotkey: Win={conflict.Hotkey?.Win}, Ctrl={conflict.Hotkey?.Ctrl}, Alt={conflict.Hotkey?.Alt}, Shift={conflict.Hotkey?.Shift}, Key={conflict.Hotkey?.Key}");

                    if (conflict.Modules != null)
                    {
                        Debug.WriteLine($"    Involved Modules ({conflict.Modules.Count}):");
                        foreach (var module in conflict.Modules)
                        {
                            var isCurrentModule = string.Equals(module.ModuleName, ModuleName, StringComparison.OrdinalIgnoreCase);
                            var marker = isCurrentModule ? " [THIS MODULE]" : string.Empty;
                            Debug.WriteLine($"      - {module.ModuleName}:{module.HotkeyName}{marker}");
                        }
                    }
                }
            }
        }

        public virtual void Dispose()
        {
            if (GlobalHotkeyConflictManager.Instance != null)
            {
                GlobalHotkeyConflictManager.Instance.ConflictsUpdated -= OnConflictsUpdated;
            }
        }
    }
}
