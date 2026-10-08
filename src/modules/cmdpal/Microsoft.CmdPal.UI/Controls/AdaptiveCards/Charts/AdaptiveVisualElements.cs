// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using Microsoft.CmdPal.AdaptiveCards.IncrementalRendering;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>
/// The Adaptive Cards elements that Command Palette renders natively because the WinUI 3
/// renderer doesn't support them. Names and properties follow the Adaptive Cards schema, so any
/// extension can use them and the same JSON renders in other hosts.
/// </summary>
internal static class AdaptiveVisualElements
{
    public static IReadOnlyList<AdaptiveVisualElementType> Types { get; } =
    [
        new("Chart.Line", (json, warnings) => LineChartModel.Parse(json, warnings), model => new LineChartControl((LineChartModel)model)),
        new("Chart.Gauge", (json, warnings) => GaugeChartModel.Parse(json, warnings), model => new GaugeChartControl((GaugeChartModel)model)),
        new("Chart.Donut", (json, warnings) => DonutChartModel.Parse(json, isPie: false, warnings), model => new DonutChartControl((DonutChartModel)model)),
        new("Chart.Pie", (json, warnings) => DonutChartModel.Parse(json, isPie: true, warnings), model => new DonutChartControl((DonutChartModel)model)),
        new("Chart.HorizontalBar.Stacked", (json, warnings) => StackedBarChartModel.Parse(json, warnings), model => new StackedBarChartControl((StackedBarChartModel)model)),
        new("Chart.VerticalBar", (json, warnings) => BarChartModel.Parse(json, BarOrientation.Vertical, warnings), model => new BarChartControl((BarChartModel)model)),
        new("Chart.HorizontalBar", (json, warnings) => BarChartModel.Parse(json, BarOrientation.Horizontal, warnings), model => new BarChartControl((BarChartModel)model)),
        new("ProgressBar", (json, warnings) => ProgressBarModel.Parse(json, warnings), model => new ProgressBarControl((ProgressBarModel)model)),
        new("Badge", (json, warnings) => BadgeModel.Parse(json, warnings), model => new BadgeControl((BadgeModel)model)),
    ];

    /// <summary>Gets the registration that lets these elements update in place.</summary>
    public static IncrementalPatchableElements PatchableElements { get; } = CreatePatchableElements();

    public static void RegisterParsers(AdaptiveElementParserRegistration parsers)
    {
        foreach (var type in Types)
        {
            parsers.Set(type.Name, new AdaptiveVisualElementParser(type));
        }
    }

    public static void RegisterRenderers(AdaptiveCardRenderer renderer)
    {
        foreach (var type in Types)
        {
            renderer.ElementRenderers.Set(type.Name, new AdaptiveVisualElementRenderer(type));
        }
    }

    private static IncrementalPatchableElements CreatePatchableElements()
    {
        var elements = new IncrementalPatchableElements();
        foreach (var type in Types)
        {
            elements.Add(type.Name);
        }

        return elements;
    }
}
