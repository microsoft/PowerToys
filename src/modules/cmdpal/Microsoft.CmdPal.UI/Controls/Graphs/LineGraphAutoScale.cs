// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls.Graphs;

internal static class LineGraphAutoScale
{
    public static double GetMaximum(IReadOnlyList<LineGraphPresentation> presentations, DateTimeOffset windowEnd, TimeSpan history, double minimum, double minimumMaximum, ReadOnlySpan<double> valueDivisors = default, IReadOnlyList<bool>? readoutOnly = null)
    {
        var windowStart = new DateTimeOffset(Math.Max(0, windowEnd.UtcTicks - history.Ticks), TimeSpan.Zero);
        var peak = minimum;
        for (var index = 0; index < presentations.Count; index++)
        {
            if (readoutOnly?[index] == true)
            {
                continue;
            }

            var presentation = presentations[index];

            // Include the rendered boundary values, not an offscreen endpoint of
            // a segment crossing the window. This also respects held/late steps.
            if (presentation.TryGetValue(windowStart, out var left, out _))
            {
                peak = Math.Max(peak, left);
            }

            foreach (var sample in presentation.Samples)
            {
                if (sample.Timestamp >= windowStart && sample.Timestamp <= windowEnd)
                {
                    peak = Math.Max(peak, sample.Value);
                }
            }

            if (presentation.TryGetValue(windowEnd, out var right, out _))
            {
                peak = Math.Max(peak, right);
            }
        }

        var floorSpan = minimumMaximum - minimum;
        var requestedSpan = Math.Min((peak - minimum) * 1.1, double.MaxValue);
        if (requestedSpan <= floorSpan)
        {
            return minimumMaximum;
        }

        var divisor = 1d;
        if (!valueDivisors.IsEmpty)
        {
            divisor = valueDivisors[0];
            for (var index = 1; index < valueDivisors.Length && requestedSpan >= valueDivisors[index]; index++)
            {
                divisor = valueDivisors[index];
            }
        }

        var displaySpan = requestedSpan / divisor;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(displaySpan)));
        var leading = displaySpan / magnitude;
        var step = leading <= 1 ? 1 : leading <= 2 ? 2 : leading <= 5 ? 5 : 10;
        var roundedSpan = step * magnitude * divisor;

        // Finite measurements can still overflow while adding headroom or
        // rounding. Keep the display range finite even at double's limits.
        var span = double.IsFinite(roundedSpan) && roundedSpan >= requestedSpan ? roundedSpan : requestedSpan;
        var upperLimit = minimum < 0 ? minimum + double.MaxValue : double.MaxValue;
        return Math.Max(minimumMaximum, Math.Min(minimum + span, upperLimit));
    }
}
