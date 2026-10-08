// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

internal enum BarOrientation
{
    Vertical,
    Horizontal,
}

/// <summary>
/// The parsed form of an Adaptive Cards <c>Chart.VerticalBar</c> or <c>Chart.HorizontalBar</c>
/// element: <c>data</c> (<c>x</c>, <c>y</c>, <c>color</c>), <c>title</c>, <c>xAxisTitle</c>,
/// <c>yAxisTitle</c>, <c>color</c>, <c>colorSet</c>, and <c>showBarValues</c>. Command Palette
/// also reads <c>yMin</c>, <c>yMax</c>, and <c>valueFormat</c>, as for <c>Chart.Line</c>.
/// </summary>
internal sealed class BarChartModel : IAdaptiveVisualModel
{
    public BarOrientation Orientation { get; init; }

    public string? Title { get; init; }

    public string? XAxisTitle { get; init; }

    public string? YAxisTitle { get; init; }

    public string? Color { get; init; }

    public string? ColorSet { get; init; }

    public bool ShowBarValues { get; init; }

    public double? YMin { get; init; }

    public double? YMax { get; init; }

    public ChartValueFormat ValueFormat { get; init; }

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

            return new BarChartModel
            {
                Orientation = orientation,
                Title = ChartJson.GetString(element, "title"),
                XAxisTitle = ChartJson.GetString(element, "xAxisTitle"),
                YAxisTitle = ChartJson.GetString(element, "yAxisTitle"),
                Color = ChartJson.GetString(element, "color"),
                ColorSet = ChartJson.GetString(element, "colorSet"),
                ShowBarValues = ChartJson.GetBoolean(element, "showBarValues") ?? false,
                YMin = ChartJson.GetNumber(element, "yMin"),
                YMax = ChartJson.GetNumber(element, "yMax"),
                ValueFormat = ChartJson.GetEnum(element, "valueFormat", ChartValueFormat.Number, warnings),
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
