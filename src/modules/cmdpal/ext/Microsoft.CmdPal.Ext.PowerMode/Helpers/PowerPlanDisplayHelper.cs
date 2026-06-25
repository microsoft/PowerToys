// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.PowerMode.Properties;

namespace Microsoft.CmdPal.Ext.PowerMode.Helpers;

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

    internal static string GetPlanTitle(Guid schemeGuid, string displayName)
    {
        if (schemeGuid == PowerPlanGuids.UltimatePerformance)
        {
            return displayName + "+";
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
