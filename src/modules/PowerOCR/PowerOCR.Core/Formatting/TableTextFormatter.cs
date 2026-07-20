// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerOCR.Core.Models;

namespace PowerOCR.Core.Formatting;

public static class TableTextFormatter
{
    private const int GridSpacing = 3;

    public static string Format(IReadOnlyList<OcrLineData> cells, string languageTag)
    {
        if (cells.Count == 0)
        {
            return string.Empty;
        }

        OcrRect bounds = cells.Select(cell => cell.Bounds).Aggregate((left, right) => left.Union(right));
        List<OcrRect> rows = BuildSegments(cells, bounds, horizontal: true);
        List<OcrRect> columns = BuildSegments(cells, bounds, horizontal: false);
        string[,] values = new string[rows.Count, columns.Count];

        foreach (OcrLineData cell in cells)
        {
            int row = BestIntersection(rows, cell.Bounds);
            int column = BestIntersection(columns, cell.Bounds);
            string text = OcrTextFormatter.UsesSpaces(languageTag)
                ? cell.Text
                : OcrTextFormatter.JoinCjkAwareWords(cell.Words);

            values[row, column] = string.IsNullOrEmpty(values[row, column])
                ? text
                : $"{values[row, column]} {text}";
        }

        var output = new List<string>(rows.Count);
        for (int row = 0; row < rows.Count; row++)
        {
            var valuesInRow = new List<string>(columns.Count);
            for (int column = 0; column < columns.Count; column++)
            {
                valuesInRow.Add(values[row, column] ?? string.Empty);
            }

            while (valuesInRow.Count > 0 && string.IsNullOrEmpty(valuesInRow[^1]))
            {
                valuesInRow.RemoveAt(valuesInRow.Count - 1);
            }

            output.Add(string.Join('\t', valuesInRow));
        }

        return string.Join(Environment.NewLine, output);
    }

    private static List<OcrRect> BuildSegments(
        IReadOnlyList<OcrLineData> cells,
        OcrRect bounds,
        bool horizontal)
    {
        int start = (int)Math.Floor(horizontal ? bounds.Y : bounds.X);
        int end = (int)Math.Ceiling(horizontal ? bounds.Bottom : bounds.Right);
        var occupied = new List<int>();

        for (int position = start; position <= end; position += GridSpacing)
        {
            var scan = horizontal
                ? new OcrRect(bounds.X, position, bounds.Width, 1)
                : new OcrRect(position, bounds.Y, 1, bounds.Height);

            if (cells.Any(cell => cell.Bounds.Intersects(scan)))
            {
                occupied.Add(position);
            }
        }

        var segments = new List<OcrRect>();
        if (occupied.Count == 0)
        {
            return [bounds];
        }

        int segmentStart = occupied[0];
        int previous = occupied[0];
        foreach (int current in occupied.Skip(1))
        {
            if (current - previous != GridSpacing)
            {
                segments.Add(CreateSegment(bounds, horizontal, segmentStart, previous));
                segmentStart = current;
            }

            previous = current;
        }

        segments.Add(CreateSegment(bounds, horizontal, segmentStart, previous));
        return segments;
    }

    private static OcrRect CreateSegment(OcrRect bounds, bool horizontal, int start, int end)
        => horizontal
            ? new(bounds.X, start, bounds.Width, Math.Max(GridSpacing, end - start + GridSpacing))
            : new(start, bounds.Y, Math.Max(GridSpacing, end - start + GridSpacing), bounds.Height);

    private static int BestIntersection(IReadOnlyList<OcrRect> segments, OcrRect cell)
    {
        int bestIndex = 0;
        double bestArea = double.MinValue;
        for (int index = 0; index < segments.Count; index++)
        {
            double area = segments[index].IntersectionArea(cell);
            if (area > bestArea)
            {
                bestArea = area;
                bestIndex = index;
            }
        }

        return bestIndex;
    }
}
