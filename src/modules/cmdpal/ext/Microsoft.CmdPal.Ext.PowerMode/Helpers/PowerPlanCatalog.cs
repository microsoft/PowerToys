// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.PowerMode.Properties;

namespace Microsoft.CmdPal.Ext.PowerMode.Helpers;

internal static class PowerPlanCatalog
{
    private const int UnknownPlanSpeedOrder = 100;

    internal static int GetSpeedOrder(Guid schemeGuid)
    {
        if (schemeGuid == PowerPlanGuids.PowerSaver)
        {
            return 0;
        }

        if (schemeGuid == PowerPlanGuids.Balanced)
        {
            return 1;
        }

        if (schemeGuid == PowerPlanGuids.HighPerformance)
        {
            return 2;
        }

        if (schemeGuid == PowerPlanGuids.UltimatePerformance)
        {
            return 3;
        }

        return UnknownPlanSpeedOrder;
    }

    internal static int CompareBySpeed(PowerPlanInfo left, PowerPlanInfo right)
    {
        var order = GetSpeedOrder(left.SchemeGuid).CompareTo(GetSpeedOrder(right.SchemeGuid));
        if (order != 0)
        {
            return order;
        }

        return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    internal static void SortBySpeed(List<PowerPlanInfo> plans)
    {
        plans.Sort(CompareBySpeed);
    }

    internal static bool TryGetKnownDescription(Guid schemeGuid, out string description)
    {
        if (schemeGuid == PowerPlanGuids.Balanced)
        {
            description = Resources.power_plan_desc_balanced;
            return true;
        }

        if (schemeGuid == PowerPlanGuids.HighPerformance)
        {
            description = Resources.power_plan_desc_high_performance;
            return true;
        }

        if (schemeGuid == PowerPlanGuids.PowerSaver)
        {
            description = Resources.power_plan_desc_power_saver;
            return true;
        }

        if (schemeGuid == PowerPlanGuids.UltimatePerformance)
        {
            description = Resources.power_plan_desc_ultimate_performance;
            return true;
        }

        description = string.Empty;
        return false;
    }
}
