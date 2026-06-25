// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Power.Helpers;
using Microsoft.CmdPal.Ext.Power.Pages;
using Microsoft.CmdPal.Ext.Power.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Power;

public sealed partial class PowerCommandsProvider : CommandProvider, IDisposable
{
    private readonly PowerModeService _powerModeService = new();
    private readonly EnergySaverService _energySaverService = new();
    private readonly PowerPlanService _powerPlanService = new();
    private readonly PowerModeDataManager _dataManager;
    private readonly CommandItem _command;
    private readonly CommandItem _dockBand;
    private readonly PowerListPage _listPage;
    private readonly FallbackPowerItem _fallback;

    public PowerCommandsProvider()
    {
        DisplayName = Resources.power_display_name;
        Id = "com.microsoft.cmdpal.builtin.power";
        Icon = Icons.PowerIcon;

        PowerListPage? listPage = null;
        _dataManager = new PowerModeDataManager(
            _powerModeService,
            _energySaverService,
            () => listPage!.HandleLiveStateChanged());
        listPage = new PowerListPage(_powerModeService, _energySaverService, _powerPlanService, _dataManager);
        _listPage = listPage;
        _listPage.LiveStateChanged += UpdateDockPresentation;
        _fallback = new FallbackPowerItem(_listPage);

        _command = new CommandItem(_listPage)
        {
            Title = Resources.power_page_title,
            Icon = Icons.PowerIcon,
        };

        _dockBand = new CommandItem(_listPage)
        {
            Title = Resources.power_dock_band_title,
            Icon = Icons.PowerIcon,
        };

        _dataManager.PushActivate();
        UpdateDockPresentation();
    }

    public override ICommandItem[] TopLevelCommands() => [_command];

    public override IFallbackCommandItem[] FallbackCommands() => [_fallback];

    public override ICommandItem[]? GetDockBands() => [_dockBand];

    public override void Dispose()
    {
        _listPage.LiveStateChanged -= UpdateDockPresentation;
        _dataManager.PopActivate();
        _dataManager.Dispose();
        _energySaverService.Dispose();
        _powerModeService.Dispose();
        GC.SuppressFinalize(this);
        base.Dispose();
    }

    private void UpdateDockPresentation()
    {
        _dockBand.Title = _listPage.GetDockTitle();
        _dockBand.Subtitle = _listPage.GetDockSubtitle();
        _dockBand.Icon = _listPage.GetDockIcon();
    }
}
