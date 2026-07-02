// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Power.Classes;
using Microsoft.CmdPal.Ext.Power.Enumerations;
using Microsoft.CmdPal.Ext.Power.Properties;

namespace Microsoft.CmdPal.Ext.Power.Helpers;

internal static class PowerModeCatalog
{
    internal static readonly PowerModeDefinition BestEfficiency = new(
        UserPowerMode.BestEfficiency,
        new Guid("961cc777-2547-4f9d-8174-7d86181b8a7a"),
        Resources.power_mode_best_efficiency,
        Resources.power_mode_best_efficiency_short);

    internal static readonly PowerModeDefinition Balanced = new(
        UserPowerMode.Balanced,
        Guid.Empty,
        Resources.power_mode_balanced,
        Resources.power_mode_balanced_short);

    internal static readonly PowerModeDefinition BestPerformance = new(
        UserPowerMode.BestPerformance,
        new Guid("ded574b5-45a0-4f42-8737-46345c09c238"),
        Resources.power_mode_best_performance,
        Resources.power_mode_best_performance_short);

    internal static IEnumerable<PowerModeDefinition> All =>
    [
        BestEfficiency,
        Balanced,
        BestPerformance,
    ];

    internal static UserPowerMode FromGuid(Guid guid)
    {
        foreach (var definition in All)
        {
            if (definition.Guid == guid)
            {
                return definition.Mode;
            }
        }

        return UserPowerMode.Unknown;
    }

    internal static Guid ToGuid(UserPowerMode mode) => mode switch
    {
        UserPowerMode.BestEfficiency => BestEfficiency.Guid,
        UserPowerMode.Balanced => Balanced.Guid,
        UserPowerMode.BestPerformance => BestPerformance.Guid,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    internal static PowerModeDefinition GetDefinition(UserPowerMode mode) => mode switch
    {
        UserPowerMode.BestEfficiency => BestEfficiency,
        UserPowerMode.Balanced => Balanced,
        UserPowerMode.BestPerformance => BestPerformance,
        _ => BestEfficiency with { Mode = UserPowerMode.Unknown, Label = Resources.power_mode_unknown, ShortLabel = Resources.power_mode_unknown_short },
    };
}
