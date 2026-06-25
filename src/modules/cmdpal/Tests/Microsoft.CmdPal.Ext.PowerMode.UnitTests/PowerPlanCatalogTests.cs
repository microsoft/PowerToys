// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.PowerMode.Helpers;
using Microsoft.CmdPal.Ext.PowerMode.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PowerMode.UnitTests;

[TestClass]
public sealed class PowerPlanCatalogTests
{
    [TestMethod]
    public void TryGetKnownDescription_Balanced_ReturnsBalancedDescription()
    {
        Assert.IsTrue(PowerPlanCatalog.TryGetKnownDescription(PowerPlanGuids.Balanced, out var description));
        Assert.AreEqual(Resources.power_plan_desc_balanced, description);
    }

    [TestMethod]
    public void TryGetKnownDescription_HighPerformance_ReturnsHighPerformanceDescription()
    {
        Assert.IsTrue(PowerPlanCatalog.TryGetKnownDescription(PowerPlanGuids.HighPerformance, out var description));
        Assert.AreEqual(Resources.power_plan_desc_high_performance, description);
    }

    [TestMethod]
    public void TryGetKnownDescription_PowerSaver_ReturnsPowerSaverDescription()
    {
        Assert.IsTrue(PowerPlanCatalog.TryGetKnownDescription(PowerPlanGuids.PowerSaver, out var description));
        Assert.AreEqual(Resources.power_plan_desc_power_saver, description);
    }

    [TestMethod]
    public void TryGetKnownDescription_UltimatePerformance_ReturnsUltimatePerformanceDescription()
    {
        Assert.IsTrue(PowerPlanCatalog.TryGetKnownDescription(PowerPlanGuids.UltimatePerformance, out var description));
        Assert.AreEqual(Resources.power_plan_desc_ultimate_performance, description);
    }

    [TestMethod]
    public void CompareBySpeed_OrdersKnownPlansSlowestToFastest()
    {
        var plans = new List<PowerPlanInfo>
        {
            new(PowerPlanGuids.UltimatePerformance, "Ultimate Performance", string.Empty),
            new(PowerPlanGuids.Balanced, "Balanced", string.Empty),
            new(PowerPlanGuids.HighPerformance, "High performance", string.Empty),
            new(PowerPlanGuids.PowerSaver, "Power saver", string.Empty),
        };

        PowerPlanCatalog.SortBySpeed(plans);

        Assert.AreEqual(PowerPlanGuids.PowerSaver, plans[0].SchemeGuid);
        Assert.AreEqual(PowerPlanGuids.Balanced, plans[1].SchemeGuid);
        Assert.AreEqual(PowerPlanGuids.HighPerformance, plans[2].SchemeGuid);
        Assert.AreEqual(PowerPlanGuids.UltimatePerformance, plans[3].SchemeGuid);
    }
}
