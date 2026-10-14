// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using Microsoft.CmdPal.AdaptiveCards.IncrementalRendering;

namespace Microsoft.CmdPal.AdaptiveCards.Polyfills;

/// <summary>
/// Adds the schema features that the WinUI 3 renderer doesn't support yet: the chart, <c>ProgressBar</c>,
/// <c>Badge</c>, and <c>Icon</c> elements, and <c>roundedCorners</c> and <c>showBorder</c> on containers.
/// </summary>
public static class AdaptiveCardPolyfills
{
    private static readonly AdaptiveVisualElementType[] Types =
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
        new("Icon", (json, warnings) => IconModel.Parse(json, warnings), model => new IconControl((IconModel)model), IconModel.HasGlyph),
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

    /// <summary>
    /// Registers the element renderers, and wraps the container renderers to draw <c>roundedCorners</c> and
    /// <c>showBorder</c> with the <c>CmdPal.Adaptive.Container.*</c> styles in the renderer's override styles.
    /// </summary>
    /// <param name="getString">Gets a localized string by its resource name, such as <c>AdaptiveChart_LineChart</c>.</param>
    public static void RegisterRenderers(AdaptiveCardRenderer renderer, Func<string, string> getString)
    {
        ChartStrings.SetLookup(getString);
        foreach (var type in Types)
        {
            renderer.ElementRenderers.Set(type.Name, new AdaptiveVisualElementRenderer(type));
        }

        AdaptiveContainerDecoratorRenderer.Register(renderer);
    }

    private static IncrementalPatchableElements CreatePatchableElements()
    {
        var elements = new IncrementalPatchableElements();
        foreach (var type in Types)
        {
            if (type.RendersItself is { } rendersItself)
            {
                elements.Add(type.Name, rendersItself);
            }
            else
            {
                elements.Add(type.Name);
            }
        }

        return elements;
    }
}
