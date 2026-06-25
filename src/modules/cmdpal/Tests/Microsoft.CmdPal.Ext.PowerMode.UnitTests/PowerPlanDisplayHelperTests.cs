// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Power.Helpers;
using Microsoft.CmdPal.Ext.Power.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Power.UnitTests;

[TestClass]
public sealed class PowerPlanDisplayHelperTests
{
    [TestMethod]
    public void GetStatusSubtitle_WhenUnavailable_ShowsUnavailableMessage()
    {
        var snapshot = CreateSnapshot(canReadPlans: false);
        Assert.AreEqual(Resources.power_plan_status_unavailable, PowerPlanDisplayHelper.GetStatusSubtitle(snapshot));
    }

    [TestMethod]
    public void GetStatusSubtitle_WhenActivePlanKnown_ShowsActivePlanName()
    {
        var activePlan = new PowerPlanInfo(PowerPlanGuids.Balanced, "Balanced", Resources.power_plan_desc_balanced);
        var snapshot = CreateSnapshot(activePlan: activePlan);
        var subtitle = PowerPlanDisplayHelper.GetStatusSubtitle(snapshot);
        StringAssert.Contains(subtitle, "Balanced");
    }

    [TestMethod]
    public void GetPlanItemSubtitle_WhenActivePlan_ShowsCurrentPlan()
    {
        var activePlan = new PowerPlanInfo(PowerPlanGuids.Balanced, "Balanced", Resources.power_plan_desc_balanced);
        var snapshot = CreateSnapshot(activePlan: activePlan);
        var subtitle = PowerPlanDisplayHelper.GetPlanItemSubtitle(activePlan, snapshot);
        Assert.AreEqual(Resources.power_plan_current, subtitle);
    }

    [TestMethod]
    public void GetPlanItemSubtitle_WhenNotActive_ShowsEmpty()
    {
        var activePlan = new PowerPlanInfo(PowerPlanGuids.Balanced, "Balanced", Resources.power_plan_desc_balanced);
        var otherPlan = new PowerPlanInfo(PowerPlanGuids.HighPerformance, "High performance", Resources.power_plan_desc_high_performance);
        var snapshot = CreateSnapshot(activePlan: activePlan);
        var subtitle = PowerPlanDisplayHelper.GetPlanItemSubtitle(otherPlan, snapshot);
        Assert.AreEqual(string.Empty, subtitle);
    }

    [TestMethod]
    public void GetPlanTitle_UltimatePerformance_AppendsPlusSuffix()
    {
        var plan = new PowerPlanInfo(PowerPlanGuids.UltimatePerformance, "Ultimate Performance", string.Empty);
        Assert.AreEqual("Ultimate Performance+", PowerPlanDisplayHelper.GetPlanTitle(plan));
    }

    [TestMethod]
    public void GetPlanTitle_Balanced_DoesNotAppendPlusSuffix()
    {
        var plan = new PowerPlanInfo(PowerPlanGuids.Balanced, "Balanced", string.Empty);
        Assert.AreEqual("Balanced", PowerPlanDisplayHelper.GetPlanTitle(plan));
    }

    private static PowerPlanSnapshot CreateSnapshot(
        bool canReadPlans = true,
        PowerPlanInfo activePlan = default,
        IReadOnlyList<PowerPlanInfo> availablePlans = null)
    {
        availablePlans ??=
        [
            new PowerPlanInfo(PowerPlanGuids.Balanced, "Balanced", Resources.power_plan_desc_balanced),
            new PowerPlanInfo(PowerPlanGuids.HighPerformance, "High performance", Resources.power_plan_desc_high_performance),
        ];

        return new PowerPlanSnapshot(
            activePlan.SchemeGuid == Guid.Empty ? null : activePlan,
            availablePlans,
            canReadPlans,
            canReadPlans);
    }
}
