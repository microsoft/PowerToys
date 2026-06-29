// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Power.Helpers;
using Microsoft.CmdPal.Ext.Power.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Power.Pages;

internal sealed partial class PowerModeDockItem : ListItem
{
    private readonly PowerModeService _powerModeService;

    internal PowerModeDockItem(PowerModeService powerModeService, PowerModePickerPage pickerPage)
    {
        _powerModeService = powerModeService;
        Command = pickerPage;
        RefreshDisplay();
    }

    internal void RefreshDisplay()
    {
        Subtitle = Resources.power_mode_dock_item_subtitle;

        var snapshot = _powerModeService.GetSnapshot();
        if (!snapshot.CanReadUserMode)
        {
            Title = Resources.power_mode_unknown_short;
            Icon = Icons.UnknownIcon;
            return;
        }

        Title = PowerModeDisplayHelper.GetUserModeShortLabel(snapshot.UserMode);
        Icon = Icons.Glyph(snapshot.UserMode);
    }
}

internal sealed partial class PowerPlanDockItem : ListItem
{
    private readonly PowerPlanService _powerPlanService;

    internal PowerPlanDockItem(PowerPlanService powerPlanService, PowerPlanPickerPage pickerPage)
    {
        _powerPlanService = powerPlanService;
        Command = pickerPage;
        RefreshDisplay();
    }

    internal void RefreshDisplay()
    {
        Subtitle = Resources.power_plan_dock_item_subtitle;

        var snapshot = _powerPlanService.GetSnapshot();
        if (!snapshot.CanReadPlans || snapshot.ActivePlan is not { } activePlan)
        {
            Title = Resources.power_plan_unknown_short;
            Icon = Icons.PlanGlyph(snapshot);
            return;
        }

        Title = PowerPlanDisplayHelper.GetPlanShortTitle(activePlan);
        Icon = Icons.PlanGlyph(activePlan.SchemeGuid);
    }
}
