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
    public partial class AllAppsViewModel : Observable
    {
        public ObservableCollection<FlyoutMenuItem> FlyoutMenuItems { get; set; }

        private ISettingsRepository<GeneralSettings> _settingsRepository;
        private GeneralSettings generalSettingsConfig;
        private ResourceLoader resourceLoader;

        private Func<string, int> SendConfigMSG { get; }

        public AllAppsViewModel(ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            _settingsRepository = settingsRepository;
            generalSettingsConfig = settingsRepository.SettingsConfig;
            generalSettingsConfig.AddEnabledModuleChangeNotification(ModuleEnabledChangedOnSettingsPage);

            resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
            FlyoutMenuItems = new ObservableCollection<FlyoutMenuItem>();

            foreach (ModuleType moduleType in Enum.GetValues<ModuleType>())
            {
                AddFlyoutMenuItem(moduleType);
            }

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
        }

        private void AddFlyoutMenuItem(ModuleType moduleType)
        {
            GpoRuleConfigured gpo = ModuleHelper.GetModuleGpoConfiguration(moduleType);
            FlyoutMenuItems.Add(new FlyoutMenuItem()
            {
                Label = resourceLoader.GetString(ModuleHelper.GetModuleLabelResourceName(moduleType)),
                IsEnabled = gpo == GpoRuleConfigured.Enabled || (gpo != GpoRuleConfigured.Disabled && ModuleHelper.GetIsModuleEnabled(generalSettingsConfig, moduleType)),
                IsLocked = gpo == GpoRuleConfigured.Enabled || gpo == GpoRuleConfigured.Disabled,
                Tag = moduleType,
                Icon = ModuleHelper.GetModuleTypeFluentIconName(moduleType),
                EnabledChangedCallback = EnabledChangedOnUI,
            });
        }

        private void EnabledChangedOnUI(FlyoutMenuItem flyoutMenuItem)
        {
            if (Views.ShellPage.UpdateGeneralSettingsCallback(flyoutMenuItem.Tag, flyoutMenuItem.IsEnabled))
            {
                Views.ShellPage.DisableFlyoutHidingCallback();
            }
        }

        private void ModuleEnabledChangedOnSettingsPage()
        {
            generalSettingsConfig = _settingsRepository.SettingsConfig;
            generalSettingsConfig.AddEnabledModuleChangeNotification(ModuleEnabledChangedOnSettingsPage);
            foreach (FlyoutMenuItem item in FlyoutMenuItems)
            {
                item.IsEnabled = ModuleHelper.GetIsModuleEnabled(generalSettingsConfig, item.Tag);
            }
        }
    }
}
