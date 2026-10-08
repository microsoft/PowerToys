// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>Maps line chart samples to plot coordinates.</summary>
/// <remarks>
/// Samples are right-aligned: the last sample of every series sits at the right edge, so live
/// series that are still filling up grow from the right like a scrolling history.
/// </remarks>
internal readonly record struct LineChartLayout(
    double Width,
    double Height,
    ChartAxisRange Range,
    int SlotCount,
    double TopPadding,
    double RightPadding,
    double BottomPadding)
{
    public double PlotLeft => 0;

    public double PlotRight => Math.Max(PlotLeft, Width - RightPadding);

    public double PlotTop => Math.Min(TopPadding, Height);

    public double PlotBottom => Math.Max(PlotTop, Height - BottomPadding);

    public double SlotWidth => SlotCount > 1 ? (PlotRight - PlotLeft) / (SlotCount - 1) : 0;

    public double MapX(int slot) => SlotCount > 1 ? PlotLeft + (slot * SlotWidth) : PlotRight;

    public double MapY(double value) => PlotBottom - (Range.Normalize(value) * (PlotBottom - PlotTop));

    /// <summary>Returns the slot of the first sample of a series with <paramref name="pointCount"/> samples.</summary>
    public int GetFirstSlot(int pointCount) => Math.Max(0, SlotCount - pointCount);

    /// <summary>Splits a series into runs of consecutive samples; missing samples end a run.</summary>
    public IReadOnlyList<IReadOnlyList<ChartPoint>> GetRuns(IReadOnlyList<LineChartPoint> points)
    {
        var runs = new List<IReadOnlyList<ChartPoint>>();
        var firstSlot = GetFirstSlot(points.Count);
        List<ChartPoint>? current = null;
        for (var i = 0; i < points.Count; i++)
        {
            if (points[i].Y is double y)
            {
                current ??= [];
                current.Add(new ChartPoint(MapX(firstSlot + i), MapY(y)));
            }
            else if (current is not null)
            {
                runs.Add(current);
                current = null;
            }
        }

        if (current is not null)
        {
            runs.Add(current);
        }

        return runs;
    }
}
