// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Power.Helpers;
using Microsoft.CmdPal.Ext.Power.Pages;
using Microsoft.CmdPal.Ext.Power.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Power.UnitTests;

[TestClass]
public sealed class FallbackPowerItemTests
{
    [TestMethod]
    public void UpdateQuery_MatchingTerm_OpensListPage()
    {
        var listPage = CreateListPage();
        var fallback = new FallbackPowerItem(listPage);

        fallback.UpdateQuery("power mode");

        Assert.AreEqual(listPage, fallback.Command);
        Assert.AreEqual(Resources.power_fallback_title, fallback.Title);
        Assert.AreEqual(Resources.power_fallback_subtitle, fallback.Subtitle);
    }

    [TestMethod]
    public void UpdateQuery_NonMatchingTerm_ClearsCommand()
    {
        var listPage = CreateListPage();
        var fallback = new FallbackPowerItem(listPage);

        fallback.UpdateQuery("clipboard");

        Assert.IsNull(fallback.Command);
        Assert.AreEqual(string.Empty, fallback.Title);
        Assert.AreEqual(string.Empty, fallback.Subtitle);
    }

    private static PowerListPage CreateListPage()
    {
        var service = new PowerModeService();
        var powerPlanService = new PowerPlanService();
        var dataManager = new PowerModeDataManager(service, () => { });
        var itemBuilder = new PowerListItemBuilder(service, powerPlanService);
        return new PowerListPage(service, powerPlanService, dataManager, itemBuilder);
    }
}
