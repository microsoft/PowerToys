// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public partial class QuickAccessViewModel : Observable
    {
        private readonly ISettingsRepository<GeneralSettings> _settingsRepository;
        private readonly IQuickAccessLauncher _launcher;
        private readonly Func<ModuleType, bool> _isModuleGpoDisabled;
        private readonly ResourceLoader _resourceLoader;
        private readonly DispatcherQueue _dispatcherQueue;
        private GeneralSettings _generalSettings;

        public ObservableCollection<QuickAccessItem> Items { get; } = new();

        public QuickAccessViewModel(
            ISettingsRepository<GeneralSettings> settingsRepository,
            IQuickAccessLauncher launcher,
            Func<ModuleType, bool> isModuleGpoDisabled,
            ResourceLoader resourceLoader)
        {
            _settingsRepository = settingsRepository;
            _launcher = launcher;
            _isModuleGpoDisabled = isModuleGpoDisabled;
            _resourceLoader = resourceLoader;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            _generalSettings = _settingsRepository.SettingsConfig;
            _generalSettings.AddEnabledModuleChangeNotification(ModuleEnabledChanged);
            _settingsRepository.SettingsChanged += OnSettingsChanged;

            InitializeItems();
        }

        private void OnSettingsChanged(GeneralSettings newSettings)
        {
            if (_dispatcherQueue != null)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    _generalSettings = newSettings;
                    _generalSettings.AddEnabledModuleChangeNotification(ModuleEnabledChanged);
                    RefreshItemsVisibility();
                });
            }
        }

        private void InitializeItems()
        {
            AddFlyoutMenuItem(ModuleType.ColorPicker);
            AddFlyoutMenuItem(ModuleType.CmdPal);
            AddFlyoutMenuItem(ModuleType.EnvironmentVariables);
            AddFlyoutMenuItem(ModuleType.FancyZones);
            AddFlyoutMenuItem(ModuleType.Hosts);
            AddFlyoutMenuItem(ModuleType.LightSwitch);
            AddFlyoutMenuItem(ModuleType.PowerDisplay);
            AddFlyoutMenuItem(ModuleType.PowerLauncher);
            AddFlyoutMenuItem(ModuleType.PowerOCR);
            AddFlyoutMenuItem(ModuleType.RegistryPreview);
            AddFlyoutMenuItem(ModuleType.MeasureTool);
            AddFlyoutMenuItem(ModuleType.ShortcutGuide);
            AddFlyoutMenuItem(ModuleType.Workspaces);
        }

        private void AddFlyoutMenuItem(ModuleType moduleType)
        {
            if (_isModuleGpoDisabled(moduleType))
            {
                return;
            }

            Items.Add(new QuickAccessItem
            {
                Title = _resourceLoader.GetString(Microsoft.PowerToys.Settings.UI.Library.Helpers.ModuleHelper.GetModuleLabelResourceName(moduleType)),
                Tag = moduleType,
                Visible = Microsoft.PowerToys.Settings.UI.Library.Helpers.ModuleHelper.GetIsModuleEnabled(_generalSettings, moduleType),
                Description = GetModuleToolTip(moduleType),
                Icon = Microsoft.PowerToys.Settings.UI.Library.Helpers.ModuleHelper.GetModuleTypeFluentIconName(moduleType),
                Command = new RelayCommand(() => _launcher.Launch(moduleType)),
            });
        }

        private void ModuleEnabledChanged()
        {
            if (_dispatcherQueue != null)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    _generalSettings = _settingsRepository.SettingsConfig;
                    _generalSettings.AddEnabledModuleChangeNotification(ModuleEnabledChanged);
                    RefreshItemsVisibility();
                });
            }
        }

        private void RefreshItemsVisibility()
        {
            foreach (var item in Items)
            {
                if (item.Tag is ModuleType moduleType)
                {
                    item.Visible = Microsoft.PowerToys.Settings.UI.Library.Helpers.ModuleHelper.GetIsModuleEnabled(_generalSettings, moduleType);
                }
            }
        }

        private string GetModuleToolTip(ModuleType moduleType)
        {
            return moduleType switch
            {
                ModuleType.ColorPicker => SettingsRepository<ColorPickerSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.ActivationShortcut.ToString(),
                ModuleType.FancyZones => SettingsRepository<FancyZonesSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.FancyzonesEditorHotkey.Value.ToString(),
                ModuleType.PowerDisplay => SettingsRepository<PowerDisplaySettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.ActivationShortcut.ToString(),
                ModuleType.LightSwitch => SettingsRepository<LightSwitchSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.ToggleThemeHotkey.Value.ToString(),
                ModuleType.PowerLauncher => SettingsRepository<PowerLauncherSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.OpenPowerLauncher.ToString(),
                ModuleType.PowerOCR => SettingsRepository<PowerOcrSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.ActivationShortcut.ToString(),
                ModuleType.Workspaces => SettingsRepository<WorkspacesSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.Hotkey.Value.ToString(),
                ModuleType.MeasureTool => SettingsRepository<MeasureToolSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.ActivationShortcut.ToString(),
                ModuleType.ShortcutGuide => GetShortcutGuideToolTip(),
                _ => string.Empty,
            };
        }

        private string GetShortcutGuideToolTip()
        {
            var shortcutGuideSettings = SettingsRepository<ShortcutGuideSettings>.GetInstance(SettingsUtils.Default).SettingsConfig;
            return shortcutGuideSettings.Properties.UseLegacyPressWinKeyBehavior.Value
                ? "Win"
                : shortcutGuideSettings.Properties.OpenShortcutGuide.ToString();
        }
    }
}
