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

internal sealed class AdaptiveSettingsCheckBoxInputElement : IAdaptiveInputElement, IAdaptiveCardElement, ICustomAdaptiveCardElement
{
    public static string CustomInputType => "SettingsCard.Input.CheckBox";

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

    public string? Header { get; set; }
}

internal sealed class AdaptiveSettingsCheckBoxInputParser : IAdaptiveElementParser
{
    public IAdaptiveCardElement FromJson(JsonObject inputJson, AdaptiveElementParserRegistration elementParsers, AdaptiveActionParserRegistration actionParsers, IList<AdaptiveWarning> warnings)
    {
        return new AdaptiveSettingsCheckBoxInputElement
        {
            Id = inputJson.GetNamedString("id"),
            Label = inputJson.GetNamedString("label"),
            Header = inputJson.GetNamedString("header"),
            Description = inputJson.GetNamedString("description"),
            IsRequired = inputJson.GetNamedBoolean("isRequired"),
            ErrorMessage = inputJson.GetNamedString("errorMessage"),
            Value = bool.TryParse(inputJson.GetNamedString("value"), out var result) && result,
        };
    }
}

internal sealed class CheckBoxInputValue : IAdaptiveInputValue
{
    public SettingsCard Card { get; }

    public bool Validate() => true;

    public void SetFocus() => Card.Focus(FocusState.Programmatic);

    public UIElement? ErrorMessage { get; set; }

    public IAdaptiveInputElement InputElement { get; set; }

    public string CurrentValue
    {
        get
        {
            var result = ((Card.Content as CheckBoxWithDescriptionControl)?.IsChecked ?? false) ? "true" : "false";
            return result;
        }
    }

    public CheckBoxInputValue(AdaptiveSettingsCheckBoxInputElement element, SettingsCard card)
    {
        InputElement = element;
        Card = card;
    }
}

internal sealed class AdaptiveSettingsCheckBoxInputRenderer : IAdaptiveElementRenderer
{
    public UIElement Render(IAdaptiveCardElement element, AdaptiveRenderContext context, AdaptiveRenderArgs renderArgs)
    {
        var item = element as AdaptiveSettingsCheckBoxInputElement;

        var content = new CheckBoxWithDescriptionControl
        {
            IsChecked = item?.Value ?? false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Header = item?.Header ?? string.Empty,
            Description = item?.Description ?? string.Empty,
        };

        var settingsCard = new SettingsCard
        {
            ContentAlignment = ContentAlignment.Left,
            Content = content,
        };

        if (item != null)
        {
            context.AddInputValue(new CheckBoxInputValue(item, settingsCard), renderArgs);
        }

        return settingsCard;
    }
}
