// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;
using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.AdaptiveCards;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Data.Json;
using Windows.System;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

#pragma warning disable SA1402 // File may only contain a single type

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards;

internal sealed partial class AdaptiveKeyValueListInputElement : AdaptiveListInputElement, ICustomAdaptiveCardElement
{
    public static string CustomInputType => "Input.CommandPalette.KeyValueList";

    public override string ElementTypeString => CustomInputType;

    public string? KeyPlaceholder { get; set; }

    public string? ValuePlaceholder { get; set; }

    public string? MissingKeyErrorMessage { get; set; }

    public string? KeyValidationPattern { get; set; }

    public string? KeyValidationErrorMessage { get; set; }

    public string? ValueValidationPattern { get; set; }

    public string? ValueValidationErrorMessage { get; set; }

    public bool PreventDuplicateKeys { get; set; }

    public string? DuplicateKeyErrorMessage { get; set; }

    public override JsonObject ToJson()
    {
        var json = base.ToJson();
        json.Remove("placeholder");
        json.Remove("itemValidationPattern");
        json.Remove("itemValidationErrorMessage");
        json.Remove("preventDuplicates");
        json.Remove("duplicateItemErrorMessage");
        AdaptiveCustomElementJson.SetString(json, "keyPlaceholder", KeyPlaceholder);
        AdaptiveCustomElementJson.SetString(json, "valuePlaceholder", ValuePlaceholder);
        AdaptiveCustomElementJson.SetString(json, "missingKeyErrorMessage", MissingKeyErrorMessage);
        AdaptiveCustomElementJson.SetString(json, "keyValidationPattern", KeyValidationPattern);
        AdaptiveCustomElementJson.SetString(json, "keyValidationErrorMessage", KeyValidationErrorMessage);
        AdaptiveCustomElementJson.SetString(json, "valueValidationPattern", ValueValidationPattern);
        AdaptiveCustomElementJson.SetString(json, "valueValidationErrorMessage", ValueValidationErrorMessage);
        AdaptiveCustomElementJson.SetBoolean(json, "preventDuplicateKeys", PreventDuplicateKeys);
        AdaptiveCustomElementJson.SetString(json, "duplicateKeyErrorMessage", DuplicateKeyErrorMessage);
        return json;
    }
}

internal sealed partial class AdaptiveKeyValueListInputElementParser : IAdaptiveElementParser
{
    public IAdaptiveCardElement FromJson(
        JsonObject inputJson,
        AdaptiveElementParserRegistration elementParsers,
        AdaptiveActionParserRegistration actionParsers,
        IList<AdaptiveWarning> warnings)
    {
        var element = AdaptiveListInputElementParser.Parse<AdaptiveKeyValueListInputElement>(
            inputJson,
            elementParsers,
            actionParsers,
            warnings);
        element.KeyPlaceholder = inputJson.GetNamedString("keyPlaceholder", string.Empty);
        element.ValuePlaceholder = inputJson.GetNamedString("valuePlaceholder", string.Empty);
        element.MissingKeyErrorMessage = inputJson.GetNamedString("missingKeyErrorMessage", string.Empty);
        element.KeyValidationPattern = AdaptiveInputValidation.ParsePattern(
            inputJson,
            "keyValidationPattern",
            AdaptiveKeyValueListInputElement.CustomInputType,
            warnings);
        element.KeyValidationErrorMessage = inputJson.GetNamedString("keyValidationErrorMessage", string.Empty);
        element.ValueValidationPattern = AdaptiveInputValidation.ParsePattern(
            inputJson,
            "valueValidationPattern",
            AdaptiveKeyValueListInputElement.CustomInputType,
            warnings);
        element.ValueValidationErrorMessage = inputJson.GetNamedString("valueValidationErrorMessage", string.Empty);
        element.PreventDuplicateKeys = inputJson.GetNamedBoolean("preventDuplicateKeys", false);
        element.DuplicateKeyErrorMessage = inputJson.GetNamedString("duplicateKeyErrorMessage", string.Empty);
        return element;
    }
}

internal sealed partial class AdaptiveKeyValueListInputElementRenderer : IAdaptiveElementRenderer
{
    public UIElement Render(IAdaptiveCardElement element, AdaptiveRenderContext context, AdaptiveRenderArgs renderArgs)
    {
        var input = (AdaptiveKeyValueListInputElement)element;
        var control = new AdaptiveKeyValueListInputControl(input);
        context.AddInputValue(new AdaptiveCustomInputValue(input, control), renderArgs);
        return control;
    }
}

internal sealed partial class AdaptiveKeyValueListInputControl : AdaptiveListInputControlBase
{
    private readonly AdaptiveKeyValueListInputElement _element;
    private readonly List<AdaptiveKeyValuePairValue> _items;
    private readonly string? _unreadableValue;
    private readonly Regex? _keyValidationRegex;
    private readonly Regex? _valueValidationRegex;

    private readonly TextBox _keyTextBox;
    private readonly TextBox _valueTextBox;
    private readonly Button _addButton;

    private bool _wasEdited;

    public AdaptiveKeyValueListInputControl(AdaptiveKeyValueListInputElement element)
        : base(element)
    {
        _element = element;
        if (AdaptiveListValueCodec.TryParsePairs(element.Value, out var parsedPairs))
        {
            _items = parsedPairs;
        }
        else
        {
            // Keep the value we could not read so that saving the form does not discard it.
            _items = [];
            _unreadableValue = element.Value;
            Logger.LogWarning($"Could not read the value of {element.ElementTypeString} '{element.Id}'.");
        }

        _keyValidationRegex = AdaptiveInputValidation.CreateRegex(element.KeyValidationPattern);
        _valueValidationRegex = AdaptiveInputValidation.CreateRegex(element.ValueValidationPattern);

        var addControls = new Grid { ColumnSpacing = 8 };
        addControls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        addControls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        addControls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _keyTextBox = new TextBox
        {
            Header = RS_.GetString("AdaptiveKeyValueListInput_Key"),
            PlaceholderText = string.IsNullOrEmpty(element.KeyPlaceholder)
                ? RS_.GetString("AdaptiveKeyValueListInput_KeyPlaceholder")
                : element.KeyPlaceholder,
        };
        _keyTextBox.KeyDown += AddTextBox_KeyDown;

        _valueTextBox = new TextBox
        {
            Header = RS_.GetString("AdaptiveKeyValueListInput_Value"),
            PlaceholderText = string.IsNullOrEmpty(element.ValuePlaceholder)
                ? RS_.GetString("AdaptiveKeyValueListInput_ValuePlaceholder")
                : element.ValuePlaceholder,
        };
        _valueTextBox.KeyDown += AddTextBox_KeyDown;
        Grid.SetColumn(_valueTextBox, 1);

        _addButton = CreateTextButton(RS_.GetString("AdaptiveListInput_Add"), AddGlyph);
        _addButton.IsEnabled = false;
        _addButton.VerticalAlignment = VerticalAlignment.Bottom;
        _addButton.Click += (_, _) => AddItem();
        Grid.SetColumn(_addButton, 2);

        _keyTextBox.TextChanged += (_, _) =>
            _addButton.IsEnabled = !string.IsNullOrWhiteSpace(_keyTextBox.Text);

        addControls.Children.Add(_keyTextBox);
        addControls.Children.Add(_valueTextBox);
        addControls.Children.Add(_addButton);
        RootPanel.Children.Add(addControls);
        CompleteLayout();
        RefreshItems();
    }

    public override string CurrentValue =>
        _unreadableValue is not null && !_wasEdited
            ? _unreadableValue
            : AdaptiveListValueCodec.ToPairsValue(_items);

    public override void FocusInput() => _keyTextBox.Focus(FocusState.Programmatic);

    private void AddTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            AddItem();
        }
    }

    private void AddItem()
    {
        var key = _keyTextBox.Text;
        var value = _valueTextBox.Text;

        if (string.IsNullOrWhiteSpace(key))
        {
            ShowValidationError(GetMissingKeyErrorMessage());
            _keyTextBox.Focus(FocusState.Programmatic);
            return;
        }

        if (!AdaptiveInputValidation.IsMatch(_keyValidationRegex, key))
        {
            ShowValidationError(GetKeyValidationErrorMessage());
            _keyTextBox.Focus(FocusState.Programmatic);
            return;
        }

        if (!AdaptiveInputValidation.IsMatch(_valueValidationRegex, value))
        {
            ShowValidationError(GetValueValidationErrorMessage());
            _valueTextBox.Focus(FocusState.Programmatic);
            return;
        }

        if (_element.PreventDuplicateKeys &&
            _items.Any(item => string.Equals(item.Key, key, StringComparison.Ordinal)))
        {
            ShowValidationError(GetDuplicateKeyErrorMessage());
            _keyTextBox.Focus(FocusState.Programmatic);
            return;
        }

        _items.Add(new AdaptiveKeyValuePairValue(key, value));
        _wasEdited = true;
        _keyTextBox.Text = string.Empty;
        _valueTextBox.Text = string.Empty;
        RefreshItems();
        UpdateValidationIfRequested();
        _keyTextBox.Focus(FocusState.Programmatic);
    }

    private void RefreshItems()
    {
        RefreshListItems(_items, CreateItemRow);
    }

    private UIElement CreateItemRow(AdaptiveKeyValuePairValue item)
    {
        var row = new Grid
        {
            ColumnSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = ListItemMinHeight,
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var keyText = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(item.Key)
                ? RS_.GetString("AdaptiveKeyValueListInput_MissingKeyDisplay")
                : item.Key,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (string.IsNullOrWhiteSpace(item.Key))
        {
            ApplyCriticalForeground(keyText);
        }

        ToolTipService.SetToolTip(keyText, item.Key);
        row.Children.Add(keyText);

        var separator = new TextBlock
        {
            Text = "=",
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetAccessibilityView(separator, AccessibilityView.Raw);
        Grid.SetColumn(separator, 1);
        row.Children.Add(separator);

        var valueText = new TextBlock
        {
            Text = item.Value,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(valueText, item.Value);
        Grid.SetColumn(valueText, 2);
        row.Children.Add(valueText);

        var removeLabel = GetRemoveItemLabel($"{item.Key} = {item.Value}");
        var removeButton = new Button
        {
            Style = (Style)Application.Current.Resources["SubtleButtonStyle"],
            Content = new FontIcon { Glyph = DeleteGlyph, FontSize = 14 },
            MinHeight = 30,
            MinWidth = 30,
            Padding = new Thickness(6),
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(removeButton, removeLabel);
        ToolTipService.SetToolTip(removeButton, removeLabel);
        removeButton.Click += (_, _) =>
        {
            _items.Remove(item);
            _wasEdited = true;
            RefreshItems();
            UpdateValidationIfRequested();
        };
        Grid.SetColumn(removeButton, 3);
        row.Children.Add(removeButton);

        return row;
    }

    protected override bool UpdateValidation()
    {
        if (_element.IsRequired && _items.Count == 0)
        {
            ShowValidationError(string.IsNullOrEmpty(_element.ErrorMessage)
                ? RS_.GetString("AdaptiveListInput_RequiredError")
                : _element.ErrorMessage);
            return false;
        }

        if (_items.Any(static item => string.IsNullOrWhiteSpace(item.Key)))
        {
            ShowValidationError(GetMissingKeyErrorMessage());
            return false;
        }

        if (_items.Any(item => !AdaptiveInputValidation.IsMatch(_keyValidationRegex, item.Key)))
        {
            ShowValidationError(GetKeyValidationErrorMessage());
            return false;
        }

        if (_items.Any(item => !AdaptiveInputValidation.IsMatch(_valueValidationRegex, item.Value)))
        {
            ShowValidationError(GetValueValidationErrorMessage());
            return false;
        }

        if (_element.PreventDuplicateKeys && HasDuplicateKeys())
        {
            ShowValidationError(GetDuplicateKeyErrorMessage());
            return false;
        }

        ValidationError.Visibility = Visibility.Collapsed;
        return true;
    }

    private string GetMissingKeyErrorMessage() =>
        string.IsNullOrEmpty(_element.MissingKeyErrorMessage)
            ? RS_.GetString("AdaptiveKeyValueListInput_MissingKeyError")
            : _element.MissingKeyErrorMessage;

    private string GetKeyValidationErrorMessage() =>
        string.IsNullOrEmpty(_element.KeyValidationErrorMessage)
            ? RS_.GetString("AdaptiveKeyValueListInput_KeyValidationError")
            : _element.KeyValidationErrorMessage;

    private string GetValueValidationErrorMessage() =>
        string.IsNullOrEmpty(_element.ValueValidationErrorMessage)
            ? RS_.GetString("AdaptiveKeyValueListInput_ValueValidationError")
            : _element.ValueValidationErrorMessage;

    private string GetDuplicateKeyErrorMessage() =>
        string.IsNullOrEmpty(_element.DuplicateKeyErrorMessage)
            ? RS_.GetString("AdaptiveKeyValueListInput_DuplicateKeyError")
            : _element.DuplicateKeyErrorMessage;

    private bool HasDuplicateKeys()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return _items.Any(item => !seen.Add(item.Key));
    }
}

#pragma warning restore SA1402 // File may only contain a single type
