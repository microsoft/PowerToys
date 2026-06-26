// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Cli.Commands;

namespace PowerDisplay.Cli.UnitTests;

[TestClass]
public class SetCommandInputsTests
{
    // The count drives the "exactly one setting" validation in Program: 0 -> NoSetting error,
    // 1 -> proceed, >1 -> OnlyOneSetting error. Exercise the 0/1/2 thresholds in one place.
    [TestMethod]
    public void CountSelectedSettings_CountsAcrossThresholds()
    {
        Assert.AreEqual(0, SetCommand.CountSelectedSettings(new SetCommandInputs()));
        Assert.AreEqual(1, SetCommand.CountSelectedSettings(new SetCommandInputs { Brightness = 50 }));
        Assert.AreEqual(2, SetCommand.CountSelectedSettings(new SetCommandInputs { Brightness = 50, Contrast = 70 }));
    }

    [TestMethod]
    public void CountSelectedSettings_AllSeven()
    {
        var inputs = new SetCommandInputs
        {
            Brightness = 0,
            Contrast = 0,
            Volume = 0,
            ColorTemperature = "x",
            InputSource = "x",
            PowerState = "x",
            Orientation = "x",
        };
        Assert.AreEqual(7, SetCommand.CountSelectedSettings(inputs));
    }
}
