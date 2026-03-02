// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Library;

public sealed class AdvancedPastePythonScriptAction : Observable, IAdvancedPasteAction, ICloneable
{
    private string _scriptPath = string.Empty;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private bool _isShown = true;
    private HotkeySettings _shortcut = new();

    [JsonPropertyName("scriptPath")]
    public string ScriptPath
    {
        get => _scriptPath;
        set => Set(ref _scriptPath, value ?? string.Empty);
    }

    [JsonPropertyName("name")]
    public string Name
    {
        get => _name;
        set => Set(ref _name, value ?? string.Empty);
    }

    [JsonPropertyName("description")]
    public string Description
    {
        get => _description;
        set => Set(ref _description, value ?? string.Empty);
    }

    [JsonPropertyName("isShown")]
    public bool IsShown
    {
        get => _isShown;
        set => Set(ref _isShown, value);
    }

    [JsonPropertyName("shortcut")]
    public HotkeySettings Shortcut
    {
        get => _shortcut;
        set
        {
            if (_shortcut != value)
            {
                _shortcut = value ?? new();
                OnPropertyChanged();
            }
        }
    }

    [JsonIgnore]
    public IEnumerable<IAdvancedPasteAction> SubActions => [];

    public object Clone()
    {
        return new AdvancedPastePythonScriptAction
        {
            ScriptPath = ScriptPath,
            Name = Name,
            Description = Description,
            IsShown = IsShown,
            Shortcut = Shortcut,
        };
    }
}
