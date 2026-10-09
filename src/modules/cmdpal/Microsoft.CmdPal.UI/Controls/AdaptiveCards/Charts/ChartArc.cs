// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>Angle math for gauges and donuts, in screen coordinates (y grows downward).</summary>
internal static class ChartArc
{
    /// <summary>
    /// Returns the point at <paramref name="degrees"/> on a circle. Angles are measured
    /// counterclockwise from the positive x axis, so 90 degrees is the top.
    /// </summary>
    public static ChartPoint PointAt(ChartPoint center, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return new ChartPoint(center.X + (radius * Math.Cos(radians)), center.Y - (radius * Math.Sin(radians)));
    }

    /// <summary>Maps a gauge fraction to an angle: 180 degrees (left, minimum) to 0 (right, maximum).</summary>
    public static double GaugeAngle(double fraction) => 180 - (Math.Clamp(fraction, 0, 1) * 180);

    /// <summary>Maps a donut fraction to an angle, starting at the top and moving clockwise.</summary>
    public static double DonutAngle(double fraction) => 90 - (fraction * 360);

    /// <summary>
    /// Splits 0..1 into one span per share, with <paramref name="gap"/> between neighbors. A closed
    /// ring, such as a donut, also has a gap where the last span meets the first.
    /// </summary>
    public static IReadOnlyList<(double Start, double End)> GetSpans(IReadOnlyList<double> shares, double gap, bool closed = true)
    {
        var spans = new (double Start, double End)[shares.Count];
        var visible = 0;
        foreach (var share in shares)
        {
            if (share > 0)
            {
                visible++;
            }
        }

        var halfGap = visible > 1 ? gap / 2 : 0;
        var start = 0d;
        var first = -1;
        var last = -1;
        for (var i = 0; i < shares.Count; i++)
        {
            var end = start + Math.Max(0, shares[i]);
            if (shares[i] > 0 && end - start > gap)
            {
                spans[i] = (start + halfGap, end - halfGap);
                first = first < 0 ? i : first;
                last = i;
            }
            else
            {
                spans[i] = (start, start);
            }

            start = end;
        }

        if (!closed && first >= 0)
        {
            spans[first] = (spans[first].Start - halfGap, spans[first].End);
            spans[last] = (spans[last].Start, spans[last].End + halfGap);
        }

        return spans;
    }
}
