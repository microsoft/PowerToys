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

internal sealed partial class AdaptiveSettingsComboBoxInputElement : IAdaptiveInputElement, IAdaptiveCardElement, ICustomAdaptiveCardElement
{
    public static string CustomInputType => "SettingsCard.Input.ComboBox";

    public string? Description { get; set; }

    public string? Value { get; set; }

    public IList<AdaptiveChoice> Choices { get; set; } = [];

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

internal sealed partial class AdaptiveSettingsComboBoxInputParser : IAdaptiveElementParser
{
    public IAdaptiveCardElement FromJson(
        JsonObject inputJson,
        AdaptiveElementParserRegistration elementParsers,
        AdaptiveActionParserRegistration actionParsers,
        IList<AdaptiveWarning> warnings)
    {
        var element = new AdaptiveSettingsComboBoxInputElement
        {
            Id = inputJson.GetNamedString("id"),
            Label = inputJson.GetNamedString("label"),
            Header = inputJson.GetNamedString("header"),
            Description = inputJson.GetNamedString("description"),
            IsRequired = inputJson.GetNamedBoolean("isRequired", false),
            ErrorMessage = inputJson.GetNamedString("errorMessage"),
            Value = inputJson.GetNamedString("value"),
        };

        if (inputJson.TryGetValue("choices", out var choicesValue) &&
            choicesValue.ValueType == JsonValueType.Array)
        {
            var choicesArray = choicesValue.GetArray();
            foreach (var item in choicesArray)
            {
                var choiceObj = item.GetObject();
                var title = choiceObj.GetNamedString("title");
                var value = choiceObj.GetNamedString("value");
                element.Choices.Add(new AdaptiveChoice(title, value));
            }
        }

        return element;
    }
}

internal sealed partial class ComboBoxWithValue : IAdaptiveInputValue
{
    public ComboBoxWithValue(AdaptiveSettingsComboBoxInputElement element, SettingsCard card)
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
            var combo = Card.Content as ComboBox;
            if (combo is null)
            {
                return string.Empty;
            }

            if (combo.SelectedValue is not null)
            {
                return combo.SelectedValue.ToString() ?? string.Empty;
            }

            if (combo.SelectedItem is AdaptiveChoice choice)
            {
                return choice.Value ?? string.Empty;
            }

            return string.Empty;
        }
    }
}

internal sealed partial class AdaptiveSettingsComboBoxInputRenderer : IAdaptiveElementRenderer
{
    public UIElement Render(IAdaptiveCardElement element, AdaptiveRenderContext context, AdaptiveRenderArgs renderArgs)
    {
        var item = element as AdaptiveSettingsComboBoxInputElement;

        var combo = new ComboBox
        {
            ItemsSource = item?.Choices,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            DisplayMemberPath = nameof(AdaptiveChoice.Title),
            SelectedValuePath = nameof(AdaptiveChoice.Value),
        };

        // Set initial selected value (if any)
        if (!string.IsNullOrEmpty(item?.Value))
        {
            combo.SelectedValue = item.Value;
        }

        var settingsCard = new SettingsCard
        {
            Header = item?.Header ?? string.Empty,
            Description = item?.Description ?? string.Empty,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = combo,
        };

        if (item != null)
        {
            context.AddInputValue(new ComboBoxWithValue(item, settingsCard), renderArgs);
        }

        return settingsCard;
    }
}

internal sealed record AdaptiveChoice(string? Title, string? Value);
