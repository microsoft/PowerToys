// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Cli.Commands;

namespace PowerDisplay.Cli.UnitTests;

[TestClass]
public class AdjustCommandInputsTests
{
    [TestMethod]
    public void CountSelectedSettings_CountsAcrossThresholds()
    {
        Assert.AreEqual(0, AdjustCommand.CountSelectedSettings(new AdjustCommandInputs()));
        Assert.AreEqual(1, AdjustCommand.CountSelectedSettings(new AdjustCommandInputs { Brightness = true }));
        Assert.AreEqual(2, AdjustCommand.CountSelectedSettings(new AdjustCommandInputs { Brightness = true, Volume = true }));
        Assert.AreEqual(3, AdjustCommand.CountSelectedSettings(new AdjustCommandInputs { Brightness = true, Contrast = true, Volume = true }));
    }
}
