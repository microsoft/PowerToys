// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.Power.Helpers;
using Microsoft.CmdPal.Ext.Power.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Power.Pages;

internal sealed partial class PowerPlanPickerPage : OnLoadStaticListPage
{
    public override string Id => "com.microsoft.cmdpal.power.planPicker";

    private readonly PowerPlanService _powerPlanService;
    private readonly PowerModeDataManager _dataManager;
    private readonly PowerListItemBuilder _itemBuilder;
    private readonly Action _onChanged;

    private IListItem[] _items = [];
    private List<ListItem> _planItems = [];
    private IReadOnlyList<Guid> _cachedPlanGuids = [];

    internal PowerPlanPickerPage(
        PowerPlanService powerPlanService,
        PowerModeDataManager dataManager,
        PowerListItemBuilder itemBuilder,
        Action onChanged)
    {
        _powerPlanService = powerPlanService;
        _dataManager = dataManager;
        _itemBuilder = itemBuilder;
        _onChanged = onChanged;
        Title = Resources.power_section_power_plan;
        Name = Resources.power_section_power_plan;
        Icon = Icons.PowerPlanBandIcon;

        RebuildItems(force: true);
    }

    public override IListItem[] GetItems()
    {
        RebuildItems(force: false);
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

    internal void HandleLiveStateChanged()
    {
        RebuildItems(force: false);
        RefreshPresentation();
    }

    private void RebuildItems(bool force)
    {
        var snapshot = _powerPlanService.GetSnapshot();
        if (!snapshot.CanReadPlans)
        {
            if (_planItems.Count > 0)
            {
                _planItems = [];
                _cachedPlanGuids = [];
                _items = [];
                RaiseItemsChanged(0);
            }

            return;
        }

        var planGuids = snapshot.AvailablePlans.Select(plan => plan.SchemeGuid).ToArray();
        if (!force && _cachedPlanGuids.SequenceEqual(planGuids))
        {
            RefreshPresentation();
            return;
        }

        var structureChanged = _cachedPlanGuids.Count > 0 && !_cachedPlanGuids.SequenceEqual(planGuids);
        _cachedPlanGuids = planGuids;
        _planItems = snapshot.AvailablePlans
            .Select(plan => _itemBuilder.CreatePlanItem(plan, snapshot, _onChanged, dismissOnSuccess: true))
            .ToList();
        _items = _planItems.ToArray<IListItem>();

        if (structureChanged)
        {
            RaiseItemsChanged(_items.Length);
        }
    }

    private void RefreshPresentation()
    {
        var snapshot = _powerPlanService.GetSnapshot();
        for (var i = 0; i < _planItems.Count && i < snapshot.AvailablePlans.Count; i++)
        {
            _itemBuilder.RefreshPlanItem(_planItems[i], snapshot.AvailablePlans[i], snapshot);
        }
    }
}
