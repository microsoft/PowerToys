// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Power.Properties;

namespace Microsoft.CmdPal.Ext.Power.Helpers;

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
        if (snapshot.CanReadUserMode && snapshot.UserMode == mode)
        {
            return Resources.power_list_current;
        }

        return string.Empty;
    }

    internal static string GetEnergySaverStatusLabel(EnergySaverSnapshot snapshot)
    {
        if (!snapshot.CanReadStatus)
        {
            return Resources.power_mode_energy_saver_unknown;
        }

        return snapshot.State switch
        {
            ResolvedEnergySaverState.On => Resources.power_mode_energy_saver_on,
            ResolvedEnergySaverState.Off => Resources.power_mode_energy_saver_off,
            ResolvedEnergySaverState.NotAvailable => Resources.power_mode_energy_saver_not_available,
            _ => Resources.power_mode_energy_saver_unknown,
        };
    }
}
