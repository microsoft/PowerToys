// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using Microsoft.UI.Xaml;
using Windows.Data.Json;

#pragma warning disable SA1402 // File may only contain a single type

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>
/// The Adaptive Cards <c>Chart.Line</c> element. The WinUI 3 renderer has no chart support, so
/// Command Palette parses and renders it as a custom element. Cards stay portable: hosts that
/// support charts render the same JSON, and other hosts use the element's <c>fallback</c>.
/// </summary>
internal sealed partial class AdaptiveLineChartElement : IAdaptiveCardElement, ICustomAdaptiveCardElement
{
    public static string CustomInputType => "Chart.Line";

    public LineChartModel Model { get; set; } = LineChartModel.Empty;

    public JsonObject ToJson() => AdaptiveCustomElementJson.Create(this);

    public JsonObject? AdditionalProperties { get; set; }

    public ElementType ElementType { get; } = ElementType.Custom;

    public string ElementTypeString => CustomInputType;

    public IAdaptiveCardElement? FallbackContent { get; set; }

    public FallbackType FallbackType { get; set; }

    public HeightType Height { get; set; }

    public string? Id { get; set; }

    public bool IsVisible { get; set; } = true;

    // Use an explicit constructor so CsWinRT generates AOT collection marshalling.
    public IList<AdaptiveRequirement> Requirements { get; } = new List<AdaptiveRequirement>();

    public bool Separator { get; set; }

    public Spacing Spacing { get; set; }
}

internal sealed partial class AdaptiveLineChartElementParser : IAdaptiveElementParser
{
    public IAdaptiveCardElement FromJson(
        JsonObject inputJson,
        AdaptiveElementParserRegistration elementParsers,
        AdaptiveActionParserRegistration actionParsers,
        IList<AdaptiveWarning> warnings)
    {
        var element = new AdaptiveLineChartElement();
        AdaptiveCustomElementJson.ParseCommonProperties(
            element,
            inputJson,
            elementParsers,
            actionParsers,
            warnings,
            requireId: false);

        var modelWarnings = new List<string>();
        element.Model = LineChartModel.Parse(inputJson.Stringify(), modelWarnings);
        foreach (var warning in modelWarnings)
        {
            warnings.Add(new AdaptiveWarning(
                WarningStatusCode.InvalidValue,
                $"{AdaptiveLineChartElement.CustomInputType}: {warning}"));
        }

        return element;
    }
}

internal sealed partial class AdaptiveLineChartElementRenderer : IAdaptiveElementRenderer
{
    public UIElement Render(IAdaptiveCardElement element, AdaptiveRenderContext context, AdaptiveRenderArgs renderArgs) =>
        new LineChartControl(((AdaptiveLineChartElement)element).Model);
}

#pragma warning restore SA1402 // File may only contain a single type
