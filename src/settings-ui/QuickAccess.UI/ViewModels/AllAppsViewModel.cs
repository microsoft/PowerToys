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
using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.QuickAccess.ViewModels;

public sealed class AllAppsViewModel : Observable
{
    private readonly object _sortLock = new object();
    private readonly IQuickAccessCoordinator _coordinator;
    private readonly ISettingsRepository<GeneralSettings> _settingsRepository;
    private readonly SettingsUtils _settingsUtils;
    private readonly ResourceLoader _resourceLoader;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly List<FlyoutMenuItem> _allFlyoutMenuItems = new();
    private GeneralSettings _generalSettings;

    // Flag to prevent toggle operations during sorting to avoid race conditions.
    private bool _isSorting;

    public ObservableCollection<FlyoutMenuItem> FlyoutMenuItems { get; }

    public DashboardSortOrder DashboardSortOrder
    {
        get => _generalSettings.DashboardSortOrder;
        set
        {
            if (_generalSettings.DashboardSortOrder != value)
            {
                _generalSettings.DashboardSortOrder = value;
                _coordinator.SendSortOrderUpdate(_generalSettings);
                OnPropertyChanged();
                SortFlyoutMenuItems();
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
        _settingsRepository.SettingsChanged += OnSettingsChanged;

        _resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
        FlyoutMenuItems = new ObservableCollection<FlyoutMenuItem>();

        BuildFlyoutMenuItems();
        RefreshFlyoutMenuItems();
    }

    private void BuildFlyoutMenuItems()
    {
        _allFlyoutMenuItems.Clear();
        foreach (ModuleType moduleType in Enum.GetValues<ModuleType>())
        {
            if (moduleType == ModuleType.GeneralSettings)
            {
                continue;
            }

            _allFlyoutMenuItems.Add(new FlyoutMenuItem
            {
                Tag = moduleType,
                EnabledChangedCallback = EnabledChangedOnUI,
            });
        }
    }

    private void OnSettingsChanged(GeneralSettings newSettings)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            _generalSettings = newSettings;
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
        foreach (var item in _allFlyoutMenuItems)
        {
            var moduleType = item.Tag;
            var gpo = Helpers.ModuleGpoHelper.GetModuleGpoConfiguration(moduleType);
            var isLocked = gpo is GpoRuleConfigured.Enabled or GpoRuleConfigured.Disabled;
            var isEnabled = gpo == GpoRuleConfigured.Enabled || (!isLocked && Microsoft.PowerToys.Settings.UI.Library.Helpers.ModuleHelper.GetIsModuleEnabled(_generalSettings, moduleType));

            item.Label = _resourceLoader.GetString(Microsoft.PowerToys.Settings.UI.Library.Helpers.ModuleHelper.GetModuleLabelResourceName(moduleType));
            item.IsLocked = isLocked;
            item.Icon = Microsoft.PowerToys.Settings.UI.Library.Helpers.ModuleHelper.GetModuleTypeFluentIconName(moduleType);

            if (item.IsEnabled != isEnabled)
            {
                item.UpdateStatus(isEnabled);
            }
        }

        SortFlyoutMenuItems();
    }

    private void SortFlyoutMenuItems()
    {
        if (_isSorting)
        {
            return;
        }

        lock (_sortLock)
        {
            _isSorting = true;
            try
            {
                var sortedItems = DashboardSortOrder switch
                {
                    DashboardSortOrder.ByStatus => _allFlyoutMenuItems.OrderByDescending(x => x.IsEnabled).ThenBy(x => x.Label).ToList(),
                    _ => _allFlyoutMenuItems.OrderBy(x => x.Label).ToList(),
                };

                if (FlyoutMenuItems.Count == 0)
                {
                    foreach (var item in sortedItems)
                    {
                        FlyoutMenuItems.Add(item);
                    }

                    return;
                }

                for (int i = 0; i < sortedItems.Count; i++)
                {
                    var item = sortedItems[i];
                    var oldIndex = FlyoutMenuItems.IndexOf(item);

                    if (oldIndex != -1 && oldIndex != i)
                    {
                        FlyoutMenuItems.Move(oldIndex, i);
                    }
                }
            }
            finally
            {
                // Use dispatcher to reset flag after UI updates complete
                _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
                {
                    _isSorting = false;
                });
            }
        }
    }

    private void EnabledChangedOnUI(ModuleListItem item)
    {
        var flyoutItem = (FlyoutMenuItem)item;
        var isEnabled = flyoutItem.IsEnabled;

        // Ignore toggle operations during sorting to prevent race conditions.
        // Revert the toggle state since UI already changed due to TwoWay binding.
        if (_isSorting)
        {
            flyoutItem.UpdateStatus(!isEnabled);
            return;
        }

        _coordinator.UpdateModuleEnabled(flyoutItem.Tag, flyoutItem.IsEnabled);
        SortFlyoutMenuItems();
    }
}
