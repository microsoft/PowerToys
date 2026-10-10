// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.AdaptiveCards.Polyfills;

/// <summary>Maps line chart samples to plot coordinates.</summary>
/// <remarks>Samples are right-aligned, so a series that's still filling up grows from the right.</remarks>
internal readonly record struct LineChartLayout(
    double Width,
    double Height,
    ChartAxisRange Range,
    int SlotCount,
    double TopPadding,
    double RightPadding,
    double BottomPadding)
{
    /// <summary>The width below which a line chart draws as a sparkline, as in a dashboard tile.</summary>
    public const double CompactWidth = 400;

    public double PlotLeft => 0;

    public double PlotRight => Math.Max(PlotLeft, Width - RightPadding);

    public double PlotTop => Math.Min(TopPadding, Height);

    public double PlotBottom => Math.Max(PlotTop, Height - BottomPadding);

    public double SlotWidth => SlotCount > 1 ? (PlotRight - PlotLeft) / (SlotCount - 1) : 0;

    public double MapX(int slot) => SlotCount > 1 ? PlotLeft + (slot * SlotWidth) : PlotRight;

    public double MapY(double value) => PlotBottom - (Range.Normalize(value) * (PlotBottom - PlotTop));

    /// <summary>Returns whether a chart this wide draws as a sparkline.</summary>
    public static bool IsCompact(double width) => width > 0 && width < CompactWidth;

    /// <summary>Returns the slot of the first sample of a series with <paramref name="pointCount"/> samples.</summary>
    public int GetFirstSlot(int pointCount) => Math.Max(0, SlotCount - pointCount);

    /// <summary>Maps a series' samples to plot coordinates.</summary>
    public ChartPoint[] MapPoints(IReadOnlyList<LineChartPoint> points)
    {
        var firstSlot = GetFirstSlot(points.Count);
        var mapped = new ChartPoint[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            mapped[i] = new ChartPoint(MapX(firstSlot + i), MapY(points[i].Y));
        }

        return mapped;
    }
}
