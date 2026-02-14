// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.CmdPal.Ext.Calc.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Calc.UnitTests;

[TestClass]
public class SettingsManagerTests
{
    [TestMethod]
    public void SettingsManagerInitializationTest()
    {
        // Act
        var settingsManager = new SettingsManager();

        // Assert
        Assert.IsNotNull(settingsManager);
        Assert.IsNotNull(settingsManager.Settings);
    }

    [TestMethod]
    public void SettingsInterfaceTest()
    {
        // Act
        ISettingsInterface settings = new SettingsManager();

        // Assert
        Assert.IsNotNull(settings);
        Assert.IsTrue(settings.TrigUnit == CalculateEngine.TrigMode.Radians);
        Assert.IsFalse(settings.InputUseEnglishFormat);
        Assert.IsFalse(settings.OutputUseEnglishFormat);
        Assert.IsTrue(settings.CloseOnEnter);
        Assert.IsFalse(settings.SaveFallbackResultsToHistory);
        Assert.IsTrue(settings.DeleteHistoryRequiresConfirmation);
        Assert.AreEqual(PrimaryAction.Default, settings.PrimaryAction);
    }

    [TestMethod]
    public void MockSettingsTest()
    {
        // Act
        var settings = new Settings(
            trigUnit: CalculateEngine.TrigMode.Degrees,
            inputUseEnglishFormat: true,
            outputUseEnglishFormat: true,
            closeOnEnter: false);

        // Assert
        Assert.IsNotNull(settings);
        Assert.AreEqual(CalculateEngine.TrigMode.Degrees, settings.TrigUnit);
        Assert.IsTrue(settings.InputUseEnglishFormat);
        Assert.IsTrue(settings.OutputUseEnglishFormat);
        Assert.IsFalse(settings.CloseOnEnter);
    }

    [TestMethod]
    public void HistorySettingsAddRemoveClearTest()
    {
        var settingsManager = new SettingsManager();
        settingsManager.ClearHistory();

        var historyItem = new HistoryItem("1+1", "2", DateTime.UtcNow);
        settingsManager.AddHistoryItem(historyItem);

        Assert.AreEqual(1, settingsManager.HistoryItems.Count);

        settingsManager.RemoveHistoryItem(historyItem.Id);
        Assert.AreEqual(0, settingsManager.HistoryItems.Count);

        settingsManager.AddHistoryItem(new HistoryItem("2+2", "4", DateTime.UtcNow));
        settingsManager.ClearHistory();
        Assert.AreEqual(0, settingsManager.HistoryItems.Count);
    }

    [TestMethod]
    public void HistorySettingsTrimsToCapacityTest()
    {
        var settingsManager = new SettingsManager();
        settingsManager.ClearHistory();

        for (var i = 0; i < 105; i++)
        {
            settingsManager.AddHistoryItem(new HistoryItem($"{i}+{i}", (i + i).ToString(CultureInfo.InvariantCulture), DateTime.UtcNow));
        }

        Assert.AreEqual(100, settingsManager.HistoryItems.Count);
        settingsManager.ClearHistory();
    }
}
