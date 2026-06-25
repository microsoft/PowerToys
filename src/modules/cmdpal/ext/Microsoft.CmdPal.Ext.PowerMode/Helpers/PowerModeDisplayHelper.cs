// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.PowerMode.Properties;

namespace Microsoft.CmdPal.Ext.PowerMode.Helpers;

internal static class PowerModeDisplayHelper
{
    internal static string GetUserModeLabel(UserPowerMode mode) => mode switch
    {
        UserPowerMode.BestEfficiency => Resources.power_mode_best_efficiency,
        UserPowerMode.Balanced => Resources.power_mode_balanced,
        UserPowerMode.BestPerformance => Resources.power_mode_best_performance,
        _ => Resources.power_mode_unknown,
    };

    internal static string GetUserModeShortLabel(UserPowerMode mode) => mode switch
    {
        UserPowerMode.BestEfficiency => Resources.power_mode_best_efficiency_short,
        UserPowerMode.Balanced => Resources.power_mode_balanced_short,
        UserPowerMode.BestPerformance => Resources.power_mode_best_performance_short,
        _ => Resources.power_mode_unknown_short,
    };

    internal static string GetPowerSourceLabel(PowerModeSnapshot snapshot) => snapshot.PowerSourceKind switch
    {
        PowerSourceKind.NoBattery => Resources.power_mode_no_battery,
        PowerSourceKind.OnBattery => Resources.power_mode_on_battery,
        PowerSourceKind.PluggedIn when snapshot.IsCharging => Resources.power_mode_plugged_in_charging,
        PowerSourceKind.PluggedIn => Resources.power_mode_on_ac,
        _ => Resources.power_mode_power_source_unknown,
    };

    internal static string GetBatteryStatusLabel(PowerModeSnapshot snapshot)
    {
        if (!snapshot.HasBattery)
        {
            return Resources.power_mode_battery_nonexistent;
        }

        return snapshot.IsCharging
            ? Resources.power_mode_battery_charging
            : Resources.power_mode_battery_not_charging;
    }

    internal static string GetStatusSubtitle(PowerModeSnapshot snapshot)
    {
        if (!snapshot.CanReadUserMode)
        {
            return Resources.power_mode_not_supported;
        }

        return GetUserModeLabel(snapshot.UserMode);
    }

    internal static string GetSetModeSubtitle(UserPowerMode mode, PowerModeSnapshot snapshot)
    {
        var modeLabel = GetUserModeLabel(mode);
        if (!snapshot.HasBattery)
        {
            return Resources.power_mode_set_mode_subtitle_desktop + modeLabel;
        }

        return snapshot.UseAcPowerProfile
            ? Resources.power_mode_set_mode_subtitle_plugged_in + modeLabel
            : Resources.power_mode_set_mode_subtitle_on_battery + modeLabel;
    }
}
