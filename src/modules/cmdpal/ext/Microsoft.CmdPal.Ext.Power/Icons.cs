// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Power.Classes;
using Microsoft.CmdPal.Ext.Power.Constants;
using Microsoft.CmdPal.Ext.Power.Enumerations;
using Microsoft.CmdPal.Ext.Power.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Power;

internal static class Icons
{
    internal static IconInfo PowerExtensionIcon { get; } = new IconInfo("\uE7E8");

    internal static IconInfo PowerModeBandIcon { get; } = ThemedBandIcon("PowerMode");

    internal static IconInfo PowerPlanBandIcon { get; } = ThemedBandIcon("PowerPlan");

    private static IconInfo ThemedBandIcon(string assetName) =>
        IconHelpers.FromRelativePaths(
            $"Assets\\{assetName}.light.svg",
            $"Assets\\{assetName}.dark.svg");

    internal static IconInfo EfficiencyIcon { get; } = new IconInfo("\uE8BE");

    internal static IconInfo BalancedIcon { get; } = new IconInfo("\uE9F5");

    internal static IconInfo PerformanceIcon { get; } = new IconInfo("\uE945");

    internal static IconInfo UnknownIcon { get; } = new IconInfo("\uE783");

    internal static IconInfo EnergySaverIcon { get; } = new IconInfo("\uEC0A");

    internal static IconInfo PowerPlanSaverIcon { get; } = new IconInfo("\uEC48");

    internal static IconInfo PowerPlanBalancedIcon { get; } = new IconInfo("\uEC49");

    internal static IconInfo PowerPlanPerformanceIcon { get; } = new IconInfo("\uEC4A");

    internal static IconInfo PowerPlanUltimatePerformanceIcon { get; } = new IconInfo("\uF42F");

    internal static IconInfo Glyph(UserPowerMode mode) => mode switch
    {
        UserPowerMode.BestEfficiency => EfficiencyIcon,
        UserPowerMode.Balanced => BalancedIcon,
        UserPowerMode.BestPerformance => PerformanceIcon,
        _ => UnknownIcon,
    };

    internal static IconInfo PlanGlyph(Guid schemeGuid)
    {
        if (schemeGuid == PowerPlanGuids.PowerSaver)
        {
            return PowerPlanSaverIcon;
        }

        if (schemeGuid == PowerPlanGuids.Balanced)
        {
            return PowerPlanBalancedIcon;
        }

        if (schemeGuid == PowerPlanGuids.HighPerformance)
        {
            return PowerPlanPerformanceIcon;
        }

        if (schemeGuid == PowerPlanGuids.UltimatePerformance)
        {
            return PowerPlanUltimatePerformanceIcon;
        }

        return PowerPlanBandIcon;
    }

    internal static IconInfo PlanGlyph(PowerPlanSnapshot snapshot) =>
        snapshot.ActivePlan is { } activePlan
            ? PlanGlyph(activePlan.SchemeGuid)
            : PowerPlanBandIcon;
}
