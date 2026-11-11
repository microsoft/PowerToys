// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using PowerScripts.Client;

namespace PowerScripts.UI.ViewModels;

/// <summary>One draft input. Omission is separate from false, zero and an empty string.</summary>
public sealed class PowerScriptRunParameter : Observable
{
    private bool _isProvided;
    private string _textValue;
    private bool _booleanValue;

    public PowerScriptRunParameter(PowerScriptParameter definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _isProvided = definition.IsRequired || definition.Default is not null;
        _textValue = definition.Default ?? string.Empty;
        _booleanValue = bool.TryParse(definition.Default, out var value) && value;
    }

    public PowerScriptParameter Definition { get; }

    public string Name => Definition.Name;

    public string Label => string.IsNullOrWhiteSpace(Definition.Label) ? Definition.Name : Definition.Label;

    public string DisplayLabel => Definition.IsRequired
        ? PowerScriptsViewModel.GetResourceString("PowerScripts_RunRequiredLabel").Replace("{0}", Label, StringComparison.Ordinal)
        : Label;

    public string Description => Definition.Description ?? string.Empty;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public IReadOnlyList<string> Options => Definition.Options;

    public bool CanOmit => !Definition.IsRequired || Definition.Default is not null;

    public bool IsBoolean => string.Equals(Definition.Type, "bool", StringComparison.OrdinalIgnoreCase);

    public bool IsChoice => string.Equals(Definition.Type, "choice", StringComparison.OrdinalIgnoreCase);

    public bool IsFile => string.Equals(Definition.Type, "file", StringComparison.OrdinalIgnoreCase);

    public bool IsText => !IsBoolean && !IsChoice && !IsFile;

    public bool IsProvided
    {
        get => _isProvided;
        set => Set(ref _isProvided, value);
    }

    public string TextValue
    {
        get => _textValue;
        set => Set(ref _textValue, value ?? string.Empty);
    }

    public bool BooleanValue
    {
        get => _booleanValue;
        set => Set(ref _booleanValue, value);
    }

    public string ProvidedValue => IsBoolean ? (BooleanValue ? "true" : "false") : TextValue;

    public string ControlId(string suffix) => "PowerScriptsRunParameter_" + Name + "_" + suffix;
}
