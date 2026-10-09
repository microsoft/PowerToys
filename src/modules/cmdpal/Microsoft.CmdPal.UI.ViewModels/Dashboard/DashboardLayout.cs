// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Dashboard;

/// <summary>
/// Places dashboard tiles on a grid of fixed-size cells.
/// </summary>
/// <remarks>
/// <para>
/// Settings store the layout the user asked for, along with the grid size it was made on.
/// This class turns that into a layout that fits the current display, without ever writing
/// back to settings. That way, plugging into a smaller display and back again restores the
/// original layout instead of leaving it squashed.
/// </para>
/// <para>
/// When the grid changes size, positions scale proportionally (a tile on the right edge stays on
/// the right edge), but sizes don't, so tile contents stay legible. Tiles are then placed in
/// order: a tile that lands on top of one that's already placed moves to the nearest free spot,
/// shrinks to the minimum size if it still doesn't fit anywhere, and as a last resort goes below
/// everything else. The dashboard scrolls, so nothing is ever lost.
/// </para>
/// </remarks>
public static class DashboardLayout
{
    /// <summary>
    /// The size of a grid cell, in device-independent pixels.
    /// </summary>
    public const double CellSize = 20;

    public const int MinColumnSpan = 10;
    public const int MinRowSpan = 8;
    public const int DefaultColumnSpan = 18;
    public const int DefaultRowSpan = 24;

    /// <summary>
    /// Returns how many whole cells fit in <paramref name="length"/> DIPs. Never less than
    /// enough for one minimum-size tile.
    /// </summary>
    public static int ColumnsFor(double length) =>
        Math.Max(MinColumnSpan, double.IsFinite(length) ? (int)Math.Floor(length / CellSize) : MinColumnSpan);

    /// <summary>
    /// Returns how many whole cells fit in <paramref name="length"/> DIPs. Never less than
    /// enough for one minimum-size tile.
    /// </summary>
    public static int RowsFor(double length) =>
        Math.Max(MinRowSpan, double.IsFinite(length) ? (int)Math.Floor(length / CellSize) : MinRowSpan);

    /// <summary>
    /// Converts a length in DIPs to the nearest whole number of cells.
    /// </summary>
    public static int ToCells(double length) =>
        double.IsFinite(length) ? (int)Math.Round(length / CellSize, MidpointRounding.AwayFromZero) : 0;

    /// <summary>
    /// Returns the number of rows the arranged tiles occupy. This can be more than the grid has
    /// when tiles overflowed.
    /// </summary>
    public static int Extent(IReadOnlyList<DashboardTileRect> arranged)
    {
        var bottom = 0;
        foreach (var rect in arranged)
        {
            bottom = Math.Max(bottom, rect.Bottom);
        }

        return bottom;
    }

    /// <summary>
    /// Fits <paramref name="desired"/> onto a grid of <paramref name="columns"/> by
    /// <paramref name="rows"/> cells, so that no two tiles overlap.
    /// </summary>
    /// <param name="desired">The tiles' requested rectangles, in priority order.</param>
    /// <param name="columns">The width of the current grid, in cells.</param>
    /// <param name="rows">The height of the current grid, in cells.</param>
    /// <param name="designColumns">
    /// The width of the grid <paramref name="desired"/> was laid out on, or zero to use positions as-is.
    /// </param>
    /// <param name="designRows">
    /// The height of the grid <paramref name="desired"/> was laid out on, or zero to use positions as-is.
    /// </param>
    /// <param name="priorityIndex">
    /// The index of a tile to place before all others, e.g. the one the user just dragged, so it
    /// lands exactly where it was dropped and pushes the others out of its way.
    /// </param>
    /// <returns>One rectangle per tile, in the same order as <paramref name="desired"/>.</returns>
    public static DashboardTileRect[] Arrange(
        IReadOnlyList<DashboardTileRect> desired,
        int columns,
        int rows,
        int designColumns = 0,
        int designRows = 0,
        int priorityIndex = -1)
    {
        columns = Math.Max(columns, MinColumnSpan);
        rows = Math.Max(rows, MinRowSpan);

        var result = new DashboardTileRect[desired.Count];
        var placed = new List<DashboardTileRect>(desired.Count);

        foreach (var index in PlacementOrder(desired.Count, priorityIndex))
        {
            var rect = Place(desired[index], columns, rows, designColumns, designRows, placed);
            result[index] = rect;
            placed.Add(rect);
        }

        return result;
    }

    private static IEnumerable<int> PlacementOrder(int count, int priorityIndex)
    {
        if (priorityIndex >= 0 && priorityIndex < count)
        {
            yield return priorityIndex;
        }

        for (var i = 0; i < count; i++)
        {
            if (i != priorityIndex)
            {
                yield return i;
            }
        }
    }

    private static DashboardTileRect Place(
        DashboardTileRect tile,
        int columns,
        int rows,
        int designColumns,
        int designRows,
        List<DashboardTileRect> placed)
    {
        var columnSpan = Math.Clamp(tile.ColumnSpan, MinColumnSpan, columns);
        var rowSpan = Math.Clamp(tile.RowSpan, MinRowSpan, rows);

        if (!tile.IsPlaced)
        {
            return FirstFree(columnSpan, rowSpan, columns, rows, placed)
                ?? FirstFree(MinColumnSpan, MinRowSpan, columns, rows, placed)
                ?? Overflow(0, columnSpan, rowSpan, placed);
        }

        var column = Scale(tile.Column, columns, designColumns);
        var row = Scale(tile.Row, rows, designRows);
        column = Math.Clamp(column, 0, columns - columnSpan);
        row = Math.Clamp(row, 0, rows - rowSpan);

        var candidate = new DashboardTileRect(column, row, columnSpan, rowSpan);
        if (IsFree(candidate, placed))
        {
            return candidate;
        }

        return NearestFree(column, row, columnSpan, rowSpan, columns, rows, placed)
            ?? NearestFree(column, row, MinColumnSpan, MinRowSpan, columns, rows, placed)
            ?? Overflow(column, columnSpan, rowSpan, placed);
    }

    private static int Scale(int position, int size, int designSize)
    {
        if (designSize <= 0 || designSize == size)
        {
            return position;
        }

        return (int)Math.Round((double)position * size / designSize, MidpointRounding.AwayFromZero);
    }

    private static bool IsFree(DashboardTileRect rect, List<DashboardTileRect> placed)
    {
        foreach (var other in placed)
        {
            if (rect.IntersectsWith(other))
            {
                return false;
            }
        }

        return true;
    }

    private static DashboardTileRect? FirstFree(int columnSpan, int rowSpan, int columns, int rows, List<DashboardTileRect> placed)
    {
        for (var row = 0; row + rowSpan <= rows; row++)
        {
            for (var column = 0; column + columnSpan <= columns; column++)
            {
                var candidate = new DashboardTileRect(column, row, columnSpan, rowSpan);
                if (IsFree(candidate, placed))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static DashboardTileRect? NearestFree(
        int column,
        int row,
        int columnSpan,
        int rowSpan,
        int columns,
        int rows,
        List<DashboardTileRect> placed)
    {
        DashboardTileRect? best = null;
        var bestDistance = long.MaxValue;
        for (var r = 0; r + rowSpan <= rows; r++)
        {
            for (var c = 0; c + columnSpan <= columns; c++)
            {
                long dx = c - column;
                long dy = r - row;
                var distance = (dx * dx) + (dy * dy);
                if (distance >= bestDistance)
                {
                    continue;
                }

                var candidate = new DashboardTileRect(c, r, columnSpan, rowSpan);
                if (IsFree(candidate, placed))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    private static DashboardTileRect Overflow(int column, int columnSpan, int rowSpan, List<DashboardTileRect> placed) =>
        new(column, Extent(placed), columnSpan, rowSpan);
}
