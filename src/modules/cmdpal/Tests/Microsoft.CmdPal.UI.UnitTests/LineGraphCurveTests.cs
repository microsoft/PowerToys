// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls.Graphs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class LineGraphCurveTests
{
    private static readonly DateTimeOffset Origin = new(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Midpoint_InterpolatesValueAndMarker()
    {
        LineGraphPoint[] samples = [new(Origin, 0), new(Origin.AddSeconds(10), 100)];

        Assert.IsTrue(new LineGraphCurve(samples, 0, 100).TryGetValue(Origin.AddSeconds(5), out var value, out var normalized));
        Assert.AreEqual(50d, value);
        Assert.AreEqual(0.5, normalized);
    }

    [TestMethod]
    [DataRow(0, 10)]
    [DataRow(5, 70)]
    [DataRow(10, 30)]
    public void ExactTimestamp_ReturnsThatObservation(int seconds, int expected)
    {
        LineGraphPoint[] samples = [new(Origin, 10), new(Origin.AddSeconds(5), 70), new(Origin.AddSeconds(10), 30)];

        Assert.IsTrue(new LineGraphCurve(samples, 0, 100).TryGetValue(Origin.AddSeconds(seconds), out var value, out var normalized));
        Assert.AreEqual((double)expected, value);
        Assert.AreEqual(expected / 100d, normalized);
    }

    [TestMethod]
    public void EmptySamples_ReturnsNoValue()
    {
        Assert.IsFalse(new LineGraphCurve([], 0, 100).TryGetValue(Origin, out var value, out var normalized));
        Assert.AreEqual(0d, value);
        Assert.AreEqual(0d, normalized);
    }

    [TestMethod]
    public void BeforeFirstObservation_ReturnsNoValue()
    {
        LineGraphPoint[] samples = [new(Origin, 75)];

        Assert.IsFalse(new LineGraphCurve(samples, 0, 100).TryGetValue(Origin.AddTicks(-1), out var value, out var normalized));
        Assert.AreEqual(0d, value);
        Assert.AreEqual(0d, normalized);
    }

    [TestMethod]
    public void AfterLastObservation_HoldsRawValueAndClampsMarker()
    {
        LineGraphPoint[] samples = [new(Origin, 10), new(Origin.AddSeconds(5), 320)];

        Assert.IsTrue(new LineGraphCurve(samples, 0, 100).TryGetValue(Origin.AddSeconds(20), out var value, out var normalized));
        Assert.AreEqual(320d, value);
        Assert.AreEqual(1d, normalized);
    }

    [TestMethod]
    public void IndependentStreams_UseTheirOwnBracketingTimestamps()
    {
        LineGraphPoint[] first = [new(Origin, 0), new(Origin.AddSeconds(10), 100)];
        LineGraphPoint[] second = [new(Origin.AddSeconds(2), 80), new(Origin.AddSeconds(8), 20)];
        var time = Origin.AddSeconds(4);

        Assert.IsTrue(new LineGraphCurve(first, 0, 100).TryGetValue(time, out var firstValue, out var firstNormalized));
        Assert.IsTrue(new LineGraphCurve(second, 0, 100).TryGetValue(time, out var secondValue, out var secondNormalized));
        Assert.AreEqual(40d, firstValue);
        Assert.AreEqual(60d, secondValue);
        Assert.AreEqual(0.4, firstNormalized, 1e-15);
        Assert.AreEqual(0.6, secondNormalized, 1e-15);
    }

    [TestMethod]
    public void ClippedEndpoints_InterpolateMarkerSeparatelyFromRawValue()
    {
        LineGraphPoint[] samples = [new(Origin, -90), new(Origin.AddSeconds(10), 210)];

        Assert.IsTrue(new LineGraphCurve(samples, 10, 110).TryGetValue(Origin.AddSeconds(2.5), out var value, out var normalized));
        Assert.AreEqual(-15d, value);
        Assert.AreEqual(0.25, normalized);
    }

    [TestMethod]
    [DataRow(-double.MaxValue, double.MaxValue, 2.5, -double.MaxValue / 2)]
    [DataRow(-double.MaxValue, double.MaxValue, 5d, 0d)]
    [DataRow(double.MaxValue, -double.MaxValue, 2.5, double.MaxValue / 2)]
    [DataRow(double.MaxValue / 2, double.MaxValue, 5d, double.MaxValue * 0.75)]
    [DataRow(-double.MaxValue / 2, -double.MaxValue, 5d, -double.MaxValue * 0.75)]
    public void ExtremeValues_InterpolateWithoutOverflow(double start, double end, double seconds, double expected)
    {
        LineGraphPoint[] samples = [new(Origin, start), new(Origin.AddSeconds(10), end)];

        Assert.IsTrue(new LineGraphCurve(samples, 0, 100).TryGetValue(Origin.AddSeconds(seconds), out var value, out var normalized));
        Assert.IsTrue(double.IsFinite(value));
        Assert.AreEqual(expected, value, double.MaxValue * 1e-15);
        Assert.IsTrue(double.IsFinite(normalized) && normalized >= 0 && normalized <= 1);
    }

    [TestMethod]
    [DataRow(double.MaxValue)]
    [DataRow(-double.MaxValue)]
    [DataRow(37.123456789)]
    public void IdenticalValues_ReturnTheExactValue(double expected)
    {
        LineGraphPoint[] samples = [new(Origin, expected), new(Origin.AddSeconds(10), expected)];

        Assert.IsTrue(new LineGraphCurve(samples, 0, 100).TryGetValue(Origin.AddSeconds(3), out var value, out _));
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    public void AdjacentExtremeValues_StayFiniteAndWithinTheirBounds()
    {
        var lower = Math.BitDecrement(double.MaxValue);
        LineGraphPoint[] samples = [new(Origin, lower), new(Origin.AddSeconds(10), double.MaxValue)];

        Assert.IsTrue(new LineGraphCurve(samples, 0, 100).TryGetValue(Origin.AddSeconds(9), out var value, out var normalized));
        Assert.IsTrue(double.IsFinite(value) && value >= lower && value <= double.MaxValue);
        Assert.AreEqual(1d, normalized);
    }

    [TestMethod]
    [DataRow(0d, 25d)]
    [DataRow(0.5, 20.3125)]
    [DataRow(1d, 15.625)]
    public void TwoPointCurve_BlendsLinearAndZeroTangentCubic(double smoothing, double expected)
    {
        LineGraphPoint[] samples = [new(Origin, 0), new(Origin.AddSeconds(4), 100)];
        var curve = new LineGraphCurve(samples, 0, 100, smoothing);

        Assert.IsTrue(curve.TryGetValue(Origin.AddSeconds(1), out var value, out var normalized));
        Assert.AreEqual(expected, value, 1e-12);
        Assert.AreEqual(expected / 100, normalized, 1e-14);
    }

    [TestMethod]
    public void FullSmoothing_UsesWeightedHarmonicInteriorTangentsAndZeroEndpoints()
    {
        LineGraphPoint[] samples = [new(Origin, 10), new(Origin.AddSeconds(2), 30), new(Origin.AddSeconds(7), 60), new(Origin.AddSeconds(8), 80)];
        var curve = new LineGraphCurve(samples, 0, 100, 1);

        Assert.AreEqual(curve.Segments[0].Start, curve.Segments[0].Control1);
        Assert.AreEqual(curve.Segments[^1].End, curve.Segments[^1].Control2);
        for (var index = 0; index < curve.Segments.Length - 1; index++)
        {
            var left = curve.Segments[index];
            var right = curve.Segments[index + 1];
            var leftSlope = 3 * (left.End - left.Control2) / (left.EndTime - left.StartTime).TotalSeconds;
            var rightSlope = 3 * (right.Control1 - right.Start) / (right.EndTime - right.StartTime).TotalSeconds;
            Assert.AreEqual(leftSlope, rightSlope, 1e-12);
        }

        var first = curve.Segments[0];
        Assert.AreEqual(70d / 9, 3 * (first.End - first.Control2) / 2, 1e-12);
    }

    [TestMethod]
    [DataRow(0.25)]
    [DataRow(0.5)]
    [DataRow(1d)]
    public void Curves_PreserveEachSegmentsBoundsAndDirection(double smoothing)
    {
        double[][] shapes = [[0, 20, 21, 80, 100], [10, 10, 10, 50, 50], [0, 90, 20, 100, 0], [-100, -90, 0, 150, 200]];
        int[] seconds = [0, 1, 10, 11, 30];
        foreach (var shape in shapes)
        {
            var samples = shape.Select((value, index) => new LineGraphPoint(Origin.AddSeconds(seconds[index]), value)).ToArray();
            AssertBoundedCurve(new LineGraphCurve(samples, 0, 100, smoothing));
        }
    }

    [TestMethod]
    [DataRow(0.25)]
    [DataRow(0.5)]
    [DataRow(1d)]
    public void SmoothExtremeValuesAndUnevenIntervals_StayFiniteAndBounded(double smoothing)
    {
        LineGraphPoint[] samples =
        [
            new(Origin, -double.MaxValue),
            new(Origin.AddTicks(1), double.MaxValue),
            new(Origin.AddSeconds(10), Math.BitDecrement(double.MaxValue)),
            new(Origin.AddSeconds(10).AddTicks(1), -double.MaxValue / 2),
            new(Origin.AddDays(1), double.MaxValue / 2),
        ];

        AssertBoundedCurve(new LineGraphCurve(samples, 0, 100, smoothing));
    }

    [TestMethod]
    public void SmoothClippedEndpoints_UseSeparateRawAndNormalizedCurves()
    {
        LineGraphPoint[] samples = [new(Origin, -90), new(Origin.AddSeconds(4), 210)];
        var curve = new LineGraphCurve(samples, 10, 110, 1);

        Assert.IsTrue(curve.TryGetValue(Origin.AddSeconds(1), out var value, out var normalized));
        Assert.AreEqual(-43.125, value);
        Assert.AreEqual(0.15625, normalized);
    }

    [TestMethod]
    [DataRow(0.25)]
    [DataRow(0.5)]
    [DataRow(1d)]
    public void Smoothing_PreservesExactObservationsAndFinalHold(double smoothing)
    {
        LineGraphPoint[] samples = [new(Origin, -double.MaxValue), new(Origin.AddSeconds(3), 37.123456789), new(Origin.AddSeconds(10), double.MaxValue)];
        var curve = new LineGraphCurve(samples, 0, 100, smoothing);

        foreach (var sample in samples)
        {
            Assert.IsTrue(curve.TryGetValue(sample.Timestamp, out var value, out _));
            Assert.AreEqual(sample.Value, value);
        }

        Assert.IsFalse(curve.TryGetValue(Origin.AddTicks(-1), out var before, out var beforeNormalized));
        Assert.AreEqual(0d, before);
        Assert.AreEqual(0d, beforeNormalized);
        Assert.IsTrue(curve.TryGetValue(Origin.AddHours(1), out var held, out var heldNormalized));
        Assert.AreEqual(double.MaxValue, held);
        Assert.AreEqual(1d, heldNormalized);
    }

    [TestMethod]
    public void SmoothIndependentStreams_UseTheirOwnTimeIntervals()
    {
        LineGraphPoint[] first = [new(Origin, 0), new(Origin.AddSeconds(10), 100)];
        LineGraphPoint[] second = [new(Origin.AddSeconds(2), 80), new(Origin.AddSeconds(8), 20)];
        var time = Origin.AddSeconds(4);

        Assert.IsTrue(new LineGraphCurve(first, 0, 100, 1).TryGetValue(time, out var firstValue, out var firstNormalized));
        Assert.IsTrue(new LineGraphCurve(second, 0, 100, 1).TryGetValue(time, out var secondValue, out var secondNormalized));
        Assert.AreEqual(35.2, firstValue, 1e-12);
        Assert.AreEqual(580d / 9, secondValue, 1e-12);
        Assert.AreEqual(firstValue / 100, firstNormalized, 1e-12);
        Assert.AreEqual(secondValue / 100, secondNormalized, 1e-12);
    }

    [TestMethod]
    public void RetainingTwoPrecedingObservations_PreservesTheVisibleCurve()
    {
        LineGraphPoint[] samples =
        [
            new(Origin, -400),
            new(Origin.AddSeconds(1), 10),
            new(Origin.AddSeconds(3), 30),
            new(Origin.AddSeconds(5), 65),
            new(Origin.AddSeconds(8), 80),
            new(Origin.AddSeconds(12), 50),
        ];
        var complete = new LineGraphCurve(samples, 0, 100, 1);
        var retained = new LineGraphCurve(samples[2..], 0, 100, 1);

        // The visible window starts at six seconds; retained points at three and
        // five seconds preserve the tangent used by its first visible segment.
        for (var milliseconds = 6000; milliseconds <= 12000; milliseconds += 125)
        {
            var time = Origin.AddMilliseconds(milliseconds);
            Assert.IsTrue(complete.TryGetValue(time, out var completeValue, out var completeNormalized));
            Assert.IsTrue(retained.TryGetValue(time, out var retainedValue, out var retainedNormalized));
            Assert.AreEqual(completeValue, retainedValue, 1e-12);
            Assert.AreEqual(completeNormalized, retainedNormalized, 1e-12);
        }
    }

    [TestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    [DataRow(-0.01)]
    [DataRow(1.01)]
    public void InvalidSmoothing_IsRejected(double smoothing)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = new LineGraphCurve([], 0, 100, smoothing));
    }

    [TestMethod]
    [DataRow(double.NaN, 100d)]
    [DataRow(0d, double.PositiveInfinity)]
    [DataRow(10d, 10d)]
    [DataRow(100d, 0d)]
    [DataRow(-double.MaxValue, double.MaxValue)]
    public void InvalidRange_IsRejected(double minimum, double maximum)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = new LineGraphCurve([], minimum, maximum));
    }

    [TestMethod]
    public void EmptyAndSinglePointSmoothCurves_HaveNoSegments()
    {
        var empty = new LineGraphCurve([], 0, 100, 1);
        var single = new LineGraphCurve([new(Origin, 75)], 0, 100, 1);

        Assert.IsEmpty(empty.Segments);
        Assert.IsEmpty(single.Segments);
        Assert.IsFalse(empty.TryGetValue(Origin, out _, out _));
        Assert.IsTrue(single.TryGetValue(Origin.AddSeconds(5), out var value, out var normalized));
        Assert.AreEqual(75d, value);
        Assert.AreEqual(0.75, normalized);
    }

    private static void AssertBoundedCurve(LineGraphCurve curve)
    {
        foreach (var segment in curve.Segments)
        {
            var minimum = Math.Min(segment.Start, segment.End);
            var maximum = Math.Max(segment.Start, segment.End);
            Assert.IsTrue(double.IsFinite(segment.Control1) && segment.Control1 >= minimum && segment.Control1 <= maximum);
            Assert.IsTrue(double.IsFinite(segment.Control2) && segment.Control2 >= minimum && segment.Control2 <= maximum);
            var previousValue = segment.Start;
            var previousNormalized = segment.NormalizedStart;
            for (var step = 0; step <= 100; step++)
            {
                var time = segment.StartTime.AddTicks((segment.EndTime - segment.StartTime).Ticks * step / 100);
                Assert.IsTrue(curve.TryGetValue(time, out var value, out var normalized));
                Assert.IsTrue(double.IsFinite(value) && value >= minimum && value <= maximum);
                Assert.IsTrue(double.IsFinite(normalized) && normalized >= Math.Min(segment.NormalizedStart, segment.NormalizedEnd) && normalized <= Math.Max(segment.NormalizedStart, segment.NormalizedEnd));
                var tolerance = Math.Max(1, Math.Max(Math.Abs(minimum), Math.Abs(maximum))) * 1e-12;
                Assert.IsTrue(segment.End >= segment.Start ? value >= previousValue - tolerance : value <= previousValue + tolerance);
                Assert.IsTrue(segment.NormalizedEnd >= segment.NormalizedStart ? normalized >= previousNormalized - 1e-12 : normalized <= previousNormalized + 1e-12);
                previousValue = value;
                previousNormalized = normalized;
            }
        }
    }
}
