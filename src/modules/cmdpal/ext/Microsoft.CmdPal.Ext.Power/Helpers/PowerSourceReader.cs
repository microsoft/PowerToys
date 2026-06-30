// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Power.Enumerations;
using Windows.Win32;
using Windows.Win32.System.Power;

namespace Microsoft.CmdPal.Ext.Power.Helpers;

internal static class PowerSourceReader
{
    private const byte BatteryFlagCharging = 0x08;
    private const byte BatteryFlagNoBattery = 0x80;
    private const byte BatteryFlagUnknown = 0xFF;

    internal static bool TryGetPowerStatus(out SYSTEM_POWER_STATUS status)
    {
        return PInvoke.GetSystemPowerStatus(out status);
    }

    internal static PowerSourceKind GetPowerSourceKind(in SYSTEM_POWER_STATUS status)
    {
        if (!HasBattery(in status))
        {
            return PowerSourceKind.NoBattery;
        }

        return status.ACLineStatus switch
        {
            1 => PowerSourceKind.PluggedIn,
            0 => PowerSourceKind.OnBattery,
            _ => PowerSourceKind.Unknown,
        };
    }

    internal static bool HasBattery(in SYSTEM_POWER_STATUS status) =>
        status.BatteryFlag != BatteryFlagUnknown && (status.BatteryFlag & BatteryFlagNoBattery) == 0;

    internal static bool IsCharging(in SYSTEM_POWER_STATUS status) =>
        HasBattery(in status) && (status.BatteryFlag & BatteryFlagCharging) != 0;

    internal static bool IsOnAcPower(in SYSTEM_POWER_STATUS status) => status.ACLineStatus == 1;

    /// <summary>
    /// Windows stores separate user power modes for AC and DC profiles on battery-capable devices.
    /// Desktops without a battery always use the AC profile.
    /// </summary>
    internal static bool UseAcPowerProfile(PowerSourceKind powerSourceKind) =>
        powerSourceKind is PowerSourceKind.NoBattery or PowerSourceKind.PluggedIn or PowerSourceKind.Unknown;
}
