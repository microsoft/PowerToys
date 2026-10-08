// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>Builds smooth curves through chart points.</summary>
internal static class ChartCurve
{
    /// <summary>
    /// Builds a monotone cubic curve (Fritsch–Carlson) through <paramref name="points"/>, which
    /// must be ordered by X. The curve never overshoots the data, so it can't dip below zero or
    /// rise above a fixed maximum between two samples.
    /// </summary>
    /// <returns>One segment per pair of neighboring points.</returns>
    public static ChartBezierSegment[] CreateMonotoneSegments(IReadOnlyList<ChartPoint> points)
    {
        var count = points.Count;
        if (count < 2)
        {
            return [];
        }

        var slopes = new double[count - 1];
        for (var i = 0; i < count - 1; i++)
        {
            var dx = points[i + 1].X - points[i].X;
            slopes[i] = dx == 0 ? 0 : (points[i + 1].Y - points[i].Y) / dx;
        }

        var tangents = new double[count];
        tangents[0] = slopes[0];
        tangents[count - 1] = slopes[count - 2];
        for (var i = 1; i < count - 1; i++)
        {
            tangents[i] = slopes[i - 1] * slopes[i] <= 0 ? 0 : (slopes[i - 1] + slopes[i]) / 2;
        }

        for (var i = 0; i < count - 1; i++)
        {
            if (slopes[i] == 0)
            {
                tangents[i] = 0;
                tangents[i + 1] = 0;
                continue;
            }

            var a = tangents[i] / slopes[i];
            var b = tangents[i + 1] / slopes[i];
            var magnitude = (a * a) + (b * b);
            if (magnitude > 9)
            {
                var tau = 3 / Math.Sqrt(magnitude);
                tangents[i] = tau * a * slopes[i];
                tangents[i + 1] = tau * b * slopes[i];
            }
        }

        var segments = new ChartBezierSegment[count - 1];
        for (var i = 0; i < count - 1; i++)
        {
            var start = points[i];
            var end = points[i + 1];
            var third = (end.X - start.X) / 3;
            segments[i] = new ChartBezierSegment(
                new ChartPoint(start.X + third, start.Y + (tangents[i] * third)),
                new ChartPoint(end.X - third, end.Y - (tangents[i + 1] * third)),
                end);
        }

        return segments;
    }
}
