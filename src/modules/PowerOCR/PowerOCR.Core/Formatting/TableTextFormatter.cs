// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerOCR.Core.Models;

namespace PowerOCR.Core.Formatting;

public static class TableTextFormatter
{
    // The legacy extractor removed six pixels from OCR bounds before projecting them
    // onto rows and columns. Split that tolerance evenly across both edges so that
    // small OCR overlaps do not collapse adjacent cells into one segment.
    private const double OcrBoundsTolerance = 6;
    private const double MinimumBoundsSizeForTolerance = 10;

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
        var occupied = cells
            .Select(cell => horizontal
                ? (Start: cell.Bounds.Y, End: cell.Bounds.Bottom)
                : (Start: cell.Bounds.X, End: cell.Bounds.Right))
            .Where(segment => segment.End > segment.Start)
            .Select(segment =>
            {
                double inset = (segment.End - segment.Start) > MinimumBoundsSizeForTolerance
                    ? OcrBoundsTolerance / 2
                    : 0;
                return (Start: segment.Start + inset, End: segment.End - inset);
            })
            .OrderBy(segment => segment.Start)
            .ThenBy(segment => segment.End)
            .ToList();

        var segments = new List<OcrRect>();
        if (occupied.Count == 0)
        {
            return [bounds];
        }

        double segmentStart = occupied[0].Start;
        double segmentEnd = occupied[0].End;
        foreach (var current in occupied.Skip(1))
        {
            if (current.Start >= segmentEnd)
            {
                segments.Add(CreateSegment(bounds, horizontal, segmentStart, segmentEnd));
                segmentStart = current.Start;
                segmentEnd = current.End;
            }
            else
            {
                segmentEnd = Math.Max(segmentEnd, current.End);
            }
        }

        segments.Add(CreateSegment(bounds, horizontal, segmentStart, segmentEnd));
        return segments;
    }

    private static OcrRect CreateSegment(OcrRect bounds, bool horizontal, double start, double end)
        => horizontal
            ? new(bounds.X, start, bounds.Width, end - start)
            : new(start, bounds.Y, end - start, bounds.Height);

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
