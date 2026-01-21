// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.Services;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// Base class for module-specific ViewModels that provides common functionality
    /// such as enabled state, GPO management, and hotkey conflict handling.
    /// </summary>
    public abstract partial class ModuleViewModelBase : ViewModelBase
    {
        private readonly Dictionary<string, bool> _hotkeyConflictStatus = new Dictionary<string, bool>();
        private readonly Dictionary<string, string> _hotkeyConflictTooltips = new Dictionary<string, string>();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEnabledGpoConfigured))]
        private bool _isEnabled;

        [ObservableProperty]
        private bool _isGpoManaged;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleViewModelBase"/> class.
        /// </summary>
        protected ModuleViewModelBase()
        {
            if (GlobalHotkeyConflictManager.Instance != null)
            {
                GlobalHotkeyConflictManager.Instance.ConflictsUpdated += OnConflictsUpdated;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleViewModelBase"/> class with a custom messenger.
        /// </summary>
        /// <param name="messenger">The messenger instance to use.</param>
        protected ModuleViewModelBase(IMessenger messenger)
            : base(messenger)
        {
            if (GlobalHotkeyConflictManager.Instance != null)
            {
                GlobalHotkeyConflictManager.Instance.ConflictsUpdated += OnConflictsUpdated;
            }
        }

        /// <summary>
        /// Gets the module name used for settings and conflict detection.
        /// </summary>
        protected abstract string ModuleName { get; }

        /// <summary>
        /// Gets a value indicating whether the enabled state is configured by GPO.
        /// </summary>
        public bool IsEnabledGpoConfigured => IsGpoManaged;

        /// <inheritdoc/>
        public override void OnPageLoaded()
        {
            base.OnPageLoaded();
            Debug.WriteLine($"=== PAGE LOADED: {ModuleName} ===");
            GlobalHotkeyConflictManager.Instance?.RequestAllConflicts();
        }

        /// <summary>
        /// Gets all hotkey settings for this module.
        /// Override in derived classes to return module-specific hotkey settings.
        /// </summary>
        /// <returns>A dictionary of module names to hotkey settings arrays.</returns>
        public virtual Dictionary<string, HotkeySettings[]> GetAllHotkeySettings()
        {
            return null;
        }

        /// <summary>
        /// Handles updates to hotkey conflicts for the module.
        /// </summary>
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

        /// <summary>
        /// Gets module-related conflicts from all conflicts data.
        /// </summary>
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

        /// <summary>
        /// Updates the hotkey conflict status based on all conflicts data.
        /// </summary>
        protected virtual void UpdateHotkeyConflictStatus(AllHotkeyConflictsData allConflicts)
        {
            _hotkeyConflictStatus.Clear();
            _hotkeyConflictTooltips.Clear();

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

        /// <summary>
        /// Gets whether a specific hotkey has a conflict.
        /// </summary>
        protected virtual bool GetHotkeyConflictStatus(string key)
        {
            return _hotkeyConflictStatus.ContainsKey(key) && _hotkeyConflictStatus[key];
        }

        /// <summary>
        /// Gets the conflict tooltip for a specific hotkey.
        /// </summary>
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

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (GlobalHotkeyConflictManager.Instance != null)
                {
                    GlobalHotkeyConflictManager.Instance.ConflictsUpdated -= OnConflictsUpdated;
                }
            }

            base.Dispose(disposing);
        }
    }
}
