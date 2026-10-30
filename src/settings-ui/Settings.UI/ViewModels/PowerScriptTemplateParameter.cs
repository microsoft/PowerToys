// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public sealed class PowerScriptTemplateParameter : Observable
{
    private string _name = string.Empty;
    private int _typeIndex = -1;
    private bool _hasDefault;

    public PowerScriptTemplateParameter(int id)
    {
        AutomationId = "PowerScriptsTemplateParameter" + id.ToString(CultureInfo.InvariantCulture);
    }

    public string AutomationId { get; }

    public string ControlId(string suffix) => AutomationId + suffix;

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public int TypeIndex
    {
        get => _typeIndex;
        set
        {
            if (Set(ref _typeIndex, value))
            {
                HasDefault = false;
                OnPropertyChanged(nameof(IsString));
                OnPropertyChanged(nameof(IsInteger));
                OnPropertyChanged(nameof(IsBoolean));
                OnPropertyChanged(nameof(IsFile));
                OnPropertyChanged(nameof(ShowTextDefault));
                OnPropertyChanged(nameof(ShowBooleanDefault));
            }
        }
    }

    public bool IsString => TypeIndex == 0;

    public bool IsInteger => TypeIndex == 1;

    public bool IsBoolean => TypeIndex == 2;

    public bool IsFile => TypeIndex == 3;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool Required { get; set; }

    public bool HasDefault
    {
        get => _hasDefault;
        set
        {
            if (Set(ref _hasDefault, value))
            {
                OnPropertyChanged(nameof(ShowTextDefault));
                OnPropertyChanged(nameof(ShowBooleanDefault));
            }
        }
    }

    public bool ShowTextDefault => HasDefault && !IsBoolean;

    public bool ShowBooleanDefault => HasDefault && IsBoolean;

    public string DefaultText { get; set; } = string.Empty;

    public bool DefaultBoolean { get; set; }

    public string EnumText { get; set; } = string.Empty;

    public string MinimumText { get; set; } = string.Empty;

    public string MaximumText { get; set; } = string.Empty;

    public string FileExtensions { get; set; } = string.Empty;

    public JsonObject ToTemplateParameter()
    {
        var type = TypeIndex switch
        {
            0 or 3 => "string",
            1 => "integer",
            2 => "boolean",
            _ => throw new InvalidOperationException("Select a parameter type."),
        };
        var schema = new JsonObject { ["type"] = type };
        if (!string.IsNullOrWhiteSpace(Title))
        {
            schema["title"] = Title;
        }

        if (!string.IsNullOrWhiteSpace(Description))
        {
            schema["description"] = Description;
        }

        if (HasDefault)
        {
            schema["default"] = IsBoolean ? JsonValue.Create(DefaultBoolean) :
                IsInteger ? JsonValue.Create(ParseInteger(DefaultText)) : JsonValue.Create(DefaultText);
        }

        if (IsString && !string.IsNullOrWhiteSpace(EnumText))
        {
            var choices = EnumText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);
            schema["enum"] = new JsonArray(choices.Select(value => (JsonNode)JsonValue.Create(value)).ToArray());
        }

        if (IsInteger)
        {
            if (!string.IsNullOrWhiteSpace(MinimumText))
            {
                schema["minimum"] = ParseInteger(MinimumText);
            }

            if (!string.IsNullOrWhiteSpace(MaximumText))
            {
                schema["maximum"] = ParseInteger(MaximumText);
            }
        }

        if (IsFile)
        {
            schema["format"] = "file-path";
        }

        var definition = new JsonObject { ["name"] = Name, ["schema"] = schema, ["required"] = Required };
        if (IsFile)
        {
            definition["fileExtensions"] = FileExtensions;
        }

        return definition;
    }

    private int ParseInteger(string text)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new ArgumentException(string.Format(
                CultureInfo.CurrentCulture,
                ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_TemplateParameterInvalidInteger"),
                Name));
        }

        return value;
    }
}
