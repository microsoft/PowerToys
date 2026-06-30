// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Power.Enumerations;
using Microsoft.CmdPal.Ext.Power.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Win32.System.Power;

namespace Microsoft.CmdPal.Ext.Power.UnitTests;

[TestClass]
public sealed class PowerSourceReaderTests
{
    [TestMethod]
    public void GetPowerSourceKind_NoBattery_ReturnsNoBattery()
    {
        var status = new SYSTEM_POWER_STATUS
        {
            BatteryFlag = 0x80,
            ACLineStatus = 1,
        };

        Assert.AreEqual(PowerSourceKind.NoBattery, PowerSourceReader.GetPowerSourceKind(in status));
        Assert.IsFalse(PowerSourceReader.HasBattery(in status));
        Assert.IsTrue(PowerSourceReader.UseAcPowerProfile(PowerSourceKind.NoBattery));
    }

    [TestMethod]
    public void GetPowerSourceKind_OnBattery_ReturnsOnBattery()
    {
        var status = new SYSTEM_POWER_STATUS
        {
            BatteryFlag = 0x01,
            ACLineStatus = 0,
        };

        Assert.AreEqual(PowerSourceKind.OnBattery, PowerSourceReader.GetPowerSourceKind(in status));
        Assert.IsTrue(PowerSourceReader.HasBattery(in status));
        Assert.IsFalse(PowerSourceReader.UseAcPowerProfile(PowerSourceKind.OnBattery));
    }

    [TestMethod]
    public void GetPowerSourceKind_PluggedIn_ReturnsPluggedIn()
    {
        var status = new SYSTEM_POWER_STATUS
        {
            BatteryFlag = 0x01,
            ACLineStatus = 1,
        };

        Assert.AreEqual(PowerSourceKind.PluggedIn, PowerSourceReader.GetPowerSourceKind(in status));
        Assert.IsTrue(PowerSourceReader.HasBattery(in status));
        Assert.IsTrue(PowerSourceReader.IsOnAcPower(in status));
        Assert.IsTrue(PowerSourceReader.UseAcPowerProfile(PowerSourceKind.PluggedIn));
    }

    [TestMethod]
    public void IsCharging_WhenChargingFlagSet_ReturnsTrue()
    {
        var status = new SYSTEM_POWER_STATUS
        {
            BatteryFlag = 0x09,
            ACLineStatus = 1,
        };

        Assert.IsTrue(PowerSourceReader.IsCharging(in status));
    }
}
