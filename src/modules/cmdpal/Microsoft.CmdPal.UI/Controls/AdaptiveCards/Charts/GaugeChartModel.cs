// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

internal enum GaugeValueFormat
{
    Percentage,
    Fraction,
}

/// <summary>
/// The parsed form of an Adaptive Cards <c>Chart.Gauge</c> element: <c>value</c>, <c>min</c>,
/// <c>max</c>, <c>segments</c>, <c>valueFormat</c>, <c>subLabel</c>, <c>showLegend</c>,
/// <c>showMinMax</c>, <c>title</c>, and <c>colorSet</c>. Command Palette also reads <c>color</c>
/// for the value arc of a gauge without segments.
/// </summary>
internal sealed class GaugeChartModel : IAdaptiveVisualModel
{
    public string? Title { get; init; }

    public string? SubLabel { get; init; }

    public double Value { get; init; }

    public double Min { get; init; }

    public double Max { get; init; } = 100;

    public GaugeValueFormat ValueFormat { get; init; }

    public bool ShowLegend { get; init; } = true;

    public bool ShowMinMax { get; init; } = true;

    public string? Color { get; init; }

    public string? ColorSet { get; init; }

    public IReadOnlyList<ChartDataPoint> Segments { get; init; } = [];

    public string IncrementalState { get; init; } = "{}";

    /// <summary>Gets the value's position between the minimum (0) and the maximum (1).</summary>
    public double Fraction => Max > Min ? Math.Clamp((Value - Min) / (Max - Min), 0, 1) : 0;

    public string FormatValue(IFormatProvider culture) =>
        ValueFormat == GaugeValueFormat.Fraction
            ? string.Format(culture, "{0:0.##}/{1:0.##}", Value, Max)
            : string.Format(culture, "{0:0}%", Fraction * 100);

    /// <summary>Returns the index of the segment that contains the value, or -1.</summary>
    public int GetActiveSegment()
    {
        var start = Min;
        for (var i = 0; i < Segments.Count; i++)
        {
            var end = start + Segments[i].Value;
            if (Value >= start && (Value < end || i == Segments.Count - 1))
            {
                return i;
            }

            start = end;
        }

        return -1;
    }

    public static GaugeChartModel Parse(string elementJson, ICollection<string> warnings)
    {
        try
        {
            using var document = JsonDocument.Parse(elementJson);
            var element = document.RootElement;

            // The schema documents the segment size as "value"; Teams samples use "size".
            var segments = ChartJson.GetDataPoints(element, "segments", "legend", "size", warnings, fallbackValueProperty: "value");
            var min = ChartJson.GetNumber(element, "min") ?? 0;
            var segmentTotal = 0d;
            foreach (var segment in segments)
            {
                segmentTotal += Math.Max(0, segment.Value);
            }

            var max = ChartJson.GetNumber(element, "max") ?? (segmentTotal > 0 ? min + segmentTotal : 100);
            if (max <= min)
            {
                warnings.Add("max must be greater than min.");
                max = min + 100;
            }

            return new GaugeChartModel
            {
                Title = ChartJson.GetString(element, "title"),
                SubLabel = ChartJson.GetString(element, "subLabel"),
                Value = ChartJson.GetNumber(element, "value") ?? 0,
                Min = min,
                Max = max,
                ValueFormat = ChartJson.GetEnum(element, "valueFormat", GaugeValueFormat.Percentage, warnings),
                ShowLegend = ChartJson.GetBoolean(element, "showLegend") ?? true,
                ShowMinMax = ChartJson.GetBoolean(element, "showMinMax") ?? true,
                Color = ChartJson.GetString(element, "color"),
                ColorSet = ChartJson.GetString(element, "colorSet"),
                Segments = segments,
                IncrementalState = ChartJson.Canonicalize(element),
            };
        }
        catch (JsonException ex)
        {
            warnings.Add($"The element JSON could not be read: {ex.Message}");
            return new GaugeChartModel();
        }
    }
}
