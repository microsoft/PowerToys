// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>
/// The parsed form of an Adaptive Cards <c>Chart.Donut</c> or <c>Chart.Pie</c> element, with the
/// schema's defaults.
/// </summary>
internal sealed class DonutChartModel : IAdaptiveVisualModel
{
    public bool IsPie { get; init; }

    public string? Title { get; init; }

    public bool ShowTitle { get; init; }

    public string? CenterLabel { get; init; }

    public string? ColorSet { get; init; }

    public bool ShowLegend { get; init; } = true;

    public IReadOnlyList<ChartDataPoint> Data { get; init; } = [];

    public string IncrementalState { get; init; } = "{}";

    public double Total
    {
        get
        {
            var total = 0d;
            foreach (var point in Data)
            {
                total += Math.Max(0, point.Value);
            }

            return total;
        }
    }

    /// <summary>Returns the share of the total for each slice, in data order.</summary>
    public IReadOnlyList<double> GetShares()
    {
        var total = Total;
        var shares = new double[Data.Count];
        for (var i = 0; i < Data.Count; i++)
        {
            shares[i] = total > 0 ? Math.Max(0, Data[i].Value) / total : 0;
        }

        return shares;
    }

    public static DonutChartModel Parse(string elementJson, bool isPie, ICollection<string> warnings)
    {
        try
        {
            using var document = JsonDocument.Parse(elementJson);
            var element = document.RootElement;
            if (element.ValueKind == JsonValueKind.Object && !element.TryGetProperty("data", out _))
            {
                warnings.Add("data is required.");
            }

            var centerLabel = element.ValueKind == JsonValueKind.Object && element.TryGetProperty("value", out var value)
                ? ChartJson.ToLabel(value)
                : null;
            return new DonutChartModel
            {
                IsPie = isPie,
                Title = ChartJson.GetString(element, "title"),
                ShowTitle = ChartJson.GetBoolean(element, "showTitle") ?? false,
                CenterLabel = isPie ? null : centerLabel,
                ColorSet = ChartJson.GetString(element, "colorSet"),
                ShowLegend = ChartJson.GetBoolean(element, "showLegend") ?? true,
                Data = ChartJson.GetDataPoints(element, "data", "legend", "value", warnings),
                IncrementalState = ChartJson.Canonicalize(element),
            };
        }
        catch (JsonException ex)
        {
            warnings.Add($"The element JSON could not be read: {ex.Message}");
            return new DonutChartModel { IsPie = isPie };
        }
    }
}
