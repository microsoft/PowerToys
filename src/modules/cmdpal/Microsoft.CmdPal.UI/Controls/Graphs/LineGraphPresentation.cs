// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls.Graphs;

internal sealed class LineGraphPresentation
{
    public static readonly TimeSpan DefaultPresentationDelay = TimeSpan.FromSeconds(1.5);

    private readonly double _minimum;
    private readonly double _maximum;
    private readonly double _smoothing;
    private readonly bool[] _steps;

    public LineGraphPresentation(LineGraphPresentation? previous, LineGraphPoint[] samples, double minimum, double maximum, double smoothing, DateTimeOffset now, TimeSpan historyDuration, TimeSpan? presentationDelay = null)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(historyDuration, TimeSpan.Zero);

        PresentationDelay = presentationDelay ?? DefaultPresentationDelay;
        ArgumentOutOfRangeException.ThrowIfLessThan(PresentationDelay, TimeSpan.Zero, nameof(presentationDelay));

        _minimum = minimum;
        _maximum = maximum;
        _smoothing = smoothing;
        Samples = samples;

        var previousOffset = 0;
        var continuesPrevious = previous is not null && samples.Length > 0 && previous.Samples.Length > 0 &&
            previous._minimum == minimum && previous._maximum == maximum && previous._smoothing == smoothing && previous.PresentationDelay == PresentationDelay &&
            TryMatchPrevious(previous.Samples, samples, out previousOffset);

        if (continuesPrevious)
        {
            // Producers retain their own history window. Preserve the additional
            // delayed window locally, with two predecessors for its left boundary.
            var cutoffTicks = Math.Max(0, now.UtcTicks - Math.Min(now.UtcTicks, historyDuration.Ticks) - PresentationDelay.Ticks);
            var cutoff = new DateTimeOffset(cutoffTicks, TimeSpan.Zero);
            var retainedStart = FindRetainedStart(previous!.Samples, cutoff);
            var prefixCount = Math.Max(0, previousOffset - retainedStart);
            if (prefixCount > 0)
            {
                Samples = new LineGraphPoint[prefixCount + samples.Length];
                Array.Copy(previous.Samples, retainedStart, Samples, 0, prefixCount);
                Array.Copy(samples, 0, Samples, prefixCount, samples.Length);
                previousOffset = retainedStart;
            }
        }

        // Endpoint-only monotone cubics need one following observation, without
        // the additional lookahead required by the general PCHIP curve.
        Curve = new LineGraphCurve(Samples, minimum, maximum, smoothing, useNeighborTangents: false);
        _steps = new bool[Curve.Segments.Length];
        for (var index = 0; index < Curve.Segments.Length; index++)
        {
            var start = Samples[index];
            var end = Samples[index + 1];

            if (continuesPrevious && previousOffset + index < previous!.Curve.Segments.Length)
            {
                _steps[index] = previous.IsStep(previousOffset + index);
            }
            else
            {
                // Initial/replacement history has no receipt information. For new
                // arrivals, the endpoint must arrive before this interval is shown.
                _steps[index] = end.Timestamp - start.Timestamp > PresentationDelay ||
                    (continuesPrevious && now - start.Timestamp > PresentationDelay);
            }
        }
    }

    public LineGraphPoint[] Samples { get; }

    public TimeSpan PresentationDelay { get; }

    public LineGraphCurve Curve { get; }

    public bool IsStep(int index) => _steps[index];

    public bool TryGetValue(DateTimeOffset time, out double value, out double normalizedValue)
    {
        value = 0;
        normalizedValue = 0;
        if (Samples.Length == 0 || time < Samples[0].Timestamp)
        {
            return false;
        }

        var low = 0;
        var high = Samples.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            if (Samples[middle].Timestamp <= time)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        value = Samples[high].Value;
        normalizedValue = Curve.GetNormalizedValue(high);
        if (high < Curve.Segments.Length && time != Samples[high].Timestamp && !IsStep(high))
        {
            LineGraphCurve.EvaluateSegment(Curve.Segments[high], time, _smoothing, out value, out normalizedValue);
        }

        return true;
    }

    private static bool TryMatchPrevious(LineGraphPoint[] previous, LineGraphPoint[] samples, out int previousOffset)
    {
        previousOffset = 0;
        while (previousOffset < previous.Length && previous[previousOffset].Timestamp < samples[0].Timestamp)
        {
            previousOffset++;
        }

        if (previousOffset == previous.Length)
        {
            return false;
        }

        // A retained suffix followed by appended observations continues history.
        // Corrections, inserted history and replacements discard old decisions.
        for (var index = 0; previousOffset + index < previous.Length; index++)
        {
            if (index == samples.Length || samples[index] != previous[previousOffset + index])
            {
                return false;
            }
        }

        return true;
    }

    private static int FindRetainedStart(LineGraphPoint[] samples, DateTimeOffset cutoff)
    {
        var low = 0;
        var high = samples.Length;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (samples[middle].Timestamp < cutoff)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return Math.Max(0, low - 2);
    }
}
