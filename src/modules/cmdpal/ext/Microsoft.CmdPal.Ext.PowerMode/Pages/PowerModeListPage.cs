// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly PowerPlanService _powerPlanService;
    private readonly PowerModeDataManager _dataManager;
    private readonly ListItem _statusItem;
    private readonly ListItem _batteryStatusItem;
    private readonly ListItem _energySaverItem;
    private readonly ListItem _powerPlanStatusItem;

    private ListItem? _efficiencyItem;
    private ListItem? _balancedItem;
    private ListItem? _performanceItem;
    private IListItem[] _items = [];
    private List<ListItem> _powerPlanItems = [];
    private IReadOnlyList<Guid> _cachedPlanGuids = [];
    private bool _itemsIncludeModeCommands;
    private bool _itemsIncludePowerPlans;
    private bool _itemsInitialized;

    internal event Action? LiveStateChanged;

    internal PowerModeListPage(
        PowerModeService powerModeService,
        EnergySaverService energySaverService,
        PowerPlanService powerPlanService,
        PowerModeDataManager dataManager)
    {
        _powerModeService = powerModeService;
        _energySaverService = energySaverService;
        _powerPlanService = powerPlanService;
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

        var refreshPlanCommand = new RefreshPowerPlanStatusCommand(RefreshPresentation)
        {
            Id = "com.microsoft.cmdpal.powermode.planStatus",
        };
        _powerPlanStatusItem = new ListItem(new CommandItem(refreshPlanCommand))
        {
            Title = Resources.power_plan_status_title,
            Icon = Icons.PowerPlanIcon,
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
        var planSnapshot = _powerPlanService.GetSnapshot();
        var supportsPlans = planSnapshot.CanReadPlans;
        RebuildPowerPlanItemsIfNeeded(planSnapshot, force);

        if (!force
            && _itemsInitialized
            && _itemsIncludeModeCommands == supportsControl
            && _itemsIncludePowerPlans == supportsPlans
            && (!_itemsIncludePowerPlans || _cachedPlanGuids.SequenceEqual(planSnapshot.AvailablePlans.Select(p => p.SchemeGuid))))
        {
            return;
        }

        var list = new List<IListItem>();

        AddSection(list, Resources.power_section_battery, _batteryStatusItem);

        AddSection(list, Resources.power_section_energy_saver, _energySaverItem);

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

            AddSection(
                list,
                Resources.power_section_power_mode,
                _statusItem,
                _efficiencyItem,
                _balancedItem,
                _performanceItem);
        }
        else
        {
            _efficiencyItem = null;
            _balancedItem = null;
            _performanceItem = null;
        }

        if (supportsPlans)
        {
            AddSection(
                list,
                Resources.power_section_power_plan,
                [_powerPlanStatusItem, .. _powerPlanItems]);
        }

        var structureChanged = _itemsInitialized
            && (_itemsIncludeModeCommands != supportsControl
                || _itemsIncludePowerPlans != supportsPlans
                || (supportsPlans && !_cachedPlanGuids.SequenceEqual(planSnapshot.AvailablePlans.Select(p => p.SchemeGuid))));
        _itemsIncludeModeCommands = supportsControl;
        _itemsIncludePowerPlans = supportsPlans;
        _items = list.ToArray();
        _itemsInitialized = true;

        if (structureChanged)
        {
            RaiseItemsChanged(_items.Length);
        }
    }

    private static void AddSection(List<IListItem> list, string sectionTitle, params ListItem[] items)
    {
        AddSection(list, sectionTitle, (IEnumerable<ListItem>)items);
    }

    private static void AddSection(List<IListItem> list, string sectionTitle, IEnumerable<ListItem> items)
    {
        list.Add(new Separator(sectionTitle));
        foreach (var item in items)
        {
            item.Section = sectionTitle;
            list.Add(item);
        }
    }

    private void RebuildPowerPlanItemsIfNeeded(PowerPlanSnapshot snapshot, bool force)
    {
        if (!snapshot.CanReadPlans)
        {
            if (_powerPlanItems.Count > 0 || _cachedPlanGuids.Count > 0)
            {
                _powerPlanItems = [];
                _cachedPlanGuids = [];
            }

            return;
        }

        var planGuids = snapshot.AvailablePlans.Select(plan => plan.SchemeGuid).ToArray();
        if (!force && _cachedPlanGuids.SequenceEqual(planGuids))
        {
            RefreshPowerPlanItemSubtitles(snapshot);
            return;
        }

        _cachedPlanGuids = planGuids;
        _powerPlanItems = snapshot.AvailablePlans
            .Select(plan => CreatePlanItem(plan, snapshot))
            .ToList();
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

    private ListItem CreatePlanItem(PowerPlanInfo plan, PowerPlanSnapshot snapshot)
    {
        return new ListItem(new SetPowerPlanCommand(_powerPlanService, plan.SchemeGuid, plan.DisplayName, RefreshPresentation))
        {
            Title = PowerPlanDisplayHelper.GetPlanTitle(plan),
            Subtitle = PowerPlanDisplayHelper.GetPlanItemSubtitle(plan, snapshot),
            Icon = Icons.PlanGlyph(plan.SchemeGuid),
        };
    }

    private void RefreshPowerPlanItemSubtitles(PowerPlanSnapshot snapshot)
    {
        for (var i = 0; i < _powerPlanItems.Count && i < snapshot.AvailablePlans.Count; i++)
        {
            var plan = snapshot.AvailablePlans[i];
            _powerPlanItems[i].Subtitle = PowerPlanDisplayHelper.GetPlanItemSubtitle(plan, snapshot);
            _powerPlanItems[i].Icon = Icons.PlanGlyph(plan.SchemeGuid);
        }
    }

    private void RefreshPresentation()
    {
        var snapshot = _powerModeService.GetSnapshot();
        var energySaverSnapshot = _energySaverService.GetSnapshot();
        var planSnapshot = _powerPlanService.GetSnapshot();

        _statusItem.Subtitle = PowerModeDisplayHelper.GetStatusSubtitle(snapshot);
        _statusItem.Icon = Icons.Glyph(snapshot.UserMode);
        _batteryStatusItem.Subtitle = PowerModeDisplayHelper.GetBatteryStatusLabel(snapshot);
        _batteryStatusItem.Icon = Icons.BatteryStatusGlyph(snapshot);

        _energySaverItem.Subtitle = PowerModeDisplayHelper.GetEnergySaverStatusLabel(energySaverSnapshot);
        _energySaverItem.Icon = Icons.EnergySaverIcon;

        _powerPlanStatusItem.Subtitle = PowerPlanDisplayHelper.GetStatusSubtitle(planSnapshot);
        _powerPlanStatusItem.Icon = Icons.PlanGlyph(planSnapshot);
        RefreshPowerPlanItemSubtitles(planSnapshot);

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
