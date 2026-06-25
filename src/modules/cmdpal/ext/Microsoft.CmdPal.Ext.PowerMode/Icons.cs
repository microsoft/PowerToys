// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.PowerMode.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerMode;

internal sealed class Icons
{
    internal static IconInfo PowerModeIcon { get; } = new IconInfo("\uE7E8");

    internal static IconInfo EfficiencyIcon { get; } = new IconInfo("\uE8BE");

    internal static IconInfo BalancedIcon { get; } = new IconInfo("\uE9F5");

    internal static IconInfo PerformanceIcon { get; } = new IconInfo("\uE945");

    internal static IconInfo UnknownIcon { get; } = new IconInfo("\uE783");

    internal static IconInfo BatteryUnknownIcon { get; } = new IconInfo("\uEC02");

    internal static IconInfo BatteryDischargingIcon { get; } = new IconInfo("\uEBA0");

    internal static IconInfo BatteryChargingIcon { get; } = new IconInfo("\uEBAB");

    internal static IconInfo EnergySaverIcon { get; } = new IconInfo("\uEC0A");

    internal static IconInfo BatteryStatusGlyph(PowerModeSnapshot snapshot)
    {
        if (!snapshot.HasBattery)
        {
            return BatteryUnknownIcon;
        }

        return snapshot.IsCharging ? BatteryChargingIcon : BatteryDischargingIcon;
    }

    internal static IconInfo Glyph(UserPowerMode mode) => mode switch
    {
        UserPowerMode.BestEfficiency => EfficiencyIcon,
        UserPowerMode.Balanced => BalancedIcon,
        UserPowerMode.BestPerformance => PerformanceIcon,
        _ => UnknownIcon,
    };
}
