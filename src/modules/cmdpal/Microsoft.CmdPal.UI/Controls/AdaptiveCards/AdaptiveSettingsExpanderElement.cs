// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Data.Json;

#pragma warning disable SA1402 // File may only contain a single type

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards;

internal sealed partial class AdaptiveSettingsExpanderElement : IAdaptiveInputElement, IAdaptiveCardElement, ICustomAdaptiveCardElement
{
    public static string CustomInputType => "SettingsCard.Expander";

    public string? Header { get; set; }

    public string? Description { get; set; }

    public bool Value { get; set; }

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

    public string? ErrorMessage { get; set; }

    public bool IsRequired { get; set; }

    public string? Label { get; set; }

    public object[] Items { get; set; } = [];
}

internal sealed partial class AdaptiveSettingsExpanderElementParser : IAdaptiveElementParser
{
    public IAdaptiveCardElement FromJson(JsonObject inputJson, AdaptiveElementParserRegistration elementParsers, AdaptiveActionParserRegistration actionParsers, IList<AdaptiveWarning> warnings)
    {
        var expander = new AdaptiveSettingsExpanderElement
        {
            Id = inputJson.GetNamedString("id"),
            Label = inputJson.GetNamedString("label"),
            Header = inputJson.GetNamedString("header"),
            Description = inputJson.GetNamedString("description"),
            IsRequired = inputJson.GetNamedBoolean("isRequired"),
            ErrorMessage = inputJson.GetNamedString("errorMessage"),
            Value = bool.TryParse(inputJson.GetNamedString("value"), out var result) && result,
        };

        // parse items
        if (inputJson.TryGetValue("items", out var itemsValue) && itemsValue.ValueType == JsonValueType.Array)
        {
            var itemsArray = itemsValue.GetArray();
            var itemsList = new List<object>();
            foreach (var itemValue in itemsArray)
            {
                if (itemValue.ValueType == JsonValueType.Object)
                {
                    var itemJson = itemValue.GetObject();
                    var elementType = itemJson.GetNamedString("type");
                    var parser = elementParsers.Get(elementType);
                    var element = parser.FromJson(itemJson, elementParsers, actionParsers, warnings);
                    itemsList.Add(element);
                }
            }

            expander.Items = itemsList.ToArray();
        }

        return expander;
    }
}

internal sealed partial class AdaptiveSettingsExpanderElementRenderer : IAdaptiveElementRenderer
{
    public UIElement Render(IAdaptiveCardElement element, AdaptiveRenderContext context, AdaptiveRenderArgs renderArgs)
    {
        var item = element as AdaptiveSettingsExpanderElement;

        var items = item?.Items ?? [];
        var itemsUIElement = new List<object>();

        foreach (var subItem in items.OfType<IAdaptiveCardElement>())
        {
            var renderer = context.ElementRenderers.Get(subItem.ElementTypeString);
            var uiElement = renderer.Render(subItem, context, renderArgs);
            itemsUIElement.Add(uiElement);
        }

        var content = new ToggleSwitch
        {
            IsOn = item?.Value ?? false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var settingsCard = new SettingsExpander
        {
            Header = item?.Header ?? string.Empty,
            Description = item?.Description ?? string.Empty,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = content,
            Items = itemsUIElement,
        };

        return settingsCard;
    }
}
