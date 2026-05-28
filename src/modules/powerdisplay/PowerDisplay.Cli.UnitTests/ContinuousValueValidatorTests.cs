// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Cli.Errors;
using PowerDisplay.Cli.Resolution;

namespace PowerDisplay.Cli.UnitTests;

[TestClass]
public class ContinuousValueValidatorTests
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(50)]
    [DataRow(100)]
    public void Validate_InRange_ReturnsNull(int value)
    {
        Assert.IsNull(ContinuousValueValidator.Validate("brightness", value));
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(101)]
    [DataRow(500)]
    public void Validate_OutOfRange_ReturnsOutOfRangeError(int value)
    {
        var error = ContinuousValueValidator.Validate("brightness", value);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.OutOfRange, error.Code);
        Assert.AreEqual(CliExitCodes.OutOfRange, error.ExitCode);
        Assert.AreEqual("[0, 100]", error.ExpectedRange);
        StringAssert.Contains(error.Message, "brightness");
        StringAssert.Contains(error.Message, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void Validate_CarriesSettingNameForward()
    {
        var error = ContinuousValueValidator.Validate("contrast", 200);
        Assert.IsNotNull(error);
        Assert.AreEqual("contrast", error.Setting);
    }
}
