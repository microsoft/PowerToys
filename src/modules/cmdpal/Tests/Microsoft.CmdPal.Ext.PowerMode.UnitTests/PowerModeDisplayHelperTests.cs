// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.PowerMode.Helpers;
using Microsoft.CmdPal.Ext.PowerMode.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PowerMode.UnitTests;

[TestClass]
public sealed class PowerModeDisplayHelperTests
{
    [TestMethod]
    public void GetPowerSourceLabel_Desktop_ShowsNoBattery()
    {
        var snapshot = CreateSnapshot(PowerSourceKind.NoBattery, hasBattery: false, isOnAcPower: true, isCharging: false);
        Assert.AreEqual(Resources.power_mode_no_battery, PowerModeDisplayHelper.GetPowerSourceLabel(snapshot));
    }

    [TestMethod]
    public void GetPowerSourceLabel_OnBattery_ShowsOnBattery()
    {
        var snapshot = CreateSnapshot(PowerSourceKind.OnBattery, hasBattery: true, isOnAcPower: false, isCharging: false);
        Assert.AreEqual(Resources.power_mode_on_battery, PowerModeDisplayHelper.GetPowerSourceLabel(snapshot));
    }

    [TestMethod]
    public void GetPowerSourceLabel_PluggedInCharging_ShowsChargingLabel()
    {
        var snapshot = CreateSnapshot(PowerSourceKind.PluggedIn, hasBattery: true, isOnAcPower: true, isCharging: true);
        Assert.AreEqual(Resources.power_mode_plugged_in_charging, PowerModeDisplayHelper.GetPowerSourceLabel(snapshot));
    }

    [TestMethod]
    public void GetBatteryStatusLabel_Desktop_ShowsNoBattery()
    {
        var snapshot = CreateSnapshot(PowerSourceKind.NoBattery, hasBattery: false, isOnAcPower: true, isCharging: false);
        Assert.AreEqual(Resources.power_mode_battery_nonexistent, PowerModeDisplayHelper.GetBatteryStatusLabel(snapshot));
    }

    [TestMethod]
    public void GetBatteryStatusLabel_Charging_ShowsCharging()
    {
        var snapshot = CreateSnapshot(PowerSourceKind.PluggedIn, hasBattery: true, isOnAcPower: true, isCharging: true);
        Assert.AreEqual(Resources.power_mode_battery_charging, PowerModeDisplayHelper.GetBatteryStatusLabel(snapshot));
    }

    [TestMethod]
    public void GetBatteryStatusLabel_OnBattery_ShowsNotCharging()
    {
        var snapshot = CreateSnapshot(PowerSourceKind.OnBattery, hasBattery: true, isOnAcPower: false, isCharging: false);
        Assert.AreEqual(Resources.power_mode_battery_not_charging, PowerModeDisplayHelper.GetBatteryStatusLabel(snapshot));
    }

    [TestMethod]
    public void GetSetModeSubtitle_WhenActiveMode_ShowsCurrent()
    {
        var snapshot = CreateSnapshot(PowerSourceKind.OnBattery, hasBattery: true, isOnAcPower: false, isCharging: false);
        var subtitle = PowerModeDisplayHelper.GetSetModeSubtitle(UserPowerMode.Balanced, snapshot);
        Assert.AreEqual(Resources.power_list_current, subtitle);
    }

    [TestMethod]
    public void GetSetModeSubtitle_WhenInactiveMode_ShowsEmpty()
    {
        var snapshot = CreateSnapshot(PowerSourceKind.OnBattery, hasBattery: true, isOnAcPower: false, isCharging: false);
        var subtitle = PowerModeDisplayHelper.GetSetModeSubtitle(UserPowerMode.BestPerformance, snapshot);
        Assert.AreEqual(string.Empty, subtitle);
    }

    [TestMethod]
    public void GetStatusSubtitle_WhenUnsupported_ShowsNotSupportedMessage()
    {
        var snapshot = CreateSnapshot(PowerSourceKind.NoBattery, hasBattery: false, isOnAcPower: true, isCharging: false, canReadUserMode: false);
        var subtitle = PowerModeDisplayHelper.GetStatusSubtitle(snapshot);
        Assert.AreEqual(Resources.power_mode_not_supported, subtitle);
    }

    [TestMethod]
    public void GetStatusSubtitle_WhenSupported_ShowsCurrentMode()
    {
        var snapshot = CreateSnapshot(PowerSourceKind.PluggedIn, hasBattery: true, isOnAcPower: true, isCharging: true);
        var subtitle = PowerModeDisplayHelper.GetStatusSubtitle(snapshot);
        Assert.AreEqual(Resources.power_mode_balanced, subtitle);
    }

    private static PowerModeSnapshot CreateSnapshot(
        PowerSourceKind powerSourceKind,
        bool hasBattery,
        bool isOnAcPower,
        bool isCharging,
        bool canReadUserMode = true) =>
        new(
            UserPowerMode.Balanced,
            null,
            powerSourceKind,
            hasBattery,
            isOnAcPower,
            isCharging,
            canReadUserMode);
}
