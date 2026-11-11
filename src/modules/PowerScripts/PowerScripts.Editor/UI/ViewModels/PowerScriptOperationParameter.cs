// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace PowerScripts.UI.ViewModels;

/// <summary>A schema-backed fixed value used by both creation and test runs.</summary>
public sealed class PowerScriptOperationParameter : Observable
{
    private readonly JsonObject _schema;
    private bool _isValueProvided;
    private string _textValue;
    private bool _booleanValue;

    public PowerScriptOperationParameter(PowerScriptOperationTemplateParameter definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name);
        _schema = (JsonObject)(definition.Schema?.DeepClone()
            ?? throw new ArgumentException("Operation parameter schema is missing.", nameof(definition)));
        Name = definition.Name;
        Required = definition.Required;
        FileExtensions = definition.FileExtensions;
        var title = _schema["title"]?.GetValue<string>();
        Label = string.IsNullOrWhiteSpace(title) ? Name : title;
        Description = _schema["description"]?.GetValue<string>() ?? string.Empty;
        var type = _schema["type"]?.GetValue<string>();
        IsBoolean = type == "boolean";
        IsInteger = type == "integer";
        IsFile = type == "string" && _schema["format"]?.GetValue<string>() == "file-path";
        if (type is not ("string" or "integer" or "boolean"))
        {
            throw new ArgumentException("Unsupported operation parameter type.", nameof(definition));
        }

        Choices = (_schema["enum"] as JsonArray)?.Select(value => value.GetValue<string>()).ToArray()
            ?? Array.Empty<string>();
        if (Choices.Count > 0 && type != "string")
        {
            throw new ArgumentException("Operation choices must contain string values.", nameof(definition));
        }

        BooleanChoices = Array.Empty<string>();
        if (IsBoolean && _schema["x-ui"] is JsonObject ui && UiString(ui, "control") == "choice")
        {
            var trueLabel = UiString(ui, "trueLabel");
            var falseLabel = UiString(ui, "falseLabel");
            if (string.IsNullOrWhiteSpace(trueLabel) || string.IsNullOrWhiteSpace(falseLabel) ||
                string.Equals(trueLabel, falseLabel, StringComparison.Ordinal))
            {
                throw new ArgumentException("Boolean choices require two distinct, non-empty labels.", nameof(definition));
            }

            BooleanChoices = new[] { trueLabel, falseLabel };
        }

        HasDefault = _schema["default"] is not null;
        _textValue = HasDefault && !IsBoolean ? DefaultText() : string.Empty;
        _booleanValue = HasDefault && IsBoolean && _schema["default"].GetValue<bool>();
        _isValueProvided = Required || HasDefault;
    }

    public string Name { get; }

    public string Label { get; }

    public string DisplayLabel => Required
        ? Format("PowerScripts_RunRequiredLabel", Label)
        : Label;

    public string Description { get; }

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public bool Required { get; }

    public bool HasDefault { get; }

    public bool CanOmit => !Required && !HasDefault;

    public bool IsBoolean { get; }

    public bool IsBooleanChoice => IsBoolean && BooleanChoices.Count > 0;

    public bool IsBooleanToggle => IsBoolean && !IsBooleanChoice;

    public bool IsInteger { get; }

    public bool IsFile { get; }

    public bool IsChoice => Choices.Count > 0;

    public bool IsText => !IsBoolean && !IsInteger && !IsChoice && !IsFile;

    public IReadOnlyList<string> Choices { get; }

    public IReadOnlyList<string> BooleanChoices { get; }

    public string FileExtensions { get; }

    public string OptionalValueLabel => Format("PowerScripts_OperationsProvideValue", Label);

    public string HelpLabel => Format("PowerScripts_OperationsAbout", Label);

    public string BrowseLabel => Format("PowerScripts_OperationsBrowseFor", Label);

    public string ValuePlaceholder => PowerScriptsViewModel.GetResourceString("PowerScripts_OperationsFixedValue");

    public bool IsValueProvided
    {
        get => _isValueProvided;
        set => Set(ref _isValueProvided, !CanOmit || value);
    }

    public string TextValue
    {
        get => _textValue;
        set => Set(ref _textValue, value ?? string.Empty);
    }

    public bool BooleanValue
    {
        get => _booleanValue;
        set
        {
            if (Set(ref _booleanValue, value))
            {
                OnPropertyChanged(nameof(SelectedBooleanChoice));
            }
        }
    }

    public string SelectedBooleanChoice
    {
        get => IsBooleanChoice ? BooleanChoices[BooleanValue ? 0 : 1] : null;
        set
        {
            if (!IsBooleanChoice || value is null)
            {
                return;
            }

            if (string.Equals(value, BooleanChoices[0], StringComparison.Ordinal))
            {
                BooleanValue = true;
            }
            else if (string.Equals(value, BooleanChoices[1], StringComparison.Ordinal))
            {
                BooleanValue = false;
            }
        }
    }

    public string ControlId(string suffix) => "PowerScriptsOperationParameter_" + Name + "_" + suffix;

    public bool TryValidateForCreate(out string error) => TryReadValue(out _, out error);

    public JsonObject ToBinding()
    {
        if (!TryReadValue(out var value, out var error))
        {
            throw new ArgumentException(error);
        }

        return IsValueProvided ? new JsonObject { ["constant"] = value } : null;
    }

    private static string UiString(JsonObject ui, string name) =>
        ui[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static string Format(string key, string label) => string.Format(
        CultureInfo.CurrentCulture,
        PowerScriptsViewModel.GetResourceString(key),
        label);

    private string DefaultText() => IsInteger
        ? _schema["default"].GetValue<int>().ToString(CultureInfo.InvariantCulture)
        : _schema["default"].GetValue<string>();

    private bool TryReadValue(out JsonNode value, out string error)
    {
        value = null;
        error = null;
        if (!IsValueProvided)
        {
            return true;
        }

        if (IsBoolean)
        {
            value = JsonValue.Create(BooleanValue);
            return true;
        }

        var text = TextValue;
        if (IsInteger)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            {
                error = Format("PowerScripts_TemplateParameterInvalidInteger", Label);
                return false;
            }

            if ((_schema["minimum"]?.GetValue<int>() is int minimum && integer < minimum) ||
                (_schema["maximum"]?.GetValue<int>() is int maximum && integer > maximum))
            {
                error = Format("PowerScripts_TemplateParameterOutsideRange", Label);
                return false;
            }

            value = JsonValue.Create(integer);
            return true;
        }

        if (Required && string.IsNullOrWhiteSpace(text))
        {
            error = Format("PowerScripts_OperationsRequiredValue", Label);
            return false;
        }

        if (IsChoice && !Choices.Contains(text, StringComparer.Ordinal))
        {
            error = Format("PowerScripts_TemplateParameterInvalidChoice", Label);
            return false;
        }

        if ((_schema["minLength"]?.GetValue<int>() is int minLength && text.Length < minLength) ||
            (_schema["maxLength"]?.GetValue<int>() is int maxLength && text.Length > maxLength))
        {
            error = Format("PowerScripts_TemplateParameterOutsideRange", Label);
            return false;
        }

        value = JsonValue.Create(text);
        return true;
    }
}
