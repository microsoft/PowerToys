// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Power;
using Microsoft.CmdPal.Ext.Power.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Power.UnitTests;

[TestClass]
public sealed class IconsTests
{
    [TestMethod]
    public void PlanGlyph_KnownPlans_UsePlanSpecificIcons()
    {
        Assert.AreSame(Icons.PowerPlanSaverIcon, Icons.PlanGlyph(PowerPlanGuids.PowerSaver));
        Assert.AreSame(Icons.PowerPlanBalancedIcon, Icons.PlanGlyph(PowerPlanGuids.Balanced));
        Assert.AreSame(Icons.PowerPlanPerformanceIcon, Icons.PlanGlyph(PowerPlanGuids.HighPerformance));
        Assert.AreSame(Icons.PowerPlanUltimatePerformanceIcon, Icons.PlanGlyph(PowerPlanGuids.UltimatePerformance));
    }

    [TestMethod]
    public void PlanGlyph_UnknownPlan_UsesPowerPlanBandIcon()
    {
        var unknown = System.Guid.Parse("11111111-1111-1111-1111-111111111111");
        Assert.AreSame(Icons.PowerPlanBandIcon, Icons.PlanGlyph(unknown));
    }
}
