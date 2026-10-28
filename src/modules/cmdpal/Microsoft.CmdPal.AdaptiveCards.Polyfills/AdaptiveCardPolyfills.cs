// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using Microsoft.CmdPal.AdaptiveCards.IncrementalRendering;

namespace Microsoft.CmdPal.AdaptiveCards.Polyfills;

/// <summary>
/// Adds the schema features that the WinUI 3 renderer doesn't support yet: the <c>Chart.Line</c> element.
/// </summary>
public static class AdaptiveCardPolyfills
{
    private static readonly AdaptiveVisualElementType[] Types =
    [
        new("Chart.Line", (json, warnings) => LineChartModel.Parse(json, warnings), model => new LineChartControl((LineChartModel)model)),
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

    /// <summary>Registers the element renderers.</summary>
    /// <param name="getString">Gets a localized string by its resource name, such as <c>AdaptiveChart_LineChart</c>.</param>
    public static void RegisterRenderers(AdaptiveCardRenderer renderer, Func<string, string> getString)
    {
        ChartStrings.SetLookup(getString);
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
