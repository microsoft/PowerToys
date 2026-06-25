// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.PowerMode.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Win32.System.Power;

namespace Microsoft.CmdPal.Ext.PowerMode.UnitTests;

[TestClass]
public sealed class PowerSourceHelperTests
{
    [TestMethod]
    public void GetPowerSourceKind_NoBattery_ReturnsNoBattery()
    {
        var status = new SYSTEM_POWER_STATUS
        {
            BatteryFlag = 0x80,
            ACLineStatus = 1,
        };

        Assert.AreEqual(PowerSourceKind.NoBattery, PowerSourceHelper.GetPowerSourceKind(in status));
        Assert.IsFalse(PowerSourceHelper.HasBattery(in status));
        Assert.IsTrue(PowerSourceHelper.UseAcPowerProfile(PowerSourceKind.NoBattery));
    }

    [TestMethod]
    public void GetPowerSourceKind_OnBattery_ReturnsOnBattery()
    {
        var status = new SYSTEM_POWER_STATUS
        {
            BatteryFlag = 0,
            ACLineStatus = 0,
        };

        Assert.AreEqual(PowerSourceKind.OnBattery, PowerSourceHelper.GetPowerSourceKind(in status));
        Assert.IsTrue(PowerSourceHelper.HasBattery(in status));
        Assert.IsFalse(PowerSourceHelper.UseAcPowerProfile(PowerSourceKind.OnBattery));
    }

    [TestMethod]
    public void GetPowerSourceKind_PluggedIn_ReturnsPluggedIn()
    {
        var status = new SYSTEM_POWER_STATUS
        {
            BatteryFlag = 0,
            ACLineStatus = 1,
        };

        Assert.AreEqual(PowerSourceKind.PluggedIn, PowerSourceHelper.GetPowerSourceKind(in status));
        Assert.IsTrue(PowerSourceHelper.HasBattery(in status));
        Assert.IsTrue(PowerSourceHelper.IsOnAcPower(in status));
        Assert.IsTrue(PowerSourceHelper.UseAcPowerProfile(PowerSourceKind.PluggedIn));
    }

    [TestMethod]
    public void IsCharging_WhenPluggedInAndChargingFlagSet_ReturnsTrue()
    {
        var status = new SYSTEM_POWER_STATUS
        {
            BatteryFlag = 0x08,
            ACLineStatus = 1,
        };

        Assert.IsTrue(PowerSourceHelper.IsCharging(in status));
    }
}
