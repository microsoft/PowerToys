// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Windows.Data.Json;

#pragma warning disable SA1402 // File may only contain a single type

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards;

internal sealed partial class AdaptiveSettingsCardContainerElement : IAdaptiveCardElement, ICustomAdaptiveCardElement
{
    public static string CustomInputType => "SettingsCard.Container";

    public string? Header { get; set; }

    public string? Description { get; set; }

    public IAdaptiveCardElement? Control { get; set; }

    public JsonObject ToJson() => throw new NotImplementedException();

    public JsonObject? AdditionalProperties { get; set; }

    public ElementType ElementType { get; } = ElementType.Custom;

    public string ElementTypeString { get; } = CustomInputType;

    public IAdaptiveCardElement? FallbackContent { get; set; }

    public FallbackType FallbackType { get; set; }

    public HeightType Height { get; set; }

    public string? Id { get; set; }

    public bool IsVisible { get; set; } = true;

    public IList<AdaptiveRequirement> Requirements { get; } = [];

    public bool Separator { get; set; }

    public Spacing Spacing { get; set; }
}

internal sealed partial class AdaptiveSettingsCardContainerElementParser : IAdaptiveElementParser
{
    public IAdaptiveCardElement FromJson(
        JsonObject inputJson,
        AdaptiveElementParserRegistration elementParsers,
        AdaptiveActionParserRegistration actionParsers,
        IList<AdaptiveWarning> warnings)
    {
        var element = new AdaptiveSettingsCardContainerElement
        {
            Id = inputJson.GetNamedString("id", string.Empty),
            Header = inputJson.GetNamedString("header", string.Empty),
            Description = inputJson.GetNamedString("description", string.Empty),
        };

        if (inputJson.TryGetValue("content", out var contentValue) &&
            contentValue.ValueType == JsonValueType.Object)
        {
            var contentJson = contentValue.GetObject();
            var elementType = contentJson.GetNamedString("type");
            var parser = elementParsers.Get(elementType);
            var control = parser.FromJson(contentJson, elementParsers, actionParsers, warnings);
            element.Control = control;
        }

        return element;
    }
}

internal sealed partial class AdaptiveSettingsCardContainerElementRenderer : IAdaptiveElementRenderer
{
    public UIElement Render(IAdaptiveCardElement element, AdaptiveRenderContext context, AdaptiveRenderArgs renderArgs)
    {
        var container = element as AdaptiveSettingsCardContainerElement;

        UIElement? content = null;

        if (container?.Control is IAdaptiveCardElement control)
        {
            var renderer = context.ElementRenderers.Get(control.ElementTypeString);
            content = renderer.Render(control, context, renderArgs);
        }

        var settingsCard = new SettingsCard
        {
            Header = container?.Header ?? string.Empty,
            Description = container?.Description ?? string.Empty,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = content,
        };

        return settingsCard;
    }
}
