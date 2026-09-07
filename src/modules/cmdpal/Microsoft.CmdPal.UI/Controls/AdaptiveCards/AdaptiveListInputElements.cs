// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using Microsoft.UI.Xaml;
using Windows.Data.Json;

#pragma warning disable SA1402 // File may only contain a single type

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards;

internal abstract partial class AdaptiveListInputElement : IAdaptiveInputElement, IAdaptiveCardElement
{
    public string? Header { get; set; }

    public string? Description { get; set; }

    public string? Value { get; set; }

    public string? Placeholder { get; set; }

    public string? ErrorMessage { get; set; }

    public string? ItemValidationPattern { get; set; }

    public string? ItemValidationErrorMessage { get; set; }

    public bool PreventDuplicates { get; set; }

    public string? DuplicateItemErrorMessage { get; set; }

    public bool IsRequired { get; set; }

    // Adaptive Cards renders this label outside custom inputs. The custom renderer uses Header instead.
    public string? Label { get; set; }

    public virtual JsonObject ToJson()
    {
        var json = AdaptiveCustomElementJson.Create(this);
        AdaptiveCustomElementJson.SetString(json, "header", Header);
        AdaptiveCustomElementJson.SetString(json, "description", Description);
        AdaptiveCustomElementJson.SetString(json, "value", Value);
        AdaptiveCustomElementJson.SetString(json, "placeholder", Placeholder);
        AdaptiveCustomElementJson.SetBoolean(json, "isRequired", IsRequired);
        AdaptiveCustomElementJson.SetString(json, "errorMessage", ErrorMessage);
        AdaptiveCustomElementJson.SetString(json, "itemValidationPattern", ItemValidationPattern);
        AdaptiveCustomElementJson.SetString(json, "itemValidationErrorMessage", ItemValidationErrorMessage);
        AdaptiveCustomElementJson.SetBoolean(json, "preventDuplicates", PreventDuplicates);
        AdaptiveCustomElementJson.SetString(json, "duplicateItemErrorMessage", DuplicateItemErrorMessage);
        return json;
    }

    public JsonObject? AdditionalProperties { get; set; }

    public ElementType ElementType { get; } = ElementType.Custom;

    public abstract string ElementTypeString { get; }

    public IAdaptiveCardElement? FallbackContent { get; set; }

    public FallbackType FallbackType { get; set; }

    public HeightType Height { get; set; }

    public string? Id { get; set; }

    public bool IsVisible { get; set; } = true;

    public IList<AdaptiveRequirement> Requirements { get; } = [];

    public bool Separator { get; set; }

    public Spacing Spacing { get; set; }
}

internal sealed partial class AdaptiveStringListInputElement : AdaptiveListInputElement, ICustomAdaptiveCardElement
{
    public static string CustomInputType => "Input.CommandPalette.StringList";

    public override string ElementTypeString => CustomInputType;
}

internal sealed partial class AdaptiveFilePathListInputElement : AdaptiveListInputElement, ICustomAdaptiveCardElement
{
    public static string CustomInputType => "Input.CommandPalette.FilePathList";

    public override string ElementTypeString => CustomInputType;

    public bool AllowFiles { get; set; } = true;

    public bool AllowFolders { get; set; } = true;

    public List<string> FileTypeFilter { get; } = [];

    public override JsonObject ToJson()
    {
        var json = base.ToJson();
        json.Remove("placeholder");
        AdaptiveCustomElementJson.SetBoolean(json, "allowFiles", AllowFiles);
        AdaptiveCustomElementJson.SetBoolean(json, "allowFolders", AllowFolders);
        AdaptiveCustomElementJson.SetStringArray(json, "fileTypeFilter", FileTypeFilter);
        return json;
    }
}

internal static class AdaptiveListInputElementParser
{
    public static T Parse<T>(
        JsonObject inputJson,
        AdaptiveElementParserRegistration elementParsers,
        AdaptiveActionParserRegistration actionParsers,
        IList<AdaptiveWarning> warnings)
        where T : AdaptiveListInputElement, new()
    {
        var adaptiveLabel = inputJson.GetNamedString("label", string.Empty);
        var element = new T
        {
            Label = string.Empty,
            Header = inputJson.GetNamedString("header", adaptiveLabel),
            Description = inputJson.GetNamedString("description", string.Empty),
            Value = inputJson.GetNamedString("value", string.Empty),
            Placeholder = inputJson.GetNamedString("placeholder", string.Empty),
            IsRequired = inputJson.GetNamedBoolean("isRequired", false),
            ErrorMessage = inputJson.GetNamedString("errorMessage", string.Empty),
            ItemValidationPattern = AdaptiveInputValidation.ParsePattern(
                inputJson,
                "itemValidationPattern",
                typeof(T).Name,
                warnings),
            ItemValidationErrorMessage = inputJson.GetNamedString("itemValidationErrorMessage", string.Empty),
            PreventDuplicates = inputJson.GetNamedBoolean("preventDuplicates", false),
            DuplicateItemErrorMessage = inputJson.GetNamedString("duplicateItemErrorMessage", string.Empty),
        };

        AdaptiveCustomElementJson.ParseCommonProperties(
            element,
            inputJson,
            elementParsers,
            actionParsers,
            warnings);
        return element;
    }
}

internal sealed partial class AdaptiveStringListInputElementParser : IAdaptiveElementParser
{
    public IAdaptiveCardElement FromJson(
        JsonObject inputJson,
        AdaptiveElementParserRegistration elementParsers,
        AdaptiveActionParserRegistration actionParsers,
        IList<AdaptiveWarning> warnings) =>
        AdaptiveListInputElementParser.Parse<AdaptiveStringListInputElement>(
            inputJson,
            elementParsers,
            actionParsers,
            warnings);
}

internal sealed partial class AdaptiveFilePathListInputElementParser : IAdaptiveElementParser
{
    public IAdaptiveCardElement FromJson(
        JsonObject inputJson,
        AdaptiveElementParserRegistration elementParsers,
        AdaptiveActionParserRegistration actionParsers,
        IList<AdaptiveWarning> warnings)
    {
        var element = AdaptiveListInputElementParser.Parse<AdaptiveFilePathListInputElement>(
            inputJson,
            elementParsers,
            actionParsers,
            warnings);
        element.AllowFiles = inputJson.GetNamedBoolean("allowFiles", true);
        element.AllowFolders = inputJson.GetNamedBoolean("allowFolders", true);

        if (!element.AllowFiles && !element.AllowFolders)
        {
            warnings.Add(new AdaptiveWarning(
                WarningStatusCode.InvalidValue,
                $"{AdaptiveFilePathListInputElement.CustomInputType} must allow files, folders, or both."));
            element.AllowFolders = true;
        }

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

internal sealed partial class AdaptiveStringListInputElementRenderer : IAdaptiveElementRenderer
{
    public UIElement Render(IAdaptiveCardElement element, AdaptiveRenderContext context, AdaptiveRenderArgs renderArgs)
    {
        var input = (AdaptiveStringListInputElement)element;
        var control = new AdaptiveListInputControl(input);
        context.AddInputValue(new AdaptiveCustomInputValue(input, control), renderArgs);
        return control;
    }
}

internal sealed partial class AdaptiveFilePathListInputElementRenderer : IAdaptiveElementRenderer
{
    public UIElement Render(IAdaptiveCardElement element, AdaptiveRenderContext context, AdaptiveRenderArgs renderArgs)
    {
        var input = (AdaptiveFilePathListInputElement)element;
        var control = new AdaptiveListInputControl(input);
        context.AddInputValue(new AdaptiveCustomInputValue(input, control), renderArgs);
        return control;
    }
}

#pragma warning restore SA1402 // File may only contain a single type
