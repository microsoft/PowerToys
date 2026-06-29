// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Power.Helpers;
using Microsoft.CmdPal.Ext.Power.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Power.Pages;

internal sealed partial class PowerDockPage : OnLoadStaticListPage
{
    private const string BaseId = "com.microsoft.cmdpal.power";

    private readonly PowerDockScope _scope;
    private readonly PowerModeDataManager _dataManager;
    private readonly PowerModeDockItem? _modeDockItem;
    private readonly PowerPlanDockItem? _planDockItem;
    private IListItem[] _items = [];

    public override string Id => _scope switch
    {
        PowerDockScope.Mode => $"{BaseId}.mode",
        PowerDockScope.Plan => $"{BaseId}.plan",
        _ => BaseId,
    };

    public override IconInfo Icon => _scope switch
    {
        PowerDockScope.Mode => Icons.PowerModeBandIcon,
        PowerDockScope.Plan => Icons.PowerPlanBandIcon,
        _ => Icons.PowerExtensionIcon,
    };

    internal PowerDockPage(
        PowerDockScope scope,
        PowerModeService powerModeService,
        PowerPlanService powerPlanService,
        PowerModePickerPage modePickerPage,
        PowerPlanPickerPage planPickerPage,
        PowerModeDataManager dataManager)
    {
        _scope = scope;
        _dataManager = dataManager;
        Title = scope switch
        {
            PowerDockScope.Mode => Resources.power_mode_dock_band_title,
            PowerDockScope.Plan => Resources.power_plan_dock_band_title,
            _ => Resources.power_dock_band_title,
        };
        Name = Title;

        if (scope is PowerDockScope.All or PowerDockScope.Mode)
        {
            _modeDockItem = new PowerModeDockItem(powerModeService, modePickerPage);
        }

        if (scope is PowerDockScope.All or PowerDockScope.Plan)
        {
            _planDockItem = new PowerPlanDockItem(powerPlanService, planPickerPage);
        }

        RebuildItems();
    }

    public override IListItem[] GetItems() => _items;

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
        RefreshPresentation();
    }

    private void RebuildItems()
    {
        var items = new List<IListItem>();
        if (_modeDockItem is not null)
        {
            items.Add(_modeDockItem);
        }

        if (_planDockItem is not null)
        {
            items.Add(_planDockItem);
        }

        _items = items.ToArray();
    }

    private void RefreshPresentation()
    {
        _modeDockItem?.RefreshDisplay();
        _planDockItem?.RefreshDisplay();
    }
}
