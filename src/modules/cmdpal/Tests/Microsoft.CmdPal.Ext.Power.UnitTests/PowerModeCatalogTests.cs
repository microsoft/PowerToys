// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Power.Enumerations;
using Microsoft.CmdPal.Ext.Power.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Power.UnitTests;

[TestClass]
public sealed class PowerModeCatalogTests
{
    [TestMethod]
    public void FromGuid_MapsKnownModes()
    {
        Assert.AreEqual(UserPowerMode.BestEfficiency, PowerModeCatalog.FromGuid(PowerModeCatalog.BestEfficiency.Guid));
        Assert.AreEqual(UserPowerMode.Balanced, PowerModeCatalog.FromGuid(PowerModeCatalog.Balanced.Guid));
        Assert.AreEqual(UserPowerMode.BestPerformance, PowerModeCatalog.FromGuid(PowerModeCatalog.BestPerformance.Guid));
    }

    [TestMethod]
    public void FromGuid_UnknownGuid_ReturnsUnknown()
    {
        Assert.AreEqual(UserPowerMode.Unknown, PowerModeCatalog.FromGuid(Guid.Parse("11111111-1111-1111-1111-111111111111")));
    }

    [TestMethod]
    public void ToGuid_RoundTripsKnownModes()
    {
        Assert.AreEqual(PowerModeCatalog.BestEfficiency.Guid, PowerModeCatalog.ToGuid(UserPowerMode.BestEfficiency));
        Assert.AreEqual(PowerModeCatalog.Balanced.Guid, PowerModeCatalog.ToGuid(UserPowerMode.Balanced));
        Assert.AreEqual(PowerModeCatalog.BestPerformance.Guid, PowerModeCatalog.ToGuid(UserPowerMode.BestPerformance));
    }
}
