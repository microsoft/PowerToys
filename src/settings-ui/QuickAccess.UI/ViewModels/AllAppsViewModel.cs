// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.QuickAccess.Helpers;
using Microsoft.PowerToys.QuickAccess.Services;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.QuickAccess.ViewModels;

public sealed class AllAppsViewModel : Observable
{
    private readonly IQuickAccessCoordinator _coordinator;
    private readonly ISettingsRepository<GeneralSettings> _settingsRepository;
    private readonly ResourceLoader _resourceLoader;
    private readonly DispatcherQueue _dispatcherQueue;
    private GeneralSettings _generalSettings;

    public ObservableCollection<FlyoutMenuItem> FlyoutMenuItems { get; }

    public AllAppsViewModel(IQuickAccessCoordinator coordinator)
    {
        _coordinator = coordinator;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        var settingsUtils = new SettingsUtils();
        _settingsRepository = SettingsRepository<GeneralSettings>.GetInstance(settingsUtils);
        _generalSettings = _settingsRepository.SettingsConfig;
        _generalSettings.AddEnabledModuleChangeNotification(ModuleEnabledChangedOnSettingsPage);
        _settingsRepository.SettingsChanged += OnSettingsChanged;

        _resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
        FlyoutMenuItems = new ObservableCollection<FlyoutMenuItem>();

        foreach (ModuleType moduleType in Enum.GetValues<ModuleType>())
        {
            AddFlyoutMenuItem(moduleType);
        }
    }

    private void OnSettingsChanged(GeneralSettings newSettings)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            ModuleEnabledChangedOnSettingsPage();
        });
    }

    private void AddFlyoutMenuItem(ModuleType moduleType)
    {
        var gpo = ModuleHelper.GetModuleGpoConfiguration(moduleType);
        var isLocked = gpo is GpoRuleConfigured.Enabled or GpoRuleConfigured.Disabled;
        var isEnabled = gpo == GpoRuleConfigured.Enabled || (!isLocked && ModuleHelper.GetIsModuleEnabled(_generalSettings, moduleType));

        FlyoutMenuItems.Add(new FlyoutMenuItem
        {
            Label = _resourceLoader.GetString(ModuleHelper.GetModuleLabelResourceName(moduleType)),
            IsEnabled = isEnabled,
            IsLocked = isLocked,
            Tag = moduleType,
            Icon = ModuleHelper.GetModuleTypeFluentIconName(moduleType),
            EnabledChangedCallback = EnabledChangedOnUI,
        });
    }

    private void EnabledChangedOnUI(FlyoutMenuItem item)
    {
        if (_coordinator.UpdateModuleEnabled(item.Tag, item.IsEnabled))
        {
            _coordinator.NotifyUserSettingsInteraction();
        }
    }

    private void ModuleEnabledChangedOnSettingsPage()
    {
        _generalSettings = _settingsRepository.SettingsConfig;
        _generalSettings.AddEnabledModuleChangeNotification(ModuleEnabledChangedOnSettingsPage);

        foreach (var item in FlyoutMenuItems)
        {
            if (!item.IsLocked)
            {
                item.IsEnabled = ModuleHelper.GetIsModuleEnabled(_generalSettings, item.Tag);
            }
        }
    }
}
