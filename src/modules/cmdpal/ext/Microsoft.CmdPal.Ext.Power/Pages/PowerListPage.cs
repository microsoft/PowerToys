// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.Power.Classes;
using Microsoft.CmdPal.Ext.Power.Enumerations;
using Microsoft.CmdPal.Ext.Power.Helpers;
using Microsoft.CmdPal.Ext.Power.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Power.Pages;

internal sealed partial class PowerListPage : OnLoadStaticListPage
{
    public override string Id => "com.microsoft.cmdpal.power";

    private readonly PowerModeService _powerModeService;
    private readonly PowerPlanService _powerPlanService;
    private readonly PowerModeDataManager _dataManager;
    private readonly PowerListItemBuilder _itemBuilder;

    private ListItem? _efficiencyItem;
    private ListItem? _balancedItem;
    private ListItem? _performanceItem;
    private IListItem[] _items = [];
    private List<ListItem> _powerPlanItems = [];
    private IReadOnlyList<Guid> _cachedPlanGuids = [];
    private bool _itemsIncludeModeCommands;
    private bool _itemsIncludePowerPlans;
    private bool _itemsInitialized;

    internal PowerListPage(
        PowerModeService powerModeService,
        PowerPlanService powerPlanService,
        PowerModeDataManager dataManager,
        PowerListItemBuilder itemBuilder)
    {
        _powerModeService = powerModeService;
        _powerPlanService = powerPlanService;
        _dataManager = dataManager;
        _itemBuilder = itemBuilder;
        Title = Resources.power_page_title;
        Name = Resources.power_page_title;
        Icon = Icons.PowerExtensionIcon;
        ShowDetails = true;

        RebuildItemListIfNeeded(force: true);
        RefreshPresentation();
    }

    public override IListItem[] GetItems()
    {
        RebuildItemListIfNeeded(force: false);
        return _items;
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

        if (supportsControl)
        {
            _efficiencyItem = _itemBuilder.CreateModeItem(
                UserPowerMode.BestEfficiency,
                Resources.power_mode_set_efficiency_title,
                Resources.power_mode_set_efficiency_toast,
                RefreshPresentation);
            _balancedItem = _itemBuilder.CreateModeItem(
                UserPowerMode.Balanced,
                Resources.power_mode_set_balanced_title,
                Resources.power_mode_set_balanced_toast,
                RefreshPresentation);
            _performanceItem = _itemBuilder.CreateModeItem(
                UserPowerMode.BestPerformance,
                Resources.power_mode_set_performance_title,
                Resources.power_mode_set_performance_toast,
                RefreshPresentation);

            AddSection(
                list,
                Resources.power_section_power_mode,
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
            AddSection(list, Resources.power_section_power_plan, _powerPlanItems);
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
            .Select(plan => _itemBuilder.CreatePlanItem(plan, snapshot, RefreshPresentation))
            .ToList();
    }

    private void RefreshPowerPlanItemSubtitles(PowerPlanSnapshot snapshot)
    {
        for (var i = 0; i < _powerPlanItems.Count && i < snapshot.AvailablePlans.Count; i++)
        {
            _itemBuilder.RefreshPlanItem(_powerPlanItems[i], snapshot.AvailablePlans[i], snapshot);
        }
    }

    private void RefreshPresentation()
    {
        var planSnapshot = _powerPlanService.GetSnapshot();

        RefreshPowerPlanItemSubtitles(planSnapshot);

        if (_efficiencyItem is not null)
        {
            _itemBuilder.RefreshModeItem(_efficiencyItem, UserPowerMode.BestEfficiency);
        }

        if (_balancedItem is not null)
        {
            _itemBuilder.RefreshModeItem(_balancedItem, UserPowerMode.Balanced);
        }

        if (_performanceItem is not null)
        {
            _itemBuilder.RefreshModeItem(_performanceItem, UserPowerMode.BestPerformance);
        }
    }

    internal void HandleLiveStateChanged()
    {
        RebuildItemListIfNeeded(force: false);
        RefreshPresentation();
    }
}
