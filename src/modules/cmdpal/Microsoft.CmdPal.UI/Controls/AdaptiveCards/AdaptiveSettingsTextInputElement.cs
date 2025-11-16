// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Data.Json;

#pragma warning disable SA1402

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards;

public partial class AdaptiveSettingsTextInputElement : IAdaptiveInputElement, IAdaptiveCardElement
{
    public const string CustomInputType = "SettingsCard.Input.Text";

    public string? Description { get; set; }

    public string? Value { get; set; }

    public string? Header { get; set; }

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
}

public partial class AdaptiveSettingsTextInputParser : IAdaptiveElementParser
{
    public IAdaptiveCardElement FromJson(
        JsonObject inputJson,
        AdaptiveElementParserRegistration elementParsers,
        AdaptiveActionParserRegistration actionParsers,
        IList<AdaptiveWarning> warnings)
    {
        return new AdaptiveSettingsTextInputElement
        {
            Id = inputJson.GetNamedString("id"),
            Label = inputJson.GetNamedString("label"),
            Header = inputJson.GetNamedString("header"),
            Description = inputJson.GetNamedString("description"),
            IsRequired = inputJson.GetNamedBoolean("isRequired"),
            ErrorMessage = inputJson.GetNamedString("errorMessage"),
            Value = inputJson.GetNamedString("value"),
        };
    }
}

public class TextBoxWithValue : IAdaptiveInputValue
{
    public TextBoxWithValue(AdaptiveSettingsTextInputElement element, SettingsCard card)
    {
        InputElement = element;
        Card = card;
    }

    public SettingsCard Card { get; }

    public bool Validate() => true;

    public void SetFocus() => Card.Focus(FocusState.Programmatic);

    public UIElement? ErrorMessage { get; set; }

    public IAdaptiveInputElement InputElement { get; set; }

    public string CurrentValue
    {
        get
        {
            return (Card.Content as TextBox)?.Text ?? string.Empty;
        }
    }
}

public class AdaptiveSettingsTextInputElementRenderer : IAdaptiveElementRenderer
{
    public UIElement Render(IAdaptiveCardElement element, AdaptiveRenderContext context, AdaptiveRenderArgs renderArgs)
    {
        var item = element as AdaptiveSettingsTextInputElement;

        var content = new TextBox
        {
            Text = item?.Value ?? string.Empty,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var settingsCard = new SettingsCard
        {
            Header = item?.Header ?? string.Empty,
            Description = item?.Description ?? string.Empty,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = content,
        };

        if (item != null)
        {
            context.AddInputValue(new TextBoxWithValue(item, settingsCard), renderArgs);
        }

        return settingsCard;
    }
}
