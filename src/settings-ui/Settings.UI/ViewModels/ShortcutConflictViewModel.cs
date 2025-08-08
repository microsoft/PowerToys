// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Threading;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Services;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class ShortcutConflictViewModel : PageViewModelBase, IDisposable
    {
        private readonly ViewModelFactory _viewModelFactory;
        private readonly Dictionary<string, PageViewModelBase> _moduleViewModels = new();
        private readonly Dispatcher _dispatcher;

        private AllHotkeyConflictsData _conflictsData = new();
        private ObservableCollection<HotkeyConflictGroupData> _conflictItems = new();

        public ShortcutConflictViewModel(
            ISettingsUtils settingsUtils,
            ISettingsRepository<GeneralSettings> settingsRepository,
            Func<string, int> ipcMSGCallBackFunc)
        {
            _dispatcher = Dispatcher.CurrentDispatcher;

            // Create ViewModelFactory with all necessary dependencies
            _viewModelFactory = new ViewModelFactory(
                settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils)),
                settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository)),
                ipcMSGCallBackFunc);
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

        public string GetAdvancedPasteCustomActionName(int actionId)
        {
            try
            {
                var advancedPasteViewModel = GetOrCreateViewModel(ModuleNames.AdvancedPaste) as AdvancedPasteViewModel;
                return advancedPasteViewModel?.CustomActions?.FirstOrDefault(ca => ca.Id == actionId)?.Name;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private PageViewModelBase GetOrCreateViewModel(string moduleKey)
        {
            if (!_moduleViewModels.TryGetValue(moduleKey, out var viewModel))
            {
                try
                {
                    viewModel = _viewModelFactory.CreateViewModel(moduleKey);
                    if (viewModel != null)
                    {
                        _moduleViewModels[moduleKey] = viewModel;
                        System.Diagnostics.Debug.WriteLine($"Created ViewModel for module: {moduleKey}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating ViewModel for {moduleKey}: {ex.Message}");
                }
            }

            return viewModel;
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
                ProcessConflictGroup(conflict, isSystemConflict);
                items.Add(conflict);
            }
        }

        private void ProcessConflictGroup(HotkeyConflictGroupData conflict, bool isSystemConflict)
        {
            foreach (var module in conflict.Modules)
            {
                SetupModuleData(module, isSystemConflict);
            }
        }

        private void SetupModuleData(ModuleHotkeyData module, bool isSystemConflict)
        {
            module.PropertyChanged += OnModuleHotkeyDataPropertyChanged;
            module.HotkeySettings = GetHotkeySettingsFromViewModel(module.ModuleName, module.HotkeyID);
            module.Header = LocalizationHelper.GetLocalizedHotkeyHeader(module.ModuleName, module.HotkeyID);
            module.IsSystemConflict = isSystemConflict;

            if (module.HotkeySettings != null)
            {
                SetConflictProperties(module.HotkeySettings, isSystemConflict);
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
                UpdateModuleViewModelHotkeySettings(moduleData.ModuleName, moduleData.HotkeyID, moduleData.HotkeySettings);
            }
        }

        private void UpdateModuleViewModelHotkeySettings(string moduleName, int hotkeyID, HotkeySettings newHotkeySettings)
        {
            try
            {
                var viewModel = GetOrCreateViewModel(GetModuleKey(moduleName));
                if (viewModel != null && HotkeyAccessorHelper.UpdateHotkeySettings(viewModel, moduleName, hotkeyID, newHotkeySettings))
                {
                    System.Diagnostics.Debug.WriteLine($"Updated {moduleName} hotkey {hotkeyID}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating hotkey settings for {moduleName}.{hotkeyID}: {ex.Message}");
            }
        }

        private HotkeySettings GetHotkeySettingsFromViewModel(string moduleName, int hotkeyID)
        {
            try
            {
                var viewModel = GetOrCreateViewModel(GetModuleKey(moduleName));
                return HotkeyAccessorHelper.GetHotkeySettings(viewModel, moduleName, hotkeyID);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting hotkey settings for {moduleName}.{hotkeyID}: {ex.Message}");
                return null;
            }
        }

        private static string GetModuleKey(string moduleName)
        {
            return moduleName?.ToLowerInvariant() switch
            {
                ModuleNames.MouseHighlighter or ModuleNames.MouseJump or
                ModuleNames.MousePointerCrosshairs or ModuleNames.FindMyMouse => ModuleNames.MouseUtils,
                _ => moduleName?.ToLowerInvariant(),
            };
        }

        public override void Dispose()
        {
            UnsubscribeFromEvents();
            DisposeViewModels();
            base.Dispose();
        }

        private void UnsubscribeFromEvents()
        {
            foreach (var conflictGroup in ConflictItems)
            {
                foreach (var module in conflictGroup.Modules)
                {
                    module.PropertyChanged -= OnModuleHotkeyDataPropertyChanged;
                }
            }
        }

        private void DisposeViewModels()
        {
            foreach (var viewModel in _moduleViewModels.Values)
            {
                viewModel?.Dispose();
            }

            _moduleViewModels.Clear();
        }
    }
}
