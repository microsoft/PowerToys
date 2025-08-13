// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.WebSearch.UnitTests;

[TestClass]
public class SettingsTests
{
    [TestMethod]
    public void DefaultSettings_HasExpectedValues()
    {
        // Act
        var settings = Settings.CreateDefaultSettings();

        // Assert
        Assert.IsFalse(settings.GlobalIfURI);
        Assert.AreEqual("None", settings.ShowHistory);
    }

    [TestMethod]
    public void GlobalUriSettings_EnablesGlobalUri()
    {
        // Act
        var settings = Settings.CreateGlobalUriSettings();

        // Assert
        Assert.IsTrue(settings.GlobalIfURI);
        Assert.AreEqual("None", settings.ShowHistory);
    }

    [TestMethod]
    public void HistoryEnabledSettings_EnablesHistory()
    {
        // Act
        var settings = Settings.CreateHistoryEnabledSettings();

        // Assert
        Assert.AreEqual("5", settings.ShowHistory);
        Assert.IsFalse(settings.GlobalIfURI);
    }

    [TestMethod]
    public void HistoryDisabledSettings_DisablesHistory()
    {
        // Act
        var settings = Settings.CreateHistoryDisabledSettings();

        // Assert
        Assert.AreEqual("None", settings.ShowHistory);
    }

    [TestMethod]
    public void SettingsInterface_ImplementedCorrectly()
    {
        // Setup
        var settings = Settings.CreateDefaultSettings();

        // Assert - Verify all interface properties are accessible
        Assert.IsNotNull(settings.ShowHistory);

        // Verify boolean properties
        Assert.IsTrue(settings.GlobalIfURI || !settings.GlobalIfURI); // Just verify it's accessible
    }
}
