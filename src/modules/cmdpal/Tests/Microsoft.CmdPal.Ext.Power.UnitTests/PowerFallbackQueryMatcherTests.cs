// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Power.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Power.UnitTests;

[TestClass]
public sealed class PowerFallbackQueryMatcherTests
{
    [TestMethod]
    public void Matches_PowerModeQuery_ReturnsTrue()
    {
        Assert.IsTrue(PowerFallbackQueryMatcher.Matches("power mode"));
    }

    [TestMethod]
    public void Matches_EnergySaverQuery_ReturnsTrue()
    {
        Assert.IsTrue(PowerFallbackQueryMatcher.Matches("energy saver"));
    }

    [TestMethod]
    public void Matches_CatalogLabelQuery_ReturnsTrue()
    {
        Assert.IsTrue(PowerFallbackQueryMatcher.Matches("best performance"));
    }

    [TestMethod]
    public void Matches_SupplementalAlias_ReturnsTrue()
    {
        Assert.IsTrue(PowerFallbackQueryMatcher.Matches("powercfg"));
    }

    [TestMethod]
    public void Matches_UnrelatedQuery_ReturnsFalse()
    {
        Assert.IsFalse(PowerFallbackQueryMatcher.Matches("clipboard"));
    }

    [TestMethod]
    public void Matches_EmptyQuery_ReturnsFalse()
    {
        Assert.IsFalse(PowerFallbackQueryMatcher.Matches(string.Empty));
        Assert.IsFalse(PowerFallbackQueryMatcher.Matches("   "));
    }
}
