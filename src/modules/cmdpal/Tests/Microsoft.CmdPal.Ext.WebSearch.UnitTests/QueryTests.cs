// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.WebSearch.UnitTests;

[TestClass]
public class QueryTests : CommandPaletteUnitTestBase
{
    [TestMethod]
    public void ValidateSettingsManager()
    {
        // Setup
        var settings = Settings.CreateDefaultSettings();

        // Assert
        Assert.IsNotNull(settings);
        Assert.IsFalse(settings.GlobalIfURI);
        Assert.AreEqual("None", settings.ShowHistory);
    }

    [TestMethod]
    public void ValidateHistoryEnabledSettings()
    {
        // Setup
        var settings = Settings.CreateHistoryEnabledSettings();

        // Assert
        Assert.IsNotNull(settings);
        Assert.AreEqual("5", settings.ShowHistory);
    }

    [TestMethod]
    public void ValidateGlobalUriSettings()
    {
        // Setup
        var settings = Settings.CreateGlobalUriSettings();

        // Assert
        Assert.IsNotNull(settings);
        Assert.IsTrue(settings.GlobalIfURI);
    }

    [TestMethod]
    public void ValidateSettingsCombination()
    {
        // Setup
        var settings = new Settings(globalIfURI: true, showHistory: "10");

        // Assert
        Assert.IsTrue(settings.GlobalIfURI);
        Assert.AreEqual("10", settings.ShowHistory);
    }
}
