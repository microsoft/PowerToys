// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
}
