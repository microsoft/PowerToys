// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Data.Json;

#pragma warning disable SA1402

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards;

internal sealed partial class AdaptiveSettingsTextInputElement : IAdaptiveInputElement, IAdaptiveCardElement, ICustomAdaptiveCardElement
{
    public static string CustomInputType => "SettingsCard.Input.Text";

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

    public string? Placeholder { get; set; }
}

public partial class AdaptiveSettingsTextInputParser : IAdaptiveElementParser
{
    public IAdaptiveCardElement FromJson(
        JsonObject inputJson,
        AdaptiveElementParserRegistration elementParsers,
        AdaptiveActionParserRegistration actionParsers,
        IList<AdaptiveWarning> warnings)
    {
        var json = new AdaptiveSettingsTextInputElement();
        json.Id = inputJson.GetNamedString("id");
        json.Label = inputJson.GetNamedString("label", string.Empty);
        json.Header = inputJson.GetNamedString("header", string.Empty);
        json.Description = inputJson.GetNamedString("description", string.Empty);
        json.IsRequired = inputJson.GetNamedBoolean("isRequired", false);
        json.ErrorMessage = inputJson.GetNamedString("errorMessage", string.Empty);
        json.Value = inputJson.GetNamedString("value", string.Empty);
        json.Placeholder = inputJson.GetNamedString("placeholder", string.Empty);
        return json;
    }
}

internal sealed partial class AdaptiveSettingsTextInputValue : IAdaptiveInputValue
{
    private readonly TextBox? _textBox;
    private readonly TextBlock? _errorTextBlock;

    public AdaptiveSettingsTextInputValue(AdaptiveSettingsTextInputElement element, SettingsCard card, TextBox? textBox, TextBlock? errorTextBlock)
    {
        InputElement = element;
        Card = card;
        _textBox = textBox;
        _errorTextBlock = errorTextBlock;

        if (_textBox != null)
        {
            _textBox.TextChanged += (_, _) => Validate();
        }
    }

    public SettingsCard Card { get; }

    public bool Validate()
    {
        var inputElement = InputElement as AdaptiveSettingsTextInputElement;
        var isValid = !(inputElement?.IsRequired == true
            && string.IsNullOrWhiteSpace(CurrentValue));

        if (_errorTextBlock != null)
        {
            _errorTextBlock.Visibility = isValid
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        return isValid;
    }

    public void SetFocus() => _textBox?.Focus(FocusState.Programmatic);

    public UIElement? ErrorMessage { get; set; }

    public IAdaptiveInputElement InputElement { get; set; }

    public string CurrentValue
    {
        get
        {
            return _textBox?.Text ?? string.Empty;
        }
    }
}

internal sealed partial class AdaptiveSettingsTextInputElementRenderer : IAdaptiveElementRenderer
{
    public UIElement Render(IAdaptiveCardElement element, AdaptiveRenderContext context, AdaptiveRenderArgs renderArgs)
    {
        var item = element as AdaptiveSettingsTextInputElement;

        var textBox = new TextBox
        {
            Text = item?.Value ?? string.Empty,
            PlaceholderText = item?.Placeholder ?? string.Empty,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
        };

        object header = item?.Header ?? string.Empty;
        if (item?.IsRequired == true)
        {
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            headerPanel.Children.Add(new TextBlock { Text = item.Header ?? string.Empty });
            headerPanel.Children.Add(new TextBlock
            {
                Text = " *",
                Foreground = Application.Current.Resources["SystemFillColorCriticalBrush"] as Brush,
            });
            header = headerPanel;
        }

        var settingsCard = new SettingsCard
        {
            Header = header,
            Description = item?.Description ?? string.Empty,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = textBox,
        };

        TextBlock? errorTextBlock = null;
        if (item?.IsRequired == true)
        {
            var errorText = string.IsNullOrEmpty(item.ErrorMessage)
                ? "This field is required."
                : item.ErrorMessage;
            errorTextBlock = new TextBlock
            {
                Text = errorText,
                Foreground = Application.Current.Resources["SystemFillColorCriticalBrush"] as Brush,
                FontSize = 12,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap,
            };
        }

        // BEAR LOADING:
        // Wrap everything in a border to prevent, otherwise Adaptive Cards moves the content of the panel
        // around and we'll get a rendering exception that container already has a parent.
        var containerWrapper = new Border();
        var container = new StackPanel { Orientation = Orientation.Vertical };
        container.Children.Add(settingsCard);
        if (errorTextBlock != null)
        {
            container.Children.Add(errorTextBlock);
        }

        if (item != null)
        {
            context.AddInputValue(
                new AdaptiveSettingsTextInputValue(item, settingsCard, textBox, errorTextBlock),
                renderArgs);
        }

        containerWrapper.Child = container;

        return containerWrapper;
    }
}
