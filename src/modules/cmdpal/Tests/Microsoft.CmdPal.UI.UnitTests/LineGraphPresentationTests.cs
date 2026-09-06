// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls.Graphs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class LineGraphPresentationTests
{
    private static readonly DateTimeOffset Origin = new(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    [DataRow(0.5)]
    [DataRow(1d)]
    [DataRow(1.5)]
    public void TimelyFastArrivals_InterpolateAtMeasurementTimestamps(double seconds)
    {
        var initial = Create(null, [new(Origin, 0)]);
        var end = Origin.AddSeconds(seconds);
        var presentation = Create(initial, [new(Origin, 0), new(end, 100)], end);

        Assert.AreEqual(TimeSpan.FromSeconds(1.5), presentation.PresentationDelay);
        Assert.IsFalse(presentation.IsStep(0));
        AssertValue(presentation, Origin, 0, 0);
        AssertValue(presentation, Origin.AddSeconds(seconds / 4), 15.625, 0.15625);
        AssertValue(presentation, end, 100, 1);
        AssertValue(presentation, end.AddSeconds(20), 100, 1);
    }

    [TestMethod]
    [DataRow(0d, 25d)]
    [DataRow(0.75, 17.96875)]
    [DataRow(1d, 15.625)]
    public void Smoothing_BlendsEndpointOnlyControlsForRawAndNormalizedValues(double smoothing, double expected)
    {
        LineGraphPoint[] samples = [new(Origin, 0), new(Origin.AddSeconds(1), 100), new(Origin.AddSeconds(2), 20)];
        var presentation = Create(null, samples, smoothing: smoothing);
        var first = presentation.Curve.Segments[0];

        Assert.AreEqual((1 - smoothing) * 100 / 3, first.Control1, 1e-12);
        Assert.AreEqual(100 - ((1 - smoothing) * 100 / 3), first.Control2, 1e-12);
        Assert.AreEqual(first.Control1 / 100, first.NormalizedControl1, 1e-12);
        Assert.AreEqual(first.Control2 / 100, first.NormalizedControl2, 1e-12);
        AssertValue(presentation, Origin.AddMilliseconds(250), expected, expected / 100);
    }

    [TestMethod]
    [DataRow(1.501)]
    [DataRow(3d)]
    public void SlowIntervals_HoldThenStepAtTheNewTimestamp(double seconds)
    {
        var initial = Create(null, [new(Origin, 20)]);
        var end = Origin.AddSeconds(seconds);
        var presentation = Create(initial, [new(Origin, 20), new(end, 80)], end);

        Assert.IsTrue(presentation.IsStep(0));
        AssertValue(presentation, end.AddTicks(-1), 20, 0.2);
        AssertValue(presentation, end, 80, 0.8);
    }

    [TestMethod]
    [DataRow(0L, false)]
    [DataRow(1L, true)]
    public void ArrivalDeadline_AllowsTheBoundaryAndStepsLateEndpoints(long ticksLate, bool expectedStep)
    {
        var initial = Create(null, [new(Origin, 0)]);
        var received = Origin.AddSeconds(1.5).AddTicks(ticksLate);
        var presentation = Create(initial, [new(Origin, 0), new(Origin.AddSeconds(1), 100)], received);

        Assert.AreEqual(expectedStep, presentation.IsStep(0));
        AssertValue(presentation, Origin.AddMilliseconds(500), expectedStep ? 0 : 50, expectedStep ? 0 : 0.5);
        AssertValue(presentation, Origin.AddSeconds(1), 100, 1);
    }

    [TestMethod]
    public void UnchangedAndAppendedSnapshots_PreservePreviouslyLateClassification()
    {
        var initial = Create(null, [new(Origin, 0)]);
        LineGraphPoint[] samples = [new(Origin, 0), new(Origin.AddSeconds(1), 100)];
        var late = Create(initial, samples, Origin.AddSeconds(1.6));
        var unchanged = Create(late, [.. samples], Origin.AddSeconds(1.8));
        var appended = Create(unchanged, [.. samples, new(Origin.AddSeconds(2), 20)], Origin.AddSeconds(2));

        Assert.IsTrue(unchanged.IsStep(0));
        Assert.IsTrue(appended.IsStep(0));
        Assert.IsFalse(appended.IsStep(1));
        AssertValue(appended, Origin.AddMilliseconds(500), 0, 0);
        AssertValue(appended, Origin.AddSeconds(1.5), 60, 0.6);
    }

    [TestMethod]
    public void AppendingNeighborsAndRefreshingLater_NeverReshapesExistingSegments()
    {
        LineGraphPoint[] samples = [new(Origin, 0), new(Origin.AddSeconds(1), 50)];
        var previous = Create(null, samples);
        var appended = Create(previous, [.. samples, new(Origin.AddSeconds(2), 100)], Origin.AddSeconds(2));
        var refreshed = Create(appended, [.. appended.Samples], Origin.AddSeconds(20));

        Assert.AreEqual(previous.Curve.Segments[0], appended.Curve.Segments[0]);
        Assert.AreEqual(appended.Curve.Segments[0], refreshed.Curve.Segments[0]);
        Assert.IsFalse(refreshed.IsStep(0));
        Assert.IsFalse(refreshed.IsStep(1));
        AssertValue(refreshed, Origin.AddMilliseconds(250), 7.8125, 0.078125);
    }

    [TestMethod]
    public void InitialReplacementCorrectionAndClear_DiscardOldArrivalDecisions()
    {
        var initial = Create(null, [new(Origin, 0)]);
        LineGraphPoint[] samples = [new(Origin, 0), new(Origin.AddSeconds(1), 100)];
        var late = Create(initial, samples, Origin.AddSeconds(20));
        Assert.IsTrue(late.IsStep(0));

        var seeded = Create(null, samples, Origin.AddSeconds(20));
        var corrected = Create(late, [new(Origin, 10), new(Origin.AddSeconds(1), 100)], Origin.AddSeconds(20));
        var replacement = Create(late, [new(Origin.AddSeconds(5), 20), new(Origin.AddSeconds(6), 80)], Origin.AddSeconds(20));
        Assert.IsFalse(seeded.IsStep(0));
        Assert.IsFalse(corrected.IsStep(0));
        Assert.IsFalse(replacement.IsStep(0));
        AssertValue(corrected, Origin, 10, 0.1);

        var cleared = Create(late, [], Origin.AddSeconds(20));
        Assert.IsEmpty(cleared.Samples);
        Assert.IsEmpty(cleared.Curve.Segments);
        Assert.IsFalse(cleared.TryGetValue(Origin, out var value, out var normalized));
        Assert.AreEqual(0d, value);
        Assert.AreEqual(0d, normalized);
        Assert.IsFalse(seeded.TryGetValue(Origin.AddTicks(-1), out _, out _));
        Assert.IsFalse(Create(cleared, samples, Origin.AddSeconds(20)).IsStep(0));
    }

    [TestMethod]
    public void ProducerTrimming_RetainsDelayedWindowAndTwoPredecessorsWithoutGrowing()
    {
        var history = TimeSpan.FromSeconds(4);
        var previous = Create(null, Points(0, 10), Origin.AddSeconds(10), history: history);
        var retainedSegment = previous.Curve.Segments[5];

        var trimmed = Create(previous, Points(6, 11), Origin.AddSeconds(11), history: history);
        Assert.AreEqual(Origin.AddSeconds(4), trimmed.Samples[0].Timestamp);
        Assert.HasCount(8, trimmed.Samples);
        Assert.AreEqual(retainedSegment, trimmed.Curve.Segments[1]);
        AssertValue(trimmed, Origin.AddSeconds(5.5), 27.5, 0.275);

        var next = Create(trimmed, Points(7, 12), Origin.AddSeconds(12), history: history);
        Assert.AreEqual(Origin.AddSeconds(5), next.Samples[0].Timestamp);
        Assert.HasCount(8, next.Samples);
        Assert.AreEqual(retainedSegment, next.Curve.Segments[0]);
        Assert.IsFalse(next.IsStep(0));
    }

    [TestMethod]
    public void ExtremeClippedValues_KeepIndependentFiniteCurveAndInspectionValues()
    {
        LineGraphPoint[] samples = [new(Origin, -double.MaxValue), new(Origin.AddSeconds(1), double.MaxValue), new(Origin.AddSeconds(3), -double.MaxValue)];
        var presentation = Create(null, samples);
        var segment = presentation.Curve.Segments[0];

        Assert.IsTrue(double.IsFinite(segment.Control1));
        Assert.IsTrue(double.IsFinite(segment.Control2));
        AssertValue(presentation, Origin.AddMilliseconds(250), -double.MaxValue * 0.6875, 0.15625);
        AssertValue(presentation, Origin.AddMilliseconds(500), 0, 0.5);
        Assert.IsTrue(presentation.IsStep(1));
        AssertValue(presentation, Origin.AddSeconds(2), double.MaxValue, 1);
        AssertValue(presentation, Origin.AddSeconds(3), -double.MaxValue, 0);
    }

    [TestMethod]
    [DataRow(0d, 1d, true)]
    [DataRow(1d, 1d, false)]
    [DataRow(1d, 1.5, true)]
    [DataRow(2d, 1.5, false)]
    public void ConfiguredDelay_ControlsTheFixedBufferBudget(double delaySeconds, double intervalSeconds, bool expectedStep)
    {
        var delay = TimeSpan.FromSeconds(delaySeconds);
        var initial = Create(null, [new(Origin, 0)], delay: delay);
        var end = Origin.AddSeconds(intervalSeconds);
        var presentation = Create(initial, [new(Origin, 0), new(end, 100)], end, delay: delay);

        Assert.AreEqual(delay, presentation.PresentationDelay);
        Assert.AreEqual(expectedStep, presentation.IsStep(0));
        AssertValue(presentation, Origin.AddSeconds(intervalSeconds / 2), expectedStep ? 0 : 50, expectedStep ? 0 : 0.5);
    }

    [TestMethod]
    public void ChangingDelay_ReclassifiesTheReplacementSnapshot()
    {
        LineGraphPoint[] samples = [new(Origin, 0), new(Origin.AddSeconds(1.5), 100)];
        var previous = Create(null, samples, delay: TimeSpan.FromSeconds(1));
        Assert.IsTrue(previous.IsStep(0));

        var changed = Create(previous, [.. samples], Origin.AddSeconds(20), delay: TimeSpan.FromSeconds(2));
        Assert.IsFalse(changed.IsStep(0));
        AssertValue(changed, Origin.AddMilliseconds(750), 50, 0.5);
    }

    [TestMethod]
    public void NegativeDelay_IsRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = Create(null, [], delay: TimeSpan.FromTicks(-1)));
    }

    private static LineGraphPoint[] Points(int first, int last)
        => Enumerable.Range(first, last - first + 1).Select(index => new LineGraphPoint(Origin.AddSeconds(index), index * 5)).ToArray();

    private static LineGraphPresentation Create(LineGraphPresentation? previous, LineGraphPoint[] samples, DateTimeOffset? now = null, double smoothing = 1, TimeSpan? delay = null, TimeSpan? history = null)
        => new(previous, samples, 0, 100, smoothing, now ?? Origin, history ?? TimeSpan.FromSeconds(30), delay);

    private static void AssertValue(LineGraphPresentation presentation, DateTimeOffset time, double expectedValue, double expectedNormalized)
    {
        Assert.IsTrue(presentation.TryGetValue(time, out var value, out var normalized));
        Assert.AreEqual(expectedValue, value, Math.Max(1e-12, Math.Abs(expectedValue) * 1e-14));
        Assert.AreEqual(expectedNormalized, normalized, 1e-12);
    }
}
