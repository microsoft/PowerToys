// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls.Graphs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class LineGraphGridTests
{
    private static readonly DateTimeOffset Origin = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan History = TimeSpan.FromSeconds(60);

    [TestMethod]
    [DataRow(300d, 7.5d)]
    [DataRow(600d, 4d)]
    [DataRow(1000d, 2.5d)]
    public void MeasuredWidth_SelectsRoundTimeDivisions(double width, double seconds)
    {
        Assert.AreEqual(TimeSpan.FromSeconds(seconds), LineGraphGrid.ChooseTimeStep(History, width, 40));
    }

    [TestMethod]
    [DataRow(0d)]
    [DataRow(-1d)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    public void UnmeasuredOrInvalidSize_DefersTheGrid(double size)
    {
        Assert.AreEqual(TimeSpan.Zero, LineGraphGrid.ChooseTimeStep(History, size, 40));
        Assert.AreEqual(TimeSpan.Zero, LineGraphGrid.ChooseTimeStep(History, 600, size));
        Assert.AreEqual(0d, LineGraphGrid.ChooseValueStep(0, 100, size));
    }

    [TestMethod]
    public void ResizeNearAnIntervalBoundary_DoesNotAlternateSteps()
    {
        var timeStep = LineGraphGrid.ChooseTimeStep(History, 870, 40);
        foreach (var width in new[] { 864, 880, 869, 876 })
        {
            timeStep = LineGraphGrid.ChooseTimeStep(History, width, 40, timeStep);
            Assert.AreEqual(TimeSpan.FromSeconds(3), timeStep);
        }

        timeStep = LineGraphGrid.ChooseTimeStep(History, 1200, 40, timeStep);
        Assert.AreEqual(TimeSpan.FromSeconds(2), timeStep);
        Assert.AreEqual(TimeSpan.FromSeconds(4), LineGraphGrid.ChooseTimeStep(History, 600, 40, timeStep));

        var valueStep = LineGraphGrid.ChooseValueStep(0, 100, 160);
        foreach (var height in new[] { 159, 161, 158, 162 })
        {
            valueStep = LineGraphGrid.ChooseValueStep(0, 100, height, previous: valueStep);
            Assert.AreEqual(20d, valueStep);
        }

        Assert.AreEqual(25d, LineGraphGrid.ChooseValueStep(0, 100, 140, previous: valueStep));
        Assert.AreEqual(20d, LineGraphGrid.ChooseValueStep(0, 100, 200, previous: 25));
    }

    [TestMethod]
    [DataRow(800d, 160d)]
    [DataRow(1400d, 180d)]
    [DataRow(300d, 240d)]
    [DataRow(1000d, 96d)]
    [DataRow(350d, 400d)]
    [DataRow(600d, 128d)]
    public void TimeDivisions_FollowTheVerticalCellSize(double width, double height)
    {
        var valueStep = LineGraphGrid.ChooseValueStep(0, 100, height);
        var verticalSpacing = valueStep / 100 * height;
        var timeStep = LineGraphGrid.ChooseTimeStep(History, width, verticalSpacing);
        var horizontalSpacing = timeStep.TotalSeconds / History.TotalSeconds * width;

        Assert.IsTrue(valueStep <= 25);
        Assert.IsTrue(horizontalSpacing / verticalSpacing is >= 0.8 and <= 1.25);
    }

    [TestMethod]
    public void FixedPercentageRange_PreservesBoundsAndUsesMeaningfulValues()
    {
        var step = LineGraphGrid.ChooseValueStep(0, 100, 200);
        CollectionAssert.AreEqual(new double[] { 0, 20, 40, 60, 80, 100 }, LineGraphGrid.GetValueTicks(0, 100, step));
        Assert.AreEqual(25d, LineGraphGrid.ChooseValueStep(0, 100, 80));
        CollectionAssert.AreEqual(new double[] { 0, 25, 50, 75, 100 }, LineGraphGrid.GetValueTicks(0, 100, 25));
    }

    [TestMethod]
    [DataRow(1d)]
    [DataRow(96d)]
    [DataRow(128d)]
    [DataRow(240d)]
    public void ValueGrid_NeverKeepsAFormerStepLargerThanAQuarter(double height)
    {
        Assert.IsTrue(LineGraphGrid.ChooseValueStep(0, 100, height, previous: 50) <= 25);
        Assert.IsTrue(LineGraphGrid.ChooseValueStep(7, 93, height, previous: 50) <= (93d - 7) / 4);
    }

    [TestMethod]
    public void NonRoundAndNegativeBounds_KeepTicksAtValueMultiples()
    {
        CollectionAssert.AreEqual(new double[] { 20, 40, 60, 80 }, LineGraphGrid.GetValueTicks(7, 93, 20));
        CollectionAssert.AreEqual(new double[] { -40, -20, 0, 20, 40 }, LineGraphGrid.GetValueTicks(-43, 47, 20));
    }

    [TestMethod]
    public void DecimalAndBinaryUnits_ProduceRoundDisplayIntervals()
    {
        const double megabit = 1_000_000;
        const double gibibyte = 1024d * 1024 * 1024;

        Assert.AreEqual(10 * megabit, LineGraphGrid.ChooseValueStep(0, 50 * megabit, 200, megabit));
        Assert.AreEqual(5 * gibibyte, LineGraphGrid.ChooseValueStep(0, 32 * gibibyte, 200, gibibyte));
        CollectionAssert.AreEqual(new double[] { 0, 5, 10, 15, 20, 25, 30 }, LineGraphGrid.GetValueTicks(0, 32 * gibibyte, 5 * gibibyte).Select(value => value / gibibyte).ToArray());
    }

    [TestMethod]
    public void Autoscaling_UpdatesTheGridWhenAVisiblePeakLeaves()
    {
        var presentation = new LineGraphPresentation(null, [new(Origin, 8000), new(Origin.AddSeconds(1), 0)], 0, 1000, 0, Origin, History);
        var maximum = LineGraphAutoScale.GetMaximum([presentation], Origin.AddSeconds(60), History, 0, 1000);
        var step = LineGraphGrid.ChooseValueStep(0, maximum, 200, 1000);
        Assert.AreEqual(10_000d, maximum);
        Assert.AreEqual(2000d, step);

        maximum = LineGraphAutoScale.GetMaximum([presentation], Origin.AddSeconds(61), History, 0, 1000);
        step = LineGraphGrid.ChooseValueStep(0, maximum, 200, 1000, step);
        Assert.AreEqual(1000d, maximum);
        Assert.AreEqual(200d, step);
        CollectionAssert.AreEqual(new double[] { 0, 200, 400, 600, 800, 1000 }, LineGraphGrid.GetValueTicks(0, maximum, step));
    }

    [TestMethod]
    public void MovingWindow_KeepsTheSameTimestampAnchors()
    {
        var step = TimeSpan.FromSeconds(10);
        var first = LineGraphGrid.GetTimeTicks(Origin.AddSeconds(61.75), History, step);
        var later = LineGraphGrid.GetTimeTicks(Origin.AddSeconds(62.25), History, step);

        Assert.AreEqual(Origin.UtcTicks, first.First);
        Assert.AreEqual(first, later);
        Assert.AreEqual(8, first.Count);
        Assert.AreEqual(0L, first.First % step.Ticks);

        var scrolled = LineGraphGrid.GetTimeTicks(Origin.AddSeconds(70), History, step);
        Assert.AreEqual(Origin.AddSeconds(10).UtcTicks, scrolled.First);
        Assert.AreEqual(first.Step, scrolled.Step);
    }

    [TestMethod]
    public void PresentationDelay_UsesTheDisplayedWindowIncludingAdjacentClippedTicks()
    {
        var now = Origin.AddSeconds(71);
        var displayTime = now - LineGraphPresentation.DefaultPresentationDelay;
        var ticks = LineGraphGrid.GetTimeTicks(displayTime, History, TimeSpan.FromSeconds(10));

        Assert.AreEqual(Origin.UtcTicks, ticks.First);
        Assert.AreEqual(Origin.AddSeconds(70).UtcTicks, ticks.First + ((ticks.Count - 1) * ticks.Step));
        Assert.IsTrue(ticks.First < (displayTime - History).UtcTicks);
        Assert.IsTrue(ticks.First + ((ticks.Count - 1) * ticks.Step) > displayTime.UtcTicks);
    }

    [TestMethod]
    public void ResizeOrHistoryChange_PreservesSharedTimeAnchors()
    {
        var end = Origin.AddSeconds(63);
        var narrow = LineGraphGrid.GetTimeTicks(end, History, LineGraphGrid.ChooseTimeStep(History, 300, 40));
        var wide = LineGraphGrid.GetTimeTicks(end, History, LineGraphGrid.ChooseTimeStep(History, 1000, 40));
        var longer = LineGraphGrid.GetTimeTicks(end, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(20));

        for (var index = 0; index < narrow.Count; index++)
        {
            var timestamp = narrow.First + (index * narrow.Step);
            Assert.AreEqual(0L, (timestamp - wide.First) % wide.Step);
        }

        Assert.AreEqual(0L, (Origin.UtcTicks - longer.First) % longer.Step);
    }

    [TestMethod]
    public void ShortAndLongHistories_UseSuitableTimeUnits()
    {
        Assert.AreEqual(TimeSpan.FromMilliseconds(40), LineGraphGrid.ChooseTimeStep(TimeSpan.FromSeconds(1), 1000, 40));
        Assert.AreEqual(TimeSpan.FromMinutes(4), LineGraphGrid.ChooseTimeStep(TimeSpan.FromHours(1), 600, 40));
        Assert.AreEqual(TimeSpan.FromHours(2.5), LineGraphGrid.ChooseTimeStep(TimeSpan.FromDays(1), 400, 40));
    }

    [TestMethod]
    public void DateAndDurationLimits_StayBoundedWithoutOverflow()
    {
        foreach (var end in new[] { DateTimeOffset.MinValue, DateTimeOffset.MaxValue })
        {
            var step = LineGraphGrid.ChooseTimeStep(TimeSpan.MaxValue, 1, 40);
            var ticks = LineGraphGrid.GetTimeTicks(end, TimeSpan.MaxValue, step);
            Assert.IsTrue(step > TimeSpan.Zero);
            Assert.IsTrue(ticks.Count > 0 && ticks.Count <= 66);
            Assert.IsTrue(ticks.First >= 0);
            Assert.IsTrue(ticks.First + ((ticks.Count - 1) * ticks.Step) <= DateTimeOffset.MaxValue.Ticks);
        }
    }

    [TestMethod]
    [DataRow(double.Epsilon, double.Epsilon)]
    [DataRow(double.MaxValue, 1d)]
    [DataRow(double.MaxValue, double.Epsilon)]
    public void ExtremeValueRanges_RemainFiniteAndBounded(double maximum, double divisor)
    {
        var step = LineGraphGrid.ChooseValueStep(0, maximum, 1000, divisor);
        Assert.IsTrue(double.IsFinite(step) && step > 0);
        var ticks = LineGraphGrid.GetValueTicks(0, maximum, step);
        Assert.IsTrue(ticks.Length <= 65);
        for (var index = 0; index < ticks.Length; index++)
        {
            Assert.IsTrue(double.IsFinite(ticks[index]) && ticks[index] >= 0 && ticks[index] <= maximum);
            Assert.IsTrue(index == 0 || ticks[index] > ticks[index - 1]);
        }
    }
}
