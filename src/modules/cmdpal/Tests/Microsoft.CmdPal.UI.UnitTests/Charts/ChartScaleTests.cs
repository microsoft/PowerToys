// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests.Charts;

[TestClass]
public class ChartScaleTests
{
    [TestMethod]
    public void FixedPercentRangeUsesQuarterGridLines()
    {
        var range = ChartScale.Compute(0, 100, 3, 42);

        Assert.AreEqual(0, range.Min);
        Assert.AreEqual(100, range.Max);
        CollectionAssert.AreEqual(new[] { 0d, 25, 50, 75, 100 }, range.GetTicks().ToArray());
    }

    [TestMethod]
    [DataRow(0.83, 1.0, 0.2)]
    [DataRow(237.0, 250.0, 50.0)]
    [DataRow(1234.0, 1250.0, 250.0)]
    [DataRow(9.0, 10.0, 2.5)]
    public void OpenMaximumRoundsUpToNiceStep(double dataMax, double expectedMax, double expectedStep)
    {
        var range = ChartScale.Compute(null, null, 0, dataMax);

        Assert.AreEqual(0, range.Min);
        Assert.AreEqual(expectedMax, range.Max, 1e-9);
        Assert.AreEqual(expectedStep, range.Step, 1e-9);
    }

    [TestMethod]
    public void NoDataProducesUnitRange()
    {
        var range = ChartScale.Compute(null, null, double.NaN, double.NaN);

        Assert.AreEqual(0, range.Min);
        Assert.AreEqual(1, range.Max);
    }

    [TestMethod]
    public void NegativeDataExtendsMinimumBelowZero()
    {
        var range = ChartScale.Compute(null, null, -7, 12);

        Assert.IsTrue(range.Min <= -7);
        Assert.IsTrue(range.Max >= 12);
        Assert.AreEqual(0, range.Min % range.Step, 1e-9);
    }

    [TestMethod]
    public void FlatDataStillHasHeight()
    {
        var range = ChartScale.Compute(null, null, 0, 0);

        Assert.IsTrue(range.Max > range.Min);
    }

    [TestMethod]
    public void NormalizeClampsOutsideRange()
    {
        var range = new ChartAxisRange(0, 100, 25);

        Assert.AreEqual(0, range.Normalize(-5));
        Assert.AreEqual(0.5, range.Normalize(50));
        Assert.AreEqual(1, range.Normalize(140));
    }

    [TestMethod]
    [DataRow(25.0, 25.0)]
    [DataRow(31.0, 25.0)]
    [DataRow(0.21, 0.2)]
    [DataRow(700.0, 500.0)]
    [DataRow(800.0, 1000.0)]
    public void NiceNumberRoundsToReadableSteps(double value, double expected)
    {
        Assert.AreEqual(expected, ChartScale.NiceNumber(value, round: true), 1e-9);
    }
}
