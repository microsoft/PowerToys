// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.CmdPal.AdaptiveCards.Polyfills;

internal enum BarOrientation
{
    Vertical,
    Horizontal,
}

/// <summary>How a <c>Chart.HorizontalBar</c> lays out its bars.</summary>
internal enum BarDisplayMode
{
    /// <summary>Bars on a value axis that starts at zero.</summary>
    AbsoluteWithAxis,

    /// <summary>Bars without an axis, each with its value at its end.</summary>
    AbsoluteNoAxis,

    /// <summary>Each bar shows its share of the total.</summary>
    PartToWhole,
}

/// <summary>
/// The parsed form of an Adaptive Cards <c>Chart.VerticalBar</c> or <c>Chart.HorizontalBar</c>
/// element, with the schema's defaults.
/// </summary>
internal sealed class BarChartModel : IAdaptiveVisualModel
{
    public BarOrientation Orientation { get; init; }

    public string? Title { get; init; }

    public bool ShowTitle { get; init; }

    public string? XAxisTitle { get; init; }

    public string? YAxisTitle { get; init; }

    public string? Color { get; init; }

    public string? ColorSet { get; init; }

    public bool ShowBarValues { get; init; }

    public double? YMin { get; init; }

    public double? YMax { get; init; }

    public BarDisplayMode DisplayMode { get; init; }

    public IReadOnlyList<ChartDataPoint> Data { get; init; } = [];

    public string IncrementalState { get; init; } = "{}";

    public (double Min, double Max) GetValueExtent()
    {
        if (Data.Count == 0)
        {
            return (double.NaN, double.NaN);
        }

        var min = double.MaxValue;
        var max = double.MinValue;
        foreach (var point in Data)
        {
            min = Math.Min(min, point.Value);
            max = Math.Max(max, point.Value);
        }

        return (min, max);
    }

    /// <summary>Returns the sum of the positive values: the whole of a part-to-whole chart.</summary>
    public double GetPositiveTotal()
    {
        var total = 0d;
        foreach (var point in Data)
        {
            total += Math.Max(0, point.Value);
        }

        return total;
    }

    public static BarChartModel Parse(string elementJson, BarOrientation orientation, ICollection<string> warnings)
    {
        try
        {
            using var document = JsonDocument.Parse(elementJson);
            var element = document.RootElement;
            if (element.ValueKind == JsonValueKind.Object && !element.TryGetProperty("data", out _))
            {
                warnings.Add("data is required.");
            }

            var isVertical = orientation == BarOrientation.Vertical;
            return new BarChartModel
            {
                Orientation = orientation,
                Title = ChartJson.GetString(element, "title"),
                ShowTitle = ChartJson.GetBoolean(element, "showTitle") ?? false,
                XAxisTitle = ChartJson.GetString(element, "xAxisTitle"),
                YAxisTitle = ChartJson.GetString(element, "yAxisTitle"),
                Color = ChartJson.GetString(element, "color"),
                ColorSet = ChartJson.GetString(element, "colorSet"),
                ShowBarValues = isVertical && (ChartJson.GetBoolean(element, "showBarValues") ?? false),
                YMin = isVertical ? ChartJson.GetNumber(element, "yMin") : null,
                YMax = isVertical ? ChartJson.GetNumber(element, "yMax") : null,
                DisplayMode = isVertical
                    ? BarDisplayMode.AbsoluteWithAxis
                    : ChartJson.GetEnum(element, "displayMode", BarDisplayMode.AbsoluteWithAxis, warnings),
                Data = ChartJson.GetDataPoints(element, "data", "x", "y", warnings),
                IncrementalState = ChartJson.Canonicalize(element),
            };
        }
        catch (JsonException ex)
        {
            warnings.Add($"The element JSON could not be read: {ex.Message}");
            return new BarChartModel { Orientation = orientation };
        }
    }
}
