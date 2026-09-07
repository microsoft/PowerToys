// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.Data.Json;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

#pragma warning disable SA1402 // File may only contain a single type

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards;

internal enum AdaptiveFilePathSelectionMode
{
    File,
    Folder,
}

internal sealed partial class AdaptiveFilePathInputElement : IAdaptiveInputElement, IAdaptiveCardElement, ICustomAdaptiveCardElement
{
    public static string CustomInputType => "Input.CommandPalette.FilePath";

    public string? Header { get; set; }

    public string? Description { get; set; }

    public string? Value { get; set; }

    public string? Placeholder { get; set; }

    public string? ErrorMessage { get; set; }

    public string? ValidationPattern { get; set; }

    public string? ValidationErrorMessage { get; set; }

    public AdaptiveFilePathSelectionMode SelectionMode { get; set; }

    public List<string> FileTypeFilter { get; } = [];

    public bool IsRequired { get; set; }

    // Adaptive Cards renders this label outside custom inputs. The custom renderer uses Header instead.
    public string? Label { get; set; }

    public JsonObject ToJson()
    {
        var json = AdaptiveCustomElementJson.Create(this);
        AdaptiveCustomElementJson.SetString(json, "header", Header);
        AdaptiveCustomElementJson.SetString(json, "description", Description);
        AdaptiveCustomElementJson.SetString(json, "value", Value);
        AdaptiveCustomElementJson.SetString(json, "placeholder", Placeholder);
        AdaptiveCustomElementJson.SetBoolean(json, "isRequired", IsRequired);
        AdaptiveCustomElementJson.SetString(json, "errorMessage", ErrorMessage);
        AdaptiveCustomElementJson.SetString(
            json,
            "selectionMode",
            SelectionMode == AdaptiveFilePathSelectionMode.File ? "file" : "folder");
        AdaptiveCustomElementJson.SetStringArray(json, "fileTypeFilter", FileTypeFilter);
        AdaptiveCustomElementJson.SetString(json, "validationPattern", ValidationPattern);
        AdaptiveCustomElementJson.SetString(json, "validationErrorMessage", ValidationErrorMessage);
        return json;
    }

    public JsonObject? AdditionalProperties { get; set; }

    public ElementType ElementType { get; } = ElementType.Custom;

    public string ElementTypeString => CustomInputType;

    public IAdaptiveCardElement? FallbackContent { get; set; }

    public FallbackType FallbackType { get; set; }

    public HeightType Height { get; set; }

    public string? Id { get; set; }

    public bool IsVisible { get; set; } = true;

    public IList<AdaptiveRequirement> Requirements { get; } = [];

    public bool Separator { get; set; }

    public Spacing Spacing { get; set; }
}

internal sealed partial class AdaptiveFilePathInputElementParser : IAdaptiveElementParser
{
    public IAdaptiveCardElement FromJson(
        JsonObject inputJson,
        AdaptiveElementParserRegistration elementParsers,
        AdaptiveActionParserRegistration actionParsers,
        IList<AdaptiveWarning> warnings)
    {
        var selectionModeValue = inputJson.GetNamedString("selectionMode", "file");
        var selectionMode = AdaptiveFilePathSelectionMode.File;
        if (string.Equals(selectionModeValue, "folder", StringComparison.OrdinalIgnoreCase))
        {
            selectionMode = AdaptiveFilePathSelectionMode.Folder;
        }
        else if (!string.Equals(selectionModeValue, "file", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add(new AdaptiveWarning(
                WarningStatusCode.InvalidValue,
                $"{AdaptiveFilePathInputElement.CustomInputType}.selectionMode must be file or folder."));
        }

        var adaptiveLabel = inputJson.GetNamedString("label", string.Empty);
        var element = new AdaptiveFilePathInputElement
        {
            Label = string.Empty,
            Header = inputJson.GetNamedString("header", adaptiveLabel),
            Description = inputJson.GetNamedString("description", string.Empty),
            Value = inputJson.GetNamedString("value", string.Empty),
            Placeholder = inputJson.GetNamedString("placeholder", string.Empty),
            IsRequired = inputJson.GetNamedBoolean("isRequired", false),
            ErrorMessage = inputJson.GetNamedString("errorMessage", string.Empty),
            ValidationPattern = AdaptiveInputValidation.ParsePattern(
                inputJson,
                "validationPattern",
                AdaptiveFilePathInputElement.CustomInputType,
                warnings),
            ValidationErrorMessage = inputJson.GetNamedString("validationErrorMessage", string.Empty),
            SelectionMode = selectionMode,
        };

        AdaptiveCustomElementJson.ParseCommonProperties(
            element,
            inputJson,
            elementParsers,
            actionParsers,
            warnings);

        if (inputJson.TryGetValue("fileTypeFilter", out var filterValue) &&
            filterValue.ValueType == JsonValueType.Array)
        {
            foreach (var value in filterValue.GetArray())
            {
                if (value.ValueType == JsonValueType.String)
                {
                    element.FileTypeFilter.Add(value.GetString());
                }
            }
        }

        return element;
    }
}

internal sealed partial class AdaptiveFilePathInputElementRenderer : IAdaptiveElementRenderer
{
    public UIElement Render(IAdaptiveCardElement element, AdaptiveRenderContext context, AdaptiveRenderArgs renderArgs)
    {
        var input = (AdaptiveFilePathInputElement)element;
        var control = new AdaptiveFilePathInputControl(input);
        context.AddInputValue(new AdaptiveCustomInputValue(input, control), renderArgs);
        return control;
    }
}

internal sealed partial class AdaptiveFilePathInputControl : AdaptiveCustomInputControlBase
{
    private static readonly CompositeFormat _browseForFormat =
        CompositeFormat.Parse(RS_.GetString("AdaptiveFilePathInput_BrowseFor"));

    private readonly AdaptiveFilePathInputElement _element;
    private readonly Regex? _validationRegex;
    private readonly TextBox _pathTextBox;
    private readonly Button _browseButton;

    public AdaptiveFilePathInputControl(AdaptiveFilePathInputElement element)
        : base(element.Header, element.Description, element.IsRequired)
    {
        _element = element;
        _validationRegex = AdaptiveInputValidation.CreateRegex(element.ValidationPattern);

        var fieldName = string.IsNullOrEmpty(element.Header)
            ? RS_.GetString(element.SelectionMode == AdaptiveFilePathSelectionMode.File
                ? "AdaptiveFilePathInput_FilePath"
                : "AdaptiveFilePathInput_FolderPath")
            : element.Header;

        var inputRow = new Grid { ColumnSpacing = 8 };
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _pathTextBox = new TextBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PlaceholderText = string.IsNullOrEmpty(element.Placeholder)
                ? RS_.GetString(element.SelectionMode == AdaptiveFilePathSelectionMode.File
                    ? "AdaptiveFilePathInput_FilePlaceholder"
                    : "AdaptiveFilePathInput_FolderPlaceholder")
                : element.Placeholder,
            Text = element.Value ?? string.Empty,
        };
        AutomationProperties.SetName(_pathTextBox, fieldName);
        _pathTextBox.TextChanged += (_, _) => UpdateValidationIfRequested();
        inputRow.Children.Add(_pathTextBox);

        _browseButton = new Button
        {
            Content = RS_.GetString("AdaptiveFilePathInput_Browse"),
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        var browseLabel = string.Format(CultureInfo.CurrentCulture, _browseForFormat, fieldName);
        AutomationProperties.SetName(_browseButton, browseLabel);
        _browseButton.Click += BrowseButton_Click;
        Grid.SetColumn(_browseButton, 1);
        inputRow.Children.Add(_browseButton);
        RootPanel.Children.Add(inputRow);
        CompleteLayout();
    }

    public override string CurrentValue => _pathTextBox.Text;

    public override void FocusInput() => _pathTextBox.Focus(FocusState.Programmatic);

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = _element.SelectionMode == AdaptiveFilePathSelectionMode.File
                ? await AdaptiveFilePicker.PickFileAsync(
                    this,
                    _element.FileTypeFilter,
                    RS_.GetString("AdaptiveFilePathInput_SelectFile"))
                : await AdaptiveFilePicker.PickFolderAsync(
                    this,
                    RS_.GetString("AdaptiveFilePathInput_SelectFolder"));

            if (!string.IsNullOrEmpty(path))
            {
                _pathTextBox.Text = path;
                _pathTextBox.Focus(FocusState.Programmatic);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to pick a path for an adaptive-card input", ex);
        }
    }

    protected override bool UpdateValidation()
    {
        var value = _pathTextBox.Text;
        if (_element.IsRequired && string.IsNullOrWhiteSpace(value))
        {
            ShowValidationError(string.IsNullOrEmpty(_element.ErrorMessage)
                ? RS_.GetString("AdaptiveFilePathInput_RequiredError")
                : _element.ErrorMessage);
            return false;
        }

        if (!string.IsNullOrEmpty(value) && !AdaptiveInputValidation.IsMatch(_validationRegex, value))
        {
            ShowValidationError(string.IsNullOrEmpty(_element.ValidationErrorMessage)
                ? RS_.GetString("AdaptiveFilePathInput_ValidationError")
                : _element.ValidationErrorMessage);
            return false;
        }

        ValidationError.Visibility = Visibility.Collapsed;
        return true;
    }
}

#pragma warning restore SA1402 // File may only contain a single type
