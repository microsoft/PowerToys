// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class FontIconSizeCalculatorTests
{
    [TestMethod]
    public void EmptySizeUsesDefaultSize()
    {
        Assert.AreEqual(256, FontIconSizeCalculator.Calculate(Size.Empty, scale: 1, defaultSize: 256));
    }

    [TestMethod]
    public void SizeIsScaledBeforeSelectingLargestDimension()
    {
        Assert.AreEqual(30, FontIconSizeCalculator.Calculate(new Size(16, 20), scale: 1.5, defaultSize: 256));
    }

    [TestMethod]
    [DataRow(0, 0, 1)]
    [DataRow(0.5, 0.5, 1)]
    [DataRow(16, 16, 0)]
    public void NonPositiveTargetUsesMinimumFallback(double width, double height, double scale)
    {
        Assert.AreEqual(8, FontIconSizeCalculator.Calculate(new Size(width, height), scale, defaultSize: 256));
    }
}
