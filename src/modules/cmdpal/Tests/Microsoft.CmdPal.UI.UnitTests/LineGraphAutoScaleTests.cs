// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls.Graphs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class LineGraphAutoScaleTests
{
    private static readonly DateTimeOffset Origin = new(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan History = TimeSpan.FromSeconds(60);

    [TestMethod]
    public void BothSeries_ShareTheLargestVisibleValueWithHeadroom()
    {
        var upload = Create([new(Origin, 0), new(Origin.AddSeconds(1), 1800)]);
        var download = Create([new(Origin, 0), new(Origin.AddSeconds(1), 8000)]);

        Assert.AreEqual(10_000d, Maximum([upload, download], 1));
        Assert.AreEqual(1800d, upload.Samples[^1].Value);
        Assert.AreEqual(8000d, download.Samples[^1].Value);
    }

    [TestMethod]
    public void ReadoutOnlySeries_DoesNotChangeTheVisibleScale()
    {
        var plotted = Create([new(Origin, 1800)]);
        var readout = Create([new(Origin, 1_000_000)]);

        Assert.AreEqual(2000d, LineGraphAutoScale.GetMaximum([plotted, readout], Origin, History, 0, 1000, readoutOnly: [false, true]));
        Assert.AreEqual(1000d, LineGraphAutoScale.GetMaximum([readout], Origin, History, 0, 1000, readoutOnly: [true]));
        Assert.IsTrue(readout.TryGetValue(Origin, out var value, out _));
        Assert.AreEqual(1_000_000d, value);
    }

    [TestMethod]
    public void RetainedPeak_StopsAffectingScaleWhenItsVisibleSegmentLeavesTheWindow()
    {
        var presentation = Create([new(Origin, 8000), new(Origin.AddSeconds(1), 100), new(Origin.AddSeconds(2), 100)]);

        Assert.AreEqual(10_000d, Maximum([presentation], 60));
        Assert.AreEqual(5000d, Maximum([presentation], 60.5));
        Assert.AreEqual(1000d, Maximum([presentation], 61));
        Assert.AreEqual(1000d, Maximum([presentation], 120));
        Assert.AreEqual(8000d, presentation.Samples[0].Value);
    }

    [TestMethod]
    public void FuturePeak_ContributesOnlyItsRenderedBoundaryValue()
    {
        var presentation = Create([new(Origin, 0), new(Origin.AddSeconds(1), 8000)]);

        Assert.AreEqual(1000d, Maximum([presentation], -1));
        Assert.AreEqual(1000d, Maximum([presentation], 0));
        Assert.AreEqual(5000d, Maximum([presentation], 0.5));
        Assert.AreEqual(10_000d, Maximum([presentation], 1));
    }

    [TestMethod]
    public void LatePeak_WaitsForTheStepInsteadOfInterpolatingIntoTheWindow()
    {
        var initial = new LineGraphPresentation(null, [new(Origin, 0)], 0, 1000, 0.2, Origin, History);
        var late = new LineGraphPresentation(initial, [new(Origin, 0), new(Origin.AddSeconds(1), 8000)], 0, 1000, 0.2, Origin.AddSeconds(2), History);

        Assert.IsTrue(late.IsStep(0));
        Assert.AreEqual(1000d, Maximum([late], 0.99));
        Assert.AreEqual(10_000d, Maximum([late], 1));
    }

    [TestMethod]
    public void EmptyZeroAndBelowMinimumValues_KeepTheConfiguredFloor()
    {
        Assert.AreEqual(1000d, Maximum([], 1));
        Assert.AreEqual(1000d, Maximum([Create([new(Origin, 0)])], 1));
        Assert.AreEqual(1000d, Maximum([Create([new(Origin, -100)])], 1));
        Assert.AreEqual(1024d, LineGraphAutoScale.GetMaximum([], DateTimeOffset.MinValue, History, 0, 1024, valueDivisors: [1, 1024]));
    }

    [TestMethod]
    public void SuppliedDivisors_RoundInDisplayUnitsAndShrinkToTheFloor()
    {
        var presentation = Create([new(Origin, 1200), new(Origin.AddSeconds(1), 0)]);

        Assert.AreEqual(2048d, LineGraphAutoScale.GetMaximum([presentation], Origin.AddSeconds(60), History, 0, 1024, valueDivisors: [1, 1024]));
        Assert.AreEqual(1024d, LineGraphAutoScale.GetMaximum([presentation], Origin.AddSeconds(61), History, 0, 1024, valueDivisors: [1, 1024]));
    }

    [TestMethod]
    [DataRow(0.003d, 0.005d)]
    [DataRow(90d, 120d)]
    public void IrregularDivisors_UseGenericRounding(double peak, double expectedMaximum)
    {
        var presentation = Create([new(Origin, peak)]);

        Assert.AreEqual(expectedMaximum, LineGraphAutoScale.GetMaximum([presentation], Origin, History, 0, 0.001, valueDivisors: [0.001, 1, 60]), 0.000001);
    }

    [TestMethod]
    public void NonzeroMinimum_RemainsFixedWhileTheSpanGrows()
    {
        var presentation = new LineGraphPresentation(null, [new(Origin, 8000)], -1000, 0, 0, Origin, History);

        Assert.AreEqual(9000d, LineGraphAutoScale.GetMaximum([presentation], Origin, History, -1000, 0));
    }

    [TestMethod]
    [DataRow(1d)]
    [DataRow(1024d)]
    [DataRow(double.Epsilon)]
    public void ExtremeFinitePeak_DoesNotOverflowTheDisplayRange(double divisor)
    {
        var presentation = Create([new(Origin, double.MaxValue)]);

        var maximum = LineGraphAutoScale.GetMaximum([presentation], Origin, History, 0, 1000, valueDivisors: [divisor]);

        Assert.IsTrue(double.IsFinite(maximum));
        Assert.AreEqual(double.MaxValue, maximum);
    }

    private static LineGraphPresentation Create(LineGraphPoint[] samples)
        => new(null, samples, 0, 1000, 0, Origin, History);

    private static double Maximum(LineGraphPresentation[] presentations, double seconds)
        => LineGraphAutoScale.GetMaximum(presentations, Origin.AddSeconds(seconds), History, 0, 1000);
}
