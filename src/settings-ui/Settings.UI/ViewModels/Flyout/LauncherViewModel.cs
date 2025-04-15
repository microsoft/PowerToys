// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;

using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class LauncherViewModel : Observable
    {
        public bool IsUpdateAvailable { get; set; }

        public ObservableCollection<FlyoutMenuItem> FlyoutMenuItems { get; set; }

        private GeneralSettings generalSettingsConfig;
        private UpdatingSettings updatingSettingsConfig;
        private ISettingsRepository<GeneralSettings> _settingsRepository;
        private ResourceLoader resourceLoader;

        private Func<string, int> SendIPCMessage { get; }

        public LauncherViewModel(ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            _settingsRepository = settingsRepository;
            generalSettingsConfig = settingsRepository.SettingsConfig;
            generalSettingsConfig.AddEnabledModuleChangeNotification(ModuleEnabledChanged);

            // set the callback functions value to handle outgoing IPC message.
            SendIPCMessage = ipcMSGCallBackFunc;
            resourceLoader = ResourceLoaderInstance.ResourceLoader;
            FlyoutMenuItems = new ObservableCollection<FlyoutMenuItem>();

            AddFlyoutMenuItem(ModuleType.ColorPicker);
            AddFlyoutMenuItem(ModuleType.CmdPal);
            AddFlyoutMenuItem(ModuleType.EnvironmentVariables);
            AddFlyoutMenuItem(ModuleType.FancyZones);
            AddFlyoutMenuItem(ModuleType.Hosts);
            AddFlyoutMenuItem(ModuleType.PowerLauncher);
            AddFlyoutMenuItem(ModuleType.PowerOCR);
            AddFlyoutMenuItem(ModuleType.RegistryPreview);
            AddFlyoutMenuItem(ModuleType.MeasureTool);
            AddFlyoutMenuItem(ModuleType.ShortcutGuide);
            AddFlyoutMenuItem(ModuleType.Workspaces);

            updatingSettingsConfig = UpdatingSettings.LoadSettings();
            if (updatingSettingsConfig == null)
            {
                updatingSettingsConfig = new UpdatingSettings();
            }

            if (updatingSettingsConfig.State == UpdatingSettings.UpdatingState.ReadyToInstall || updatingSettingsConfig.State == UpdatingSettings.UpdatingState.ReadyToDownload)
            {
                IsUpdateAvailable = true;
            }
            else
            {
                IsUpdateAvailable = false;
            }
        }

        private void AddFlyoutMenuItem(ModuleType moduleType)
        {
            if (ModuleHelper.GetModuleGpoConfiguration(moduleType) == GpoRuleConfigured.Disabled)
            {
                return;
            }

            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString(ModuleHelper.GetModuleLabelResourceName(moduleType)),
                Tag = moduleType,
                Visible = ModuleHelper.GetIsModuleEnabled(generalSettingsConfig, moduleType),
                ToolTip = GetModuleToolTip(moduleType),
                Icon = ModuleHelper.GetModuleTypeFluentIconName(moduleType),
            });
        }

        private string GetModuleToolTip(ModuleType moduleType)
        {
            return moduleType switch
            {
                ModuleType.ColorPicker => SettingsRepository<ColorPickerSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ActivationShortcut.ToString(),
                ModuleType.FancyZones => SettingsRepository<FancyZonesSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.FancyzonesEditorHotkey.Value.ToString(),
                ModuleType.PowerLauncher => SettingsRepository<PowerLauncherSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.OpenPowerLauncher.ToString(),
                ModuleType.PowerOCR => SettingsRepository<PowerOcrSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ActivationShortcut.ToString(),
                ModuleType.Workspaces => SettingsRepository<WorkspacesSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.Hotkey.Value.ToString(),
                ModuleType.MeasureTool => SettingsRepository<MeasureToolSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ActivationShortcut.ToString(),
                ModuleType.ShortcutGuide => GetShortcutGuideToolTip(),
                _ => string.Empty,
            };
        }

        private void ModuleEnabledChanged()
        {
            generalSettingsConfig = _settingsRepository.SettingsConfig;
            generalSettingsConfig.AddEnabledModuleChangeNotification(ModuleEnabledChanged);
            foreach (FlyoutMenuItem item in FlyoutMenuItems)
            {
                item.Visible = ModuleHelper.GetIsModuleEnabled(generalSettingsConfig, item.Tag);
            }
        }

        private string GetShortcutGuideToolTip()
        {
            var shortcutGuideSettings = SettingsRepository<ShortcutGuideSettings>.GetInstance(new SettingsUtils()).SettingsConfig;
            return shortcutGuideSettings.Properties.UseLegacyPressWinKeyBehavior.Value
                ? "Win"
                : shortcutGuideSettings.Properties.OpenShortcutGuide.ToString();
        }

        internal void StartBugReport()
        {
            SendIPCMessage("{\"bugreport\": 0 }");
        }

        internal void KillRunner()
        {
            SendIPCMessage("{\"killrunner\": 0 }");
        }
    }
}
