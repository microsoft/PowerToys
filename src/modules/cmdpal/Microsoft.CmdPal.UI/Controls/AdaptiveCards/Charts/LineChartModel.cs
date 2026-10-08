// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

internal enum ChartFill
{
    None,
    Gradient,
}

internal enum ChartInterpolation
{
    Smooth,
    Linear,
}

internal enum ChartStyle
{
    Default,
    Sparkline,
}

/// <summary>
/// The parsed form of an Adaptive Cards <c>Chart.Line</c> element.
/// </summary>
/// <remarks>
/// Standard properties follow the Adaptive Cards schema: <c>title</c>, <c>xAxisTitle</c>,
/// <c>yAxisTitle</c>, <c>color</c>, <c>colorSet</c>, and <c>data</c>. Command Palette also reads
/// optional properties that other hosts ignore: <c>yMin</c>, <c>yMax</c>, <c>valueFormat</c>,
/// <c>fill</c>, <c>curve</c>, <c>style</c>, <c>showLegend</c>, and <c>minHeight</c>.
/// </remarks>
internal sealed class LineChartModel : IAdaptiveVisualModel
{
    public static LineChartModel Empty { get; } = new() { IncrementalState = "{}" };

    public string? Title { get; init; }

    public string? XAxisTitle { get; init; }

    public string? YAxisTitle { get; init; }

    public string? Color { get; init; }

    public string? ColorSet { get; init; }

    public double? YMin { get; init; }

    public double? YMax { get; init; }

    public ChartValueFormat ValueFormat { get; init; }

    public ChartFill Fill { get; init; }

    public ChartInterpolation Interpolation { get; init; }

    public ChartStyle Style { get; init; }

    public bool? ShowLegend { get; init; }

    public double? MinHeight { get; init; }

    public IReadOnlyList<LineChartSeries> Series { get; init; } = [];

    /// <summary>Gets the number of horizontal slots, which is the length of the longest series.</summary>
    public int SlotCount { get; init; }

    /// <summary>Gets a value indicating whether any sample has an <c>x</c> label.</summary>
    public bool HasPointLabels { get; init; }

    /// <summary>Gets the canonical element JSON, which identifies everything the control renders.</summary>
    public string IncrementalState { get; init; } = "{}";

    public bool ShowsLegend => Style != ChartStyle.Sparkline && (ShowLegend ?? Series.Count > 1);

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
            XAxisTitle = ChartJson.GetString(element, "xAxisTitle"),
            YAxisTitle = ChartJson.GetString(element, "yAxisTitle"),
            Color = ChartJson.GetString(element, "color"),
            ColorSet = ChartJson.GetString(element, "colorSet"),
            YMin = ChartJson.GetNumber(element, "yMin"),
            YMax = ChartJson.GetNumber(element, "yMax"),
            ValueFormat = ChartJson.GetEnum(element, "valueFormat", ChartValueFormat.Number, warnings),
            Fill = ChartJson.GetEnum(element, "fill", ChartFill.None, warnings),
            Interpolation = ChartJson.GetEnum(element, "curve", ChartInterpolation.Smooth, warnings),
            Style = ChartJson.GetEnum(element, "style", ChartStyle.Default, warnings),
            ShowLegend = ChartJson.GetBoolean(element, "showLegend"),
            MinHeight = ChartJson.GetPixels(element, "minHeight"),
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
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    var label = value.TryGetProperty("x", out var x) ? ChartJson.ToLabel(x) : null;
                    var y = value.TryGetProperty("y", out var yValue) ? ChartJson.ToNumber(yValue) : null;
                    points.Add(new LineChartPoint(label, y));
                    break;
                case JsonValueKind.Number:
                    points.Add(new LineChartPoint(null, ChartJson.ToNumber(value)));
                    break;
                default:
                    points.Add(new LineChartPoint(null, null));
                    break;
            }
        }

        return points;
    }
}
