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
using Microsoft.UI.Dispatching;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.QuickAccess.ViewModels;

public sealed class AllAppsViewModel : Observable
{
    private readonly IQuickAccessCoordinator _coordinator;
    private readonly ISettingsRepository<GeneralSettings> _settingsRepository;
    private readonly ISettingsUtils _settingsUtils;
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
        _settingsUtils = new SettingsUtils();
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

    private void RefreshFlyoutMenuItems()
    {
        var desiredItems = new List<FlyoutMenuItem>();

        foreach (ModuleType moduleType in Enum.GetValues<ModuleType>())
        {
            var gpo = ModuleHelper.GetModuleGpoConfiguration(moduleType);
            var isLocked = gpo is GpoRuleConfigured.Enabled or GpoRuleConfigured.Disabled;
            var isEnabled = gpo == GpoRuleConfigured.Enabled || (!isLocked && ModuleHelper.GetIsModuleEnabled(_generalSettings, moduleType));

            var existingItem = FlyoutMenuItems.FirstOrDefault(x => x.Tag == moduleType);

            if (existingItem != null)
            {
                existingItem.Label = _resourceLoader.GetString(ModuleHelper.GetModuleLabelResourceName(moduleType));
                existingItem.IsLocked = isLocked;
                existingItem.Icon = ModuleHelper.GetModuleTypeFluentIconName(moduleType);

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
                    Label = _resourceLoader.GetString(ModuleHelper.GetModuleLabelResourceName(moduleType)),
                    IsEnabled = isEnabled,
                    IsLocked = isLocked,
                    Tag = moduleType,
                    Icon = ModuleHelper.GetModuleTypeFluentIconName(moduleType),
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

            // If sorting by status, we might want to re-sort, but that could be jarring.
            // DashboardViewModel calls RequestConflictData but doesn't seem to re-sort immediately on toggle?
            // Actually DashboardViewModel calls RefreshModuleList() in ModuleEnabledChangedOnSettingsPage, but that's from settings change.
            // EnabledChangedOnUI in DashboardViewModel calls UpdateGeneralSettingsCallback.
            // If we want to re-sort on toggle, we should call RefreshFlyoutMenuItems().
            // But usually users don't like items jumping around when they toggle them.
            // So let's leave it for now.
        }
    }

    private void ModuleEnabledChangedOnSettingsPage()
    {
        // This is called when settings change (via OnSettingsChanged -> this).
        // But OnSettingsChanged already calls RefreshFlyoutMenuItems.
        // However, ModuleEnabledChangedOnSettingsPage is also passed as a callback to GeneralSettings.
        // Wait, GeneralSettings.AddEnabledModuleChangeNotification adds it to a list.
        // But GeneralSettings is just a data object. Who calls the notification?
        // It seems GeneralSettings doesn't have logic to call it itself unless something calls it.
        // In DashboardViewModel, it's called in OnSettingsChanged.

        // In my implementation of OnSettingsChanged, I call RefreshFlyoutMenuItems directly.
        // So I might not need ModuleEnabledChangedOnSettingsPage to do much, or I can remove it if I don't use the callback mechanism inside GeneralSettings (which seems to be for internal notification within the object, but GeneralSettings is just a POCO-like object with some logic).

        // Actually, let's look at DashboardViewModel again.
        // It adds ModuleEnabledChangedOnSettingsPage to generalSettingsConfig.AddEnabledModuleChangeNotification.
        // And OnSettingsChanged calls ModuleEnabledChangedOnSettingsPage.

        // I'll stick to calling RefreshFlyoutMenuItems in OnSettingsChanged.
        // And I'll keep ModuleEnabledChangedOnSettingsPage for compatibility if needed, but maybe just redirect it.
        RefreshFlyoutMenuItems();
    }
}
