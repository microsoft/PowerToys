// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls.Graphs;

internal sealed class LineGraphCurve
{
    private readonly LineGraphPoint[] _samples;
    private readonly double[] _normalizedValues;
    private readonly double _smoothing;

    public LineGraphCurve(LineGraphPoint[] samples, double minimum, double maximum, double smoothing = 0, bool useNeighborTangents = true)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (!double.IsFinite(smoothing) || smoothing < 0 || smoothing > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(smoothing));
        }

        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || maximum <= minimum || !double.IsFinite(maximum - minimum))
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), "The range must have a finite positive span.");
        }

        // The control owns this immutable snapshot; no second sample copy is needed.
        _samples = samples;
        _smoothing = smoothing;
        _normalizedValues = new double[samples.Length];
        for (var index = 0; index < samples.Length; index++)
        {
            var sample = samples[index];
            if (!double.IsFinite(sample.Value) || (index > 0 && sample.Timestamp <= samples[index - 1].Timestamp))
            {
                throw new ArgumentException("Samples must be finite and strictly ordered by timestamp.", nameof(samples));
            }

            _normalizedValues[index] = (Math.Clamp(sample.Value, minimum, maximum) - minimum) / (maximum - minimum);
        }

        Segments = new LineGraphSegment[Math.Max(0, samples.Length - 1)];
        for (var index = 0; index < Segments.Length; index++)
        {
            Segments[index] = CreateSegment(
                useNeighborTangents && index > 0 ? samples[index - 1] : null,
                samples[index],
                samples[index + 1],
                useNeighborTangents && index + 2 < samples.Length ? samples[index + 2] : null,
                useNeighborTangents && index > 0 ? _normalizedValues[index - 1] : 0,
                _normalizedValues[index],
                _normalizedValues[index + 1],
                useNeighborTangents && index + 2 < samples.Length ? _normalizedValues[index + 2] : 0,
                smoothing);
        }
    }

    public LineGraphSegment[] Segments { get; }

    public bool TryGetValue(DateTimeOffset time, out double value, out double normalizedValue)
    {
        value = 0;
        normalizedValue = 0;
        if (_samples.Length == 0 || time < _samples[0].Timestamp)
        {
            return false;
        }

        var low = 0;
        var high = _samples.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            if (_samples[middle].Timestamp <= time)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        var previous = _samples[high];
        value = previous.Value;
        normalizedValue = _normalizedValues[high];
        if (high == _samples.Length - 1 || time == previous.Timestamp)
        {
            return true;
        }

        EvaluateSegment(Segments[high], time, _smoothing, out value, out normalizedValue);
        return true;
    }

    internal double GetNormalizedValue(int index) => _normalizedValues[index];

    internal static void EvaluateSegment(LineGraphSegment segment, DateTimeOffset time, double smoothing, out double value, out double normalizedValue)
    {
        var progress = (double)(time - segment.StartTime).Ticks / (segment.EndTime - segment.StartTime).Ticks;
        if (smoothing == 0)
        {
            // Preserve linear inspection exactly, without cubic rounding.
            value = Interpolate(segment.Start, segment.End, progress);
            normalizedValue = Interpolate(segment.NormalizedStart, segment.NormalizedEnd, progress);
        }
        else
        {
            value = Evaluate(segment.Start, segment.Control1, segment.Control2, segment.End, progress);
            normalizedValue = Evaluate(segment.NormalizedStart, segment.NormalizedControl1, segment.NormalizedControl2, segment.NormalizedEnd, progress);
        }
    }

    internal static LineGraphSegment CreateSegment(
        LineGraphPoint? previous,
        LineGraphPoint start,
        LineGraphPoint end,
        LineGraphPoint? next,
        double normalizedPrevious,
        double normalizedStart,
        double normalizedEnd,
        double normalizedNext,
        double smoothing)
    {
        (double First, double Second) rawControls = (1d / 3, 2d / 3);
        (double First, double Second) normalizedControls = rawControls;
        if (smoothing != 0)
        {
            var previousInterval = previous is { } before ? (start.Timestamp - before.Timestamp).Ticks : 0d;
            var interval = (double)(end.Timestamp - start.Timestamp).Ticks;
            var nextInterval = next is { } after ? (after.Timestamp - end.Timestamp).Ticks : 0d;

            // Local scaling keeps opposite-sign extreme secants finite and makes
            // retained history independent of observations outside these neighbors.
            var scale = Math.Max(Math.Max(Math.Abs(previous?.Value ?? 0), Math.Abs(start.Value)), Math.Max(Math.Abs(end.Value), Math.Abs(next?.Value ?? 0)));
            scale = scale == 0 ? 1 : scale;
            rawControls = GetControlFractions((previous?.Value ?? 0) / scale, start.Value / scale, end.Value / scale, (next?.Value ?? 0) / scale, previousInterval, interval, nextInterval, smoothing);
            normalizedControls = GetControlFractions(normalizedPrevious, normalizedStart, normalizedEnd, normalizedNext, previousInterval, interval, nextInterval, smoothing);
        }

        return new(
            start.Timestamp,
            end.Timestamp,
            start.Value,
            Interpolate(start.Value, end.Value, rawControls.First),
            Interpolate(start.Value, end.Value, rawControls.Second),
            end.Value,
            normalizedStart,
            Interpolate(normalizedStart, normalizedEnd, normalizedControls.First),
            Interpolate(normalizedStart, normalizedEnd, normalizedControls.Second),
            normalizedEnd);
    }

    private static double GetTangent(double previousSlope, double nextSlope, double previousInterval, double nextInterval)
    {
        if (previousSlope == 0 || nextSlope == 0 || (previousSlope < 0) != (nextSlope < 0))
        {
            return 0;
        }

        var previousMagnitude = Math.Abs(previousSlope);
        var nextMagnitude = Math.Abs(nextSlope);
        var smaller = Math.Min(previousMagnitude, nextMagnitude);
        var previousWeight = ((2 * nextInterval) + previousInterval) / (3 * (previousInterval + nextInterval));
        var denominator = (previousWeight * (smaller / previousMagnitude)) + ((1 - previousWeight) * (smaller / nextMagnitude));
        var tangent = smaller / denominator;
        return previousSlope < 0 ? -tangent : tangent;
    }

    private static (double First, double Second) GetControlFractions(double previous, double start, double end, double next, double previousInterval, double interval, double nextInterval, double smoothing)
    {
        var slope = (end - start) / interval;

        // Zero endpoint tangents join the final observation smoothly to its hold.
        var startTangent = previousInterval == 0 ? 0 : GetTangent((start - previous) / previousInterval, slope, previousInterval, interval);
        var endTangent = nextInterval == 0 ? 0 : GetTangent(slope, (next - end) / nextInterval, interval, nextInterval);
        var first = slope == 0 ? 0 : Math.Clamp(startTangent / slope / 3, 0, 1);
        var second = slope == 0 ? 1 : 1 - Math.Clamp(endTangent / slope / 3, 0, 1);
        return (Interpolate(1d / 3, first, smoothing), Interpolate(2d / 3, second, smoothing));
    }

    private static double Evaluate(double start, double control1, double control2, double end, double progress)
    {
        // De Casteljau evaluation uses bounded convex combinations throughout.
        var first = Interpolate(start, control1, progress);
        var middle = Interpolate(control1, control2, progress);
        var last = Interpolate(control2, end, progress);
        return Interpolate(Interpolate(first, middle, progress), Interpolate(middle, last, progress), progress);
    }

    internal static double Interpolate(double start, double end, double progress)
    {
        if (start == end || progress == 0)
        {
            return start;
        }

        if (progress == 1)
        {
            return end;
        }

        var value = (start < 0) == (end < 0)
            ? start + ((end - start) * progress)
            : (start * (1 - progress)) + (end * progress);
        return Math.Clamp(value, Math.Min(start, end), Math.Max(start, end));
    }
}

internal readonly record struct LineGraphSegment(
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    double Start,
    double Control1,
    double Control2,
    double End,
    double NormalizedStart,
    double NormalizedControl1,
    double NormalizedControl2,
    double NormalizedEnd);
