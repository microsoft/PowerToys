// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>
/// The parsed form of an Adaptive Cards <c>Chart.Line</c> element: <c>title</c>,
/// <c>showTitle</c>, <c>xAxisTitle</c>, <c>yAxisTitle</c>, <c>yMin</c>, <c>yMax</c>,
/// <c>color</c>, <c>colorSet</c>, <c>showLegend</c>, and <c>data</c>, with the schema's defaults.
/// It reads no other properties, so cards that render here render the same way in other hosts.
/// </summary>
internal sealed class LineChartModel : IAdaptiveVisualModel
{
    public static LineChartModel Empty { get; } = new() { IncrementalState = "{}" };

    public string? Title { get; init; }

    public bool ShowTitle { get; init; }

    public string? XAxisTitle { get; init; }

    public string? YAxisTitle { get; init; }

    public string? Color { get; init; }

    public string? ColorSet { get; init; }

    public double? YMin { get; init; }

    public double? YMax { get; init; }

    public bool ShowLegend { get; init; } = true;

    public IReadOnlyList<LineChartSeries> Series { get; init; } = [];

    /// <summary>Gets the number of horizontal slots, which is the length of the longest series.</summary>
    public int SlotCount { get; init; }

    /// <summary>Gets a value indicating whether any sample has an <c>x</c> label.</summary>
    public bool HasPointLabels { get; init; }

    /// <summary>Gets the canonical element JSON, which identifies everything the control renders.</summary>
    public string IncrementalState { get; init; } = "{}";

    /// <summary>
    /// Gets a value indicating whether the legend has something to show: it's on, as it is by
    /// default, and there's more than one series or a series has a legend.
    /// </summary>
    public bool ShowsLegend => ShowLegend && (Series.Count > 1 || Series.Any(series => !string.IsNullOrEmpty(series.Legend)));

    /// <summary>Returns the smallest and largest sample, or NaN when there are no samples.</summary>
    public (double Min, double Max) GetValueExtent()
    {
        var min = double.NaN;
        var max = double.NaN;
        foreach (var series in Series)
        {
            foreach (var point in series.Points)
            {
                if (point.Y is not double y)
                {
                    continue;
                }

                min = double.IsNaN(min) ? y : Math.Min(min, y);
                max = double.IsNaN(max) ? y : Math.Max(max, y);
            }
        }

        return (min, max);
    }

    public static LineChartModel Parse(string elementJson, ICollection<string> warnings)
    {
        try
        {
            using var document = JsonDocument.Parse(elementJson);
            return Parse(document.RootElement, warnings);
        }
        catch (JsonException ex)
        {
            warnings.Add($"The element JSON could not be read: {ex.Message}");
            return Empty;
        }
    }

    public static LineChartModel Parse(JsonElement element, ICollection<string> warnings)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            warnings.Add("The element must be a JSON object.");
            return Empty;
        }

        var series = ParseSeries(element, warnings);
        var slotCount = 0;
        var hasPointLabels = false;
        foreach (var item in series)
        {
            slotCount = Math.Max(slotCount, item.Points.Count);
            foreach (var point in item.Points)
            {
                hasPointLabels |= point.Label is not null;
            }
        }

        return new LineChartModel
        {
            Title = ChartJson.GetString(element, "title"),
            ShowTitle = ChartJson.GetBoolean(element, "showTitle") ?? false,
            XAxisTitle = ChartJson.GetString(element, "xAxisTitle"),
            YAxisTitle = ChartJson.GetString(element, "yAxisTitle"),
            Color = ChartJson.GetString(element, "color"),
            ColorSet = ChartJson.GetString(element, "colorSet"),
            YMin = ChartJson.GetNumber(element, "yMin"),
            YMax = ChartJson.GetNumber(element, "yMax"),
            ShowLegend = ChartJson.GetBoolean(element, "showLegend") ?? true,
            Series = series,
            SlotCount = slotCount,
            HasPointLabels = hasPointLabels,
            IncrementalState = ChartJson.Canonicalize(element),
        };
    }

    private static List<LineChartSeries> ParseSeries(JsonElement element, ICollection<string> warnings)
    {
        var result = new List<LineChartSeries>();
        if (!element.TryGetProperty("data", out var data))
        {
            warnings.Add("data is required.");
            return result;
        }

        if (data.ValueKind != JsonValueKind.Array)
        {
            warnings.Add("data must be an array of series.");
            return result;
        }

        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                warnings.Add("Each data item must be an object with a values array.");
                continue;
            }

            result.Add(new LineChartSeries
            {
                Legend = ChartJson.GetString(item, "legend"),
                Color = ChartJson.GetString(item, "color"),
                Points = ParsePoints(item, warnings),
            });
        }

        return result;
    }

    private static List<LineChartPoint> ParsePoints(JsonElement series, ICollection<string> warnings)
    {
        var points = new List<LineChartPoint>();
        if (!series.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            warnings.Add("Each series needs a values array.");
            return points;
        }

        foreach (var value in values.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                warnings.Add("Each value must be an object with x and y.");
                continue;
            }

            // y is a number that defaults to 0, so a value without a usable y is plotted at 0.
            var label = value.TryGetProperty("x", out var x) ? ChartJson.ToLabel(x) : null;
            double? y = 0;
            if (value.TryGetProperty("y", out var yValue))
            {
                y = ChartJson.ToNumber(yValue);
                if (y is null)
                {
                    warnings.Add("y must be a number.");
                    y = 0;
                }
            }

            points.Add(new LineChartPoint(label, y));
        }

        return points;
    }
}
