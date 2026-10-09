// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Win32;
using Windows.Win32.System.Power;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class BatteryStats
{
    // GetSystemPowerStatus returns 255 (0xFF) for percent / time when unknown.
    private const byte BatteryPercentUnknown = 255;
    private const uint BatteryLifeTimeUnknown = 0xFFFFFFFF;

    // BatteryFlag bits.
    private const byte BatteryFlagCharging = 0x08;
    private const byte BatteryFlagNoBattery = 0x80;
    private const byte BatteryFlagUnknown = 0xFF;

    public bool HasBattery { get; set; }

    public bool IsCharging { get; set; }

    public bool IsOnAcPower { get; set; }

    /// <summary>
    /// Charge level in [0, 1], or -1 when unknown.
    /// </summary>
    public float ChargePercent { get; set; } = -1f;

    /// <summary>
    /// Estimated seconds of battery life remaining, or -1 when unknown / charging / on AC.
    /// </summary>
    public int SecondsRemaining { get; set; } = -1;

    public void GetData()
    {
        if (!PInvoke.GetSystemPowerStatus(out SYSTEM_POWER_STATUS status))
        {
            HasBattery = false;
            IsCharging = false;
            IsOnAcPower = false;
            ChargePercent = -1f;
            SecondsRemaining = -1;
            return;
        }

        HasBattery = status.BatteryFlag != BatteryFlagUnknown && (status.BatteryFlag & BatteryFlagNoBattery) == 0;
        IsCharging = HasBattery && (status.BatteryFlag & BatteryFlagCharging) != 0;
        IsOnAcPower = status.ACLineStatus == 1;

        ChargePercent = status.BatteryLifePercent != BatteryPercentUnknown
            ? status.BatteryLifePercent / 100f
            : -1f;

        SecondsRemaining = status.BatteryLifeTime != BatteryLifeTimeUnknown
            ? (int)status.BatteryLifeTime
            : -1;
    }
}
