// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Power.Properties;

namespace Microsoft.CmdPal.Ext.Power.Helpers;

internal static class PowerPlanDisplayHelper
{
    internal static string GetStatusSubtitle(PowerPlanSnapshot snapshot)
    {
        if (!snapshot.CanReadPlans)
        {
            return Resources.power_plan_status_unavailable;
        }

        if (snapshot.ActivePlan is { } activePlan)
        {
            return GetPlanTitle(activePlan);
        }

        return Resources.power_plan_status_unknown;
    }

    internal static string GetPlanTitle(PowerPlanInfo plan) =>
        GetPlanTitle(plan.SchemeGuid, plan.DisplayName);

    internal static string GetPlanTitle(Guid schemeGuid, string displayName) => displayName;

    internal static string GetPlanShortTitle(PowerPlanInfo plan) =>
        GetPlanShortTitle(plan.SchemeGuid, plan.DisplayName);

    internal static string GetPlanShortTitle(Guid schemeGuid, string displayName)
    {
        if (schemeGuid == PowerPlanGuids.PowerSaver)
        {
            return Resources.power_plan_power_saver_short;
        }

        if (schemeGuid == PowerPlanGuids.Balanced)
        {
            return Resources.power_plan_balanced_short;
        }

        if (schemeGuid == PowerPlanGuids.HighPerformance)
        {
            return Resources.power_plan_high_performance_short;
        }

        if (schemeGuid == PowerPlanGuids.UltimatePerformance)
        {
            return Resources.power_plan_ultimate_performance_short;
        }

        return displayName;
    }

    internal static string GetPlanItemSubtitle(PowerPlanInfo plan, PowerPlanSnapshot snapshot)
    {
        if (snapshot.ActivePlan?.SchemeGuid == plan.SchemeGuid)
        {
            return Resources.power_plan_current;
        }

        return string.Empty;
    }
}
