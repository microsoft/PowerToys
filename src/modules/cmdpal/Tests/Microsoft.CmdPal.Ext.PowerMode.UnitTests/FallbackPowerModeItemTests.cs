// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.PowerMode.Helpers;
using Microsoft.CmdPal.Ext.PowerMode.Pages;
using Microsoft.CmdPal.Ext.PowerMode.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PowerMode.UnitTests;

[TestClass]
public sealed class FallbackPowerModeItemTests
{
    [TestMethod]
    public void UpdateQuery_MatchingTerm_OpensListPage()
    {
        var listPage = CreateListPage();
        var fallback = new FallbackPowerModeItem(listPage);

        fallback.UpdateQuery("power mode");

        Assert.AreEqual(listPage, fallback.Command);
        Assert.AreEqual(Resources.power_mode_fallback_title, fallback.Title);
        Assert.AreEqual(Resources.power_mode_fallback_subtitle, fallback.Subtitle);
    }

    [TestMethod]
    public void UpdateQuery_NonMatchingTerm_ClearsCommand()
    {
        var listPage = CreateListPage();
        var fallback = new FallbackPowerModeItem(listPage);

        fallback.UpdateQuery("clipboard");

        Assert.IsNull(fallback.Command);
        Assert.AreEqual(string.Empty, fallback.Title);
        Assert.AreEqual(string.Empty, fallback.Subtitle);
    }

    private static PowerModeListPage CreateListPage()
    {
        var service = new PowerModeService();
        var energySaverService = new EnergySaverService();
        var dataManager = new PowerModeDataManager(service, energySaverService, () => { });
        return new PowerModeListPage(service, energySaverService, dataManager);
    }
}
