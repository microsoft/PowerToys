// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.QuickAccess.Helpers;
using Microsoft.PowerToys.QuickAccess.Services;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.QuickAccess.ViewModels;

public sealed class AllAppsViewModel : Observable
{
    private readonly IQuickAccessCoordinator _coordinator;
    private readonly ISettingsRepository<GeneralSettings> _settingsRepository;
    private readonly SettingsUtils _settingsUtils;
    private readonly ResourceLoader _resourceLoader;
    private readonly DispatcherQueue _dispatcherQueue;
    private GeneralSettings _generalSettings;

    public ObservableCollection<FlyoutMenuItem> FlyoutMenuItems { get; }

    public DashboardSortOrder DashboardSortOrder
    {
        get => _generalSettings.DashboardSortOrder;
        set
        {
            if (_generalSettings.DashboardSortOrder != value)
            {
                _generalSettings.DashboardSortOrder = value;
                _settingsUtils.SaveSettings(_generalSettings.ToJsonString(), _generalSettings.GetModuleName());
                OnPropertyChanged();
                RefreshFlyoutMenuItems();
            }
        }
    }

    public AllAppsViewModel(IQuickAccessCoordinator coordinator)
    {
        _coordinator = coordinator;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _settingsUtils = SettingsUtils.Default;
        _settingsRepository = SettingsRepository<GeneralSettings>.GetInstance(_settingsUtils);
        _generalSettings = _settingsRepository.SettingsConfig;
        _generalSettings.AddEnabledModuleChangeNotification(ModuleEnabledChangedOnSettingsPage);
        _settingsRepository.SettingsChanged += OnSettingsChanged;

        _resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
        FlyoutMenuItems = new ObservableCollection<FlyoutMenuItem>();

        RefreshFlyoutMenuItems();
    }

    private void OnSettingsChanged(GeneralSettings newSettings)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            _generalSettings = newSettings;
            _generalSettings.AddEnabledModuleChangeNotification(ModuleEnabledChangedOnSettingsPage);
            OnPropertyChanged(nameof(DashboardSortOrder));
            RefreshFlyoutMenuItems();
        });
    }

    public void RefreshSettings()
    {
        if (_settingsRepository.ReloadSettings())
        {
            OnSettingsChanged(_settingsRepository.SettingsConfig);
        }
    }

    private void RefreshFlyoutMenuItems()
    {
        var desiredItems = new List<FlyoutMenuItem>();

        foreach (ModuleType moduleType in Enum.GetValues<ModuleType>())
        {
            if (moduleType == ModuleType.GeneralSettings)
            {
                continue;
            }

            var gpo = Helpers.ModuleGpoHelper.GetModuleGpoConfiguration(moduleType);
            var isLocked = gpo is GpoRuleConfigured.Enabled or GpoRuleConfigured.Disabled;
            var isEnabled = gpo == GpoRuleConfigured.Enabled || (!isLocked && Microsoft.PowerToys.Settings.UI.Library.Helpers.ModuleHelper.GetIsModuleEnabled(_generalSettings, moduleType));

            var existingItem = FlyoutMenuItems.FirstOrDefault(x => x.Tag == moduleType);

            if (existingItem != null)
            {
                existingItem.Label = _resourceLoader.GetString(Microsoft.PowerToys.Settings.UI.Library.Helpers.ModuleHelper.GetModuleLabelResourceName(moduleType));
                existingItem.IsLocked = isLocked;
                existingItem.Icon = Microsoft.PowerToys.Settings.UI.Library.Helpers.ModuleHelper.GetModuleTypeFluentIconName(moduleType);

                if (existingItem.IsEnabled != isEnabled)
                {
                    var callback = existingItem.EnabledChangedCallback;
                    existingItem.EnabledChangedCallback = null;
                    existingItem.IsEnabled = isEnabled;
                    existingItem.EnabledChangedCallback = callback;
                }

                desiredItems.Add(existingItem);
            }
            else
            {
                desiredItems.Add(new FlyoutMenuItem
                {
                    Label = _resourceLoader.GetString(Microsoft.PowerToys.Settings.UI.Library.Helpers.ModuleHelper.GetModuleLabelResourceName(moduleType)),
                    IsEnabled = isEnabled,
                    IsLocked = isLocked,
                    Tag = moduleType,
                    Icon = Microsoft.PowerToys.Settings.UI.Library.Helpers.ModuleHelper.GetModuleTypeFluentIconName(moduleType),
                    EnabledChangedCallback = EnabledChangedOnUI,
                });
            }
        }

        var sortedItems = DashboardSortOrder switch
        {
            DashboardSortOrder.ByStatus => desiredItems.OrderByDescending(x => x.IsEnabled).ThenBy(x => x.Label).ToList(),
            _ => desiredItems.OrderBy(x => x.Label).ToList(),
        };

        for (int i = FlyoutMenuItems.Count - 1; i >= 0; i--)
        {
            if (!sortedItems.Contains(FlyoutMenuItems[i]))
            {
                FlyoutMenuItems.RemoveAt(i);
            }
        }

        for (int i = 0; i < sortedItems.Count; i++)
        {
            var item = sortedItems[i];
            var oldIndex = FlyoutMenuItems.IndexOf(item);

            if (oldIndex < 0)
            {
                FlyoutMenuItems.Insert(i, item);
            }
            else if (oldIndex != i)
            {
                FlyoutMenuItems.Move(oldIndex, i);
            }
        }
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
        RefreshFlyoutMenuItems();
    }
}
