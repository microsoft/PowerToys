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
/// An Adaptive Cards element from the current schema (charts, progress bars, badges) that the
/// WinUI 3 renderer doesn't support. Command Palette parses and renders it natively. Cards stay
/// portable: other hosts render the same JSON or use the element's <c>fallback</c>.
/// </summary>
internal sealed partial class AdaptiveVisualElement : IAdaptiveCardElement
{
    public AdaptiveVisualElement(string elementType)
    {
        ElementTypeString = elementType;
    }

    public IAdaptiveVisualModel? Model { get; set; }

    public JsonObject ToJson() => AdaptiveCustomElementJson.Create(this);

    public JsonObject? AdditionalProperties { get; set; }

    public ElementType ElementType { get; } = ElementType.Custom;

    public string ElementTypeString { get; }

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

internal sealed partial class AdaptiveVisualElementParser : IAdaptiveElementParser
{
    private readonly AdaptiveVisualElementType _type;

    public AdaptiveVisualElementParser(AdaptiveVisualElementType type)
    {
        _type = type;
    }

    public IAdaptiveCardElement FromJson(
        JsonObject inputJson,
        AdaptiveElementParserRegistration elementParsers,
        AdaptiveActionParserRegistration actionParsers,
        IList<AdaptiveWarning> warnings)
    {
        var element = new AdaptiveVisualElement(_type.Name);
        AdaptiveCustomElementJson.ParseCommonProperties(
            element,
            inputJson,
            elementParsers,
            actionParsers,
            warnings,
            requireId: false);

        var modelWarnings = new List<string>();
        element.Model = _type.Parse(inputJson.Stringify(), modelWarnings);
        foreach (var warning in modelWarnings)
        {
            warnings.Add(new AdaptiveWarning(WarningStatusCode.InvalidValue, $"{_type.Name}: {warning}"));
        }

        return element;
    }
}

internal sealed partial class AdaptiveVisualElementRenderer : IAdaptiveElementRenderer
{
    private readonly AdaptiveVisualElementType _type;

    public AdaptiveVisualElementRenderer(AdaptiveVisualElementType type)
    {
        _type = type;
    }

    public UIElement Render(IAdaptiveCardElement element, AdaptiveRenderContext context, AdaptiveRenderArgs renderArgs)
    {
        var model = ((AdaptiveVisualElement)element).Model ?? _type.Parse("{}", new List<string>());
        return _type.Create(model);
    }
}

#pragma warning restore SA1402 // File may only contain a single type
