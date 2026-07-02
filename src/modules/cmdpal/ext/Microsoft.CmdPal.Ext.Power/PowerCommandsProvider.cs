// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Power.Helpers;
using Microsoft.CmdPal.Ext.Power.Pages;
using Microsoft.CmdPal.Ext.Power.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Power;

public sealed partial class PowerCommandsProvider : CommandProvider, IDisposable
{
    private readonly PowerModeService _powerModeService = new();
    private readonly PowerPlanService _powerPlanService = new();
    private readonly PowerModeDataManager _dataManager;
    private readonly PowerListItemBuilder _itemBuilder;
    private readonly CommandItem _command;
    private readonly PowerListPage _listPage;
    private readonly PowerModePickerPage _modePickerPage;
    private readonly PowerPlanPickerPage _planPickerPage;
    private readonly PowerDockPage _modeDockPage;
    private readonly PowerDockPage _planDockPage;
    private readonly FallbackPowerItem _fallback;

    public PowerCommandsProvider()
    {
        DisplayName = Resources.power_display_name;
        Id = "com.microsoft.cmdpal.builtin.power";
        Icon = Icons.PowerExtensionIcon;

        _itemBuilder = new PowerListItemBuilder(_powerModeService, _powerPlanService);

        PowerListPage? listPage = null;
        PowerModePickerPage? modePickerPage = null;
        PowerPlanPickerPage? planPickerPage = null;
        PowerDockPage? modeDockPage = null;
        PowerDockPage? planDockPage = null;

        _dataManager = new PowerModeDataManager(_powerModeService, HandleLiveStateChanged);

        listPage = new PowerListPage(_powerModeService, _powerPlanService, _dataManager, _itemBuilder);
        _listPage = listPage;

        modePickerPage = new PowerModePickerPage(_powerModeService, _dataManager, _itemBuilder, HandleLiveStateChanged);
        _modePickerPage = modePickerPage;

        planPickerPage = new PowerPlanPickerPage(_powerPlanService, _dataManager, _itemBuilder, HandleLiveStateChanged);
        _planPickerPage = planPickerPage;

        modeDockPage = new PowerDockPage(
            PowerDockScope.Mode,
            _powerModeService,
            _powerPlanService,
            modePickerPage,
            planPickerPage,
            _dataManager);
        _modeDockPage = modeDockPage;

        planDockPage = new PowerDockPage(
            PowerDockScope.Plan,
            _powerModeService,
            _powerPlanService,
            modePickerPage,
            planPickerPage,
            _dataManager);
        _planDockPage = planDockPage;

        _fallback = new FallbackPowerItem(_listPage);

        _command = new CommandItem(_listPage)
        {
            Title = Resources.power_page_title,
            Icon = Icons.PowerExtensionIcon,
        };

        _dataManager.PushActivate();
    }

    public override ICommandItem[] TopLevelCommands() => [_command];

    public override IFallbackCommandItem[] FallbackCommands() => [_fallback];

    public override ICommandItem[]? GetDockBands()
    {
        var bands = new List<ICommandItem>();

        if (_powerModeService.SupportsPowerModeControl())
        {
            bands.Add(new CommandItem(_modeDockPage)
            {
                Title = Resources.power_mode_dock_band_title,
                Icon = Icons.PowerModeBandIcon,
            });
        }

        if (_powerPlanService.GetSnapshot().CanReadPlans)
        {
            bands.Add(new CommandItem(_planDockPage)
            {
                Title = Resources.power_plan_dock_band_title,
                Icon = Icons.PowerPlanBandIcon,
            });
        }

        return bands.Count > 0 ? bands.ToArray() : null;
    }

    public override void Dispose()
    {
        _dataManager.PopActivate();
        _dataManager.Dispose();
        _powerModeService.Dispose();
        GC.SuppressFinalize(this);
        base.Dispose();
    }

    private void HandleLiveStateChanged()
    {
        _listPage.HandleLiveStateChanged();
        _modePickerPage.HandleLiveStateChanged();
        _planPickerPage.HandleLiveStateChanged();
        _modeDockPage.HandleLiveStateChanged();
        _planDockPage.HandleLiveStateChanged();
    }
}
