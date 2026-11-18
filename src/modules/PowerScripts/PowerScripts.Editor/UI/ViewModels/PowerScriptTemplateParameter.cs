// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using PowerScripts.UI.Helpers;

namespace PowerScripts.UI.ViewModels;

public sealed class PowerScriptTemplateParameter : Observable
{
    private const string Separator = " \u00B7 ";

    private string _name = string.Empty;
    private int _typeIndex = -1;
    private bool _hasDefault;
    private string _title = string.Empty;
    private bool _required;
    private string _defaultText = string.Empty;
    private bool _defaultBoolean;
    private string _enumText = string.Empty;
    private string _description = string.Empty;
    private string _minimumText = string.Empty;
    private string _maximumText = string.Empty;
    private string _fileExtensions = string.Empty;
    private string _error = string.Empty;

    public PowerScriptTemplateParameter(int id)
    {
        AutomationId = "PowerScriptsTemplateParameter" + id.ToString(CultureInfo.InvariantCulture);
    }

    public string AutomationId { get; }

    public string ControlId(string suffix) => AutomationId + suffix;

    public string Name
    {
        get => _name;
        set
        {
            if (Set(ref _name, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>Gets the name shown in the parameter's header.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? PowerScriptsViewModel.GetResourceString("PowerScripts_NewScript_ParameterUntitled") : Name.Trim();

    /// <summary>Gets a one-line summary of the parameter: its display name, type, whether it's required, and its default.</summary>
    public string Meta
    {
        get
        {
            if (TypeIndex < 0)
            {
                return TypeLabel;
            }

            var choices = IsString ? SplitChoices().Length : 0;
            var parts = new[]
            {
                Title.Trim(),
                choices > 0 ? Format("PowerScripts_NewScript_ParameterListOf", choices) : TypeLabel,
                PowerScriptsViewModel.GetResourceString(Required ? "PowerScripts_NewScript_ParameterRequired" : "PowerScripts_NewScript_ParameterOptional"),
                !HasDefault ? string.Empty
                    : IsBoolean ? PowerScriptsViewModel.GetResourceString(DefaultBoolean ? "PowerScripts_NewScript_ParameterDefaultOn" : "PowerScripts_NewScript_ParameterDefaultOff")
                    : string.IsNullOrWhiteSpace(DefaultText) ? string.Empty : Format("PowerScripts_NewScript_ParameterDefault", DefaultText.Trim()),
            };
            return string.Join(Separator, parts.Where(part => part.Length > 0));
        }
    }

    /// <summary>Gets the tile hue for the parameter's type.</summary>
    public string TypeKind => TypeIndex switch
    {
        0 => "blue",
        1 => "orange",
        2 => "green",
        3 => "purple",
        _ => "slate",
    };

    /// <summary>Gets the tile glyph for the parameter's type.</summary>
    public string TypeGlyph => TypeIndex switch
    {
        0 => "\uE8D2",
        1 => "\uE8EF",
        2 => "\uE73A",
        3 => "\uE8A5",
        _ => "\uE8FD",
    };

    /// <summary>Gets the type's name, or a prompt to pick one.</summary>
    public string TypeLabel => PowerScriptsViewModel.GetResourceString(TypeIndex switch
    {
        0 => "PowerScripts_NewScript_TypeText",
        1 => "PowerScripts_NewScript_TypeNumber",
        2 => "PowerScripts_NewScript_TypeBoolean",
        3 => "PowerScripts_NewScript_TypeFile",
        _ => "PowerScripts_NewScript_TypePick",
    });

    /// <summary>Gets a line explaining the selected type.</summary>
    public string TypeHelp => PowerScriptsViewModel.GetResourceString(TypeIndex switch
    {
        0 => "PowerScripts_NewScript_TypeTextHelp",
        1 => "PowerScripts_NewScript_TypeNumberHelp",
        2 => "PowerScripts_NewScript_TypeBooleanHelp",
        3 => "PowerScripts_NewScript_TypeFileHelp",
        _ => "PowerScripts_NewScript_TypePickHelp",
    });

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
                OnPropertyChanged(nameof(TypeKind));
                OnPropertyChanged(nameof(TypeGlyph));
                OnPropertyChanged(nameof(TypeLabel));
                OnPropertyChanged(nameof(TypeHelp));
                OnPropertyChanged(nameof(Meta));
            }
        }
    }

    public bool IsString => TypeIndex == 0;

    public bool IsInteger => TypeIndex == 1;

    public bool IsBoolean => TypeIndex == 2;

    public bool IsFile => TypeIndex == 3;

    public string Title
    {
        get => _title;
        set => SetAndRefreshMeta(ref _title, value ?? string.Empty);
    }

    public string Description
    {
        get => _description;
        set => Set(ref _description, value ?? string.Empty);
    }

    public bool Required
    {
        get => _required;
        set => SetAndRefreshMeta(ref _required, value);
    }

    public bool HasDefault
    {
        get => _hasDefault;
        set
        {
            if (Set(ref _hasDefault, value))
            {
                OnPropertyChanged(nameof(ShowTextDefault));
                OnPropertyChanged(nameof(ShowBooleanDefault));
                OnPropertyChanged(nameof(Meta));
            }
        }
    }

    public bool ShowTextDefault => HasDefault && !IsBoolean;

    public bool ShowBooleanDefault => HasDefault && IsBoolean;

    public string DefaultText
    {
        get => _defaultText;
        set => SetAndRefreshMeta(ref _defaultText, value ?? string.Empty);
    }

    public bool DefaultBoolean
    {
        get => _defaultBoolean;
        set => SetAndRefreshMeta(ref _defaultBoolean, value);
    }

    public string EnumText
    {
        get => _enumText;
        set => SetAndRefreshMeta(ref _enumText, value ?? string.Empty);
    }

    public string MinimumText
    {
        get => _minimumText;
        set => SetAndRefreshMeta(ref _minimumText, value ?? string.Empty);
    }

    public string MaximumText
    {
        get => _maximumText;
        set => SetAndRefreshMeta(ref _maximumText, value ?? string.Empty);
    }

    public string FileExtensions
    {
        get => _fileExtensions;
        set => Set(ref _fileExtensions, value ?? string.Empty);
    }

    /// <summary>Gets or sets the message explaining what's wrong with the parameter, or an empty string.</summary>
    public string Error
    {
        get => _error;
        set
        {
            if (Set(ref _error, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    /// <summary>Gets a value indicating whether the parameter has a problem to fix.</summary>
    public bool HasError => Error.Length > 0;

    /// <summary>Gets the choices entered for a text parameter, one per line.</summary>
    /// <returns>The trimmed, non-empty choices.</returns>
    public string[] GetChoices() => IsString ? SplitChoices() : Array.Empty<string>();

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
            var choices = SplitChoices();
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

    private static string Format(string resource, object value) =>
        string.Format(CultureInfo.CurrentCulture, PowerScriptsViewModel.GetResourceString(resource), value);

    private string[] SplitChoices() => EnumText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private void SetAndRefreshMeta<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (Set(ref field, value, propertyName))
        {
            OnPropertyChanged(nameof(Meta));
        }
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
