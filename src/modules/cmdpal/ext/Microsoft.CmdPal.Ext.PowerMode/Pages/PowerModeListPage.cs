// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.PowerMode.Commands;
using Microsoft.CmdPal.Ext.PowerMode.Helpers;
using Microsoft.CmdPal.Ext.PowerMode.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerMode.Pages;

internal sealed partial class PowerModeListPage : OnLoadStaticListPage
{
    public override string Id => "com.microsoft.cmdpal.powermode";

    private readonly PowerModeService _powerModeService;
    private readonly EnergySaverService _energySaverService;
    private readonly PowerModeDataManager _dataManager;
    private readonly ListItem _statusItem;
    private readonly ListItem _batteryStatusItem;
    private readonly ListItem _energySaverItem;

    private ListItem? _efficiencyItem;
    private ListItem? _balancedItem;
    private ListItem? _performanceItem;
    private IListItem[] _items = [];
    private bool _itemsIncludeModeCommands;
    private bool _itemsInitialized;

    internal event Action? LiveStateChanged;

    internal PowerModeListPage(
        PowerModeService powerModeService,
        EnergySaverService energySaverService,
        PowerModeDataManager dataManager)
    {
        _powerModeService = powerModeService;
        _energySaverService = energySaverService;
        _dataManager = dataManager;
        Title = Resources.power_mode_page_title;
        Name = Resources.power_mode_page_title;
        Icon = Icons.PowerModeIcon;
        ShowDetails = true;

        var refreshCommand = new RefreshPowerModeStatusCommand(RefreshPresentation)
        {
            Id = "com.microsoft.cmdpal.powermode.status",
        };
        _statusItem = new ListItem(new CommandItem(refreshCommand))
        {
            Title = Resources.power_mode_status_title,
            Icon = Icons.PowerModeIcon,
        };

        _batteryStatusItem = new ListItem(new NoOpCommand())
        {
            Title = Resources.power_mode_battery_status_title,
            Icon = Icons.BatteryUnknownIcon,
        };

        _energySaverItem = new ListItem(new ToggleEnergySaverCommand(_energySaverService, RefreshPresentation))
        {
            Title = Resources.power_mode_energy_saver_title,
            Icon = Icons.EnergySaverIcon,
        };

        RebuildItemListIfNeeded(force: true);
        RefreshPresentation();
    }

    public override IListItem[] GetItems()
    {
        RebuildItemListIfNeeded(force: false);
        return _items;
    }

    internal string GetDockTitle()
    {
        var snapshot = _powerModeService.GetSnapshot();
        if (!snapshot.CanReadUserMode)
        {
            return Resources.power_mode_dock_band_title;
        }

        return PowerModeDisplayHelper.GetUserModeShortLabel(snapshot.UserMode);
    }

    internal string GetDockSubtitle()
    {
        var snapshot = _powerModeService.GetSnapshot();
        return PowerModeDisplayHelper.GetBatteryStatusLabel(snapshot);
    }

    internal IconInfo GetDockIcon()
    {
        var snapshot = _powerModeService.GetSnapshot();
        return snapshot.CanReadUserMode
            ? Icons.Glyph(snapshot.UserMode)
            : Icons.PowerModeIcon;
    }

    protected override void Loaded()
    {
        _dataManager.PushActivate();
        RefreshPresentation();
    }

    protected override void Unloaded()
    {
        _dataManager.PopActivate();
    }

    private void RebuildItemListIfNeeded(bool force)
    {
        var supportsControl = _powerModeService.SupportsPowerModeControl();
        if (!force && _itemsInitialized && _itemsIncludeModeCommands == supportsControl)
        {
            return;
        }

        var list = new List<ListItem>
        {
            _statusItem,
            _batteryStatusItem,
            _energySaverItem,
        };

        if (supportsControl)
        {
            _efficiencyItem = CreateModeItem(
                UserPowerMode.BestEfficiency,
                Resources.power_mode_set_efficiency_title,
                Resources.power_mode_set_efficiency_toast);
            _balancedItem = CreateModeItem(
                UserPowerMode.Balanced,
                Resources.power_mode_set_balanced_title,
                Resources.power_mode_set_balanced_toast);
            _performanceItem = CreateModeItem(
                UserPowerMode.BestPerformance,
                Resources.power_mode_set_performance_title,
                Resources.power_mode_set_performance_toast);
            list.Add(_efficiencyItem);
            list.Add(_balancedItem);
            list.Add(_performanceItem);
        }
        else
        {
            _efficiencyItem = null;
            _balancedItem = null;
            _performanceItem = null;
        }

        var structureChanged = _itemsInitialized && _itemsIncludeModeCommands != supportsControl;
        _itemsIncludeModeCommands = supportsControl;
        _items = list.ToArray();
        _itemsInitialized = true;

        if (structureChanged)
        {
            RaiseItemsChanged(_items.Length);
        }
    }

    private ListItem CreateModeItem(UserPowerMode mode, string title, string successToast)
    {
        var snapshot = _powerModeService.GetSnapshot();
        return new ListItem(new SetPowerModeCommand(_powerModeService, mode, successToast, RefreshPresentation))
        {
            Title = title,
            Subtitle = PowerModeDisplayHelper.GetSetModeSubtitle(mode, snapshot),
            Icon = Icons.Glyph(mode),
        };
    }

    private void RefreshPresentation()
    {
        var snapshot = _powerModeService.GetSnapshot();
        var energySaverSnapshot = _energySaverService.GetSnapshot();

        _statusItem.Subtitle = PowerModeDisplayHelper.GetStatusSubtitle(snapshot);
        _statusItem.Icon = Icons.PowerModeIcon;
        _batteryStatusItem.Subtitle = PowerModeDisplayHelper.GetBatteryStatusLabel(snapshot);
        _batteryStatusItem.Icon = Icons.BatteryStatusGlyph(snapshot);

        _energySaverItem.Subtitle = PowerModeDisplayHelper.GetEnergySaverStatusLabel(energySaverSnapshot);
        _energySaverItem.Icon = Icons.EnergySaverIcon;

        if (_efficiencyItem is not null)
        {
            _efficiencyItem.Subtitle = PowerModeDisplayHelper.GetSetModeSubtitle(UserPowerMode.BestEfficiency, snapshot);
        }

        if (_balancedItem is not null)
        {
            _balancedItem.Subtitle = PowerModeDisplayHelper.GetSetModeSubtitle(UserPowerMode.Balanced, snapshot);
        }

        if (_performanceItem is not null)
        {
            _performanceItem.Subtitle = PowerModeDisplayHelper.GetSetModeSubtitle(UserPowerMode.BestPerformance, snapshot);
        }

        LiveStateChanged?.Invoke();
    }

    internal void HandleLiveStateChanged()
    {
        RebuildItemListIfNeeded(force: false);
        RefreshPresentation();
    }
}
