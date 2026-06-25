// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.Power.Helpers;

internal static class PowerModeMapper
{
    internal static UserPowerMode FromGuid(Guid guid)
    {
        if (guid == PowerModeGuids.BestEfficiency)
        {
            return UserPowerMode.BestEfficiency;
        }

        if (guid == PowerModeGuids.Balanced)
        {
            return UserPowerMode.Balanced;
        }

        if (guid == PowerModeGuids.BestPerformance)
        {
            return UserPowerMode.BestPerformance;
        }

        return UserPowerMode.Unknown;
    }

    internal static Guid ToGuid(UserPowerMode mode) => mode switch
    {
        UserPowerMode.BestEfficiency => PowerModeGuids.BestEfficiency,
        UserPowerMode.Balanced => PowerModeGuids.Balanced,
        UserPowerMode.BestPerformance => PowerModeGuids.BestPerformance,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };
}
