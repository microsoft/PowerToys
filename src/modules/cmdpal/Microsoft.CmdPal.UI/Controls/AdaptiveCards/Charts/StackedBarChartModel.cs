// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>
/// The parsed form of an Adaptive Cards <c>Chart.HorizontalBar.Stacked</c> element, with the
/// schema's defaults: <c>data</c> is a list of bars, each with a <c>title</c> and its own
/// <c>data</c> of <c>legend</c>, <c>value</c>, and <c>color</c>. Also reads <c>title</c>,
/// <c>showTitle</c>, <c>color</c>, <c>colorSet</c>, and <c>showLegend</c>.
/// </summary>
internal sealed class StackedBarChartModel : IAdaptiveVisualModel
{
    public string? Title { get; init; }

    public bool ShowTitle { get; init; }

    public string? Color { get; init; }

    public string? ColorSet { get; init; }

    public bool ShowLegend { get; init; } = true;

    public IReadOnlyList<StackedBarGroup> Groups { get; init; } = [];

    public string IncrementalState { get; init; } = "{}";

    /// <summary>Gets the total of the longest bar, which spans the full width.</summary>
    public double MaxTotal
    {
        get
        {
            var max = 0d;
            foreach (var group in Groups)
            {
                max = Math.Max(max, group.Total);
            }

            return max;
        }
    }

    /// <summary>
    /// Returns each distinct legend in first-seen order, with the color index the chart uses for it,
    /// so the same legend has the same color in every bar.
    /// </summary>
    public IReadOnlyList<(string Legend, string? Color, int ColorIndex)> GetLegend()
    {
        var legend = new List<(string Legend, string? Color, int ColorIndex)>();
        foreach (var group in Groups)
        {
            foreach (var point in group.Data)
            {
                var name = point.Label ?? string.Empty;
                if (legend.FindIndex(item => string.Equals(item.Legend, name, StringComparison.Ordinal)) < 0)
                {
                    legend.Add((name, point.Color, legend.Count));
                }
            }
        }

        return legend;
    }

    public static StackedBarChartModel Parse(string elementJson, ICollection<string> warnings)
    {
        try
        {
            using var document = JsonDocument.Parse(elementJson);
            var element = document.RootElement;
            var groups = new List<StackedBarGroup>();
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("data", out var data))
            {
                if (data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in data.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object)
                        {
                            warnings.Add("Each data item must be an object with a data array.");
                            continue;
                        }

                        groups.Add(new StackedBarGroup(
                            ChartJson.GetString(item, "title"),
                            ChartJson.GetDataPoints(item, "data", "legend", "value", warnings)));
                    }
                }
                else
                {
                    warnings.Add("data must be an array.");
                }
            }
            else
            {
                warnings.Add("data is required.");
            }

            return new StackedBarChartModel
            {
                Title = ChartJson.GetString(element, "title"),
                ShowTitle = ChartJson.GetBoolean(element, "showTitle") ?? false,
                Color = ChartJson.GetString(element, "color"),
                ColorSet = ChartJson.GetString(element, "colorSet"),
                ShowLegend = ChartJson.GetBoolean(element, "showLegend") ?? true,
                Groups = groups,
                IncrementalState = ChartJson.Canonicalize(element),
            };
        }
        catch (JsonException ex)
        {
            warnings.Add($"The element JSON could not be read: {ex.Message}");
            return new StackedBarChartModel();
        }
    }
}
