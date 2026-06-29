// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Power.Helpers;
using Microsoft.CmdPal.Ext.Power.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Power.Pages;

internal sealed partial class PowerModePickerPage : OnLoadStaticListPage
{
    public override string Id => "com.microsoft.cmdpal.power.modePicker";

    private readonly PowerModeService _powerModeService;
    private readonly PowerModeDataManager _dataManager;
    private readonly PowerListItemBuilder _itemBuilder;
    private readonly Action _onChanged;

    private ListItem? _efficiencyItem;
    private ListItem? _balancedItem;
    private ListItem? _performanceItem;
    private IListItem[] _items = [];

    internal PowerModePickerPage(
        PowerModeService powerModeService,
        PowerModeDataManager dataManager,
        PowerListItemBuilder itemBuilder,
        Action onChanged)
    {
        _powerModeService = powerModeService;
        _dataManager = dataManager;
        _itemBuilder = itemBuilder;
        _onChanged = onChanged;
        Title = Resources.power_section_power_mode;
        Name = Resources.power_section_power_mode;
        Icon = Icons.PowerModeBandIcon;

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
        if (!_powerModeService.SupportsPowerModeControl())
        {
            _items = [];
            return;
        }

        _efficiencyItem = _itemBuilder.CreateModeItem(
            UserPowerMode.BestEfficiency,
            Resources.power_mode_set_efficiency_title,
            Resources.power_mode_set_efficiency_toast,
            _onChanged,
            dismissOnSuccess: true);
        _balancedItem = _itemBuilder.CreateModeItem(
            UserPowerMode.Balanced,
            Resources.power_mode_set_balanced_title,
            Resources.power_mode_set_balanced_toast,
            _onChanged,
            dismissOnSuccess: true);
        _performanceItem = _itemBuilder.CreateModeItem(
            UserPowerMode.BestPerformance,
            Resources.power_mode_set_performance_title,
            Resources.power_mode_set_performance_toast,
            _onChanged,
            dismissOnSuccess: true);

        _items =
        [
            _efficiencyItem,
            _balancedItem,
            _performanceItem,
        ];
    }

    private void RefreshPresentation()
    {
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
}
