// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.PowerMode.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Win32.System.Power;

namespace Microsoft.CmdPal.Ext.PowerMode.UnitTests;

[TestClass]
public sealed class PowerModeMapperTests
{
    [TestMethod]
    public void FromGuid_MapsKnownModes()
    {
        Assert.AreEqual(UserPowerMode.BestEfficiency, PowerModeMapper.FromGuid(PowerModeGuids.BestEfficiency));
        Assert.AreEqual(UserPowerMode.Balanced, PowerModeMapper.FromGuid(PowerModeGuids.Balanced));
        Assert.AreEqual(UserPowerMode.BestPerformance, PowerModeMapper.FromGuid(PowerModeGuids.BestPerformance));
    }

    [TestMethod]
    public void FromGuid_UnknownGuid_ReturnsUnknown()
    {
        Assert.AreEqual(UserPowerMode.Unknown, PowerModeMapper.FromGuid(Guid.Parse("11111111-1111-1111-1111-111111111111")));
    }

    [TestMethod]
    public void ToGuid_RoundTripsKnownModes()
    {
        Assert.AreEqual(PowerModeGuids.BestEfficiency, PowerModeMapper.ToGuid(UserPowerMode.BestEfficiency));
        Assert.AreEqual(PowerModeGuids.Balanced, PowerModeMapper.ToGuid(UserPowerMode.Balanced));
        Assert.AreEqual(PowerModeGuids.BestPerformance, PowerModeMapper.ToGuid(UserPowerMode.BestPerformance));
    }
}
