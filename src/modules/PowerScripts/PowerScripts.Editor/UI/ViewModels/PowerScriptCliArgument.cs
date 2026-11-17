// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace PowerScripts.UI.ViewModels;

/// <summary>Chooses where an editable module-call argument obtains its value.</summary>
public sealed class PowerScriptCliArgument : Observable
{
    private int _sourceIndex;
    private bool _canUseInput = true;

    public PowerScriptCliArgument(PowerScriptOperationTemplateParameter definition, bool useInput)
    {
        Value = new PowerScriptOperationParameter(definition);
        Value.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(Value.TextValue))
            {
                OnPropertyChanged(nameof(SelectedChoice));
            }
        };
        _sourceIndex = useInput ? 1 : 0;
    }

    public PowerScriptOperationParameter Value { get; }

    public string Name => Value.Name;

    public string InputType => Value.IsFile ? "files" : "text";

    public bool IsFixed => SourceIndex == 0;

    public string SelectedChoice
    {
        get => Value.IsChoice ? Value.TextValue : null;
        set
        {
            if (Value.IsChoice && value is not null)
            {
                Value.TextValue = value;
            }
        }
    }

    public int SourceIndex
    {
        get => _sourceIndex;
        set
        {
            if (Set(ref _sourceIndex, value))
            {
                OnPropertyChanged(nameof(IsFixed));
                OnPropertyChanged(nameof(SourceHelp));
            }
        }
    }

    public bool CanUseInput
    {
        get => _canUseInput;
        set => Set(ref _canUseInput, value);
    }

    public string SourceHelp => PowerScriptsViewModel.GetResourceString(SourceIndex switch
    {
        0 => "PowerScripts_CliArgumentFixedHelp",
        1 => Value.IsFile ? "PowerScripts_CliArgumentFilesHelp" : "PowerScripts_CliArgumentTextHelp",
        _ => "PowerScripts_CliArgumentParameterHelp",
    });

    public string ControlId(string suffix) => "PowerScriptsCliArgument_" + Name + "_" + suffix;

    public JsonObject ToBinding()
    {
        var source = SourceIndex switch
        {
            0 => "fixed",
            1 => "input",
            2 => "parameter",
            _ => throw new ArgumentException("Choose a value source for each module argument."),
        };
        var result = new JsonObject { ["source"] = source };
        if (IsFixed)
        {
            result["value"] = Value.ToBinding()?["constant"]?.DeepClone()
                ?? throw new ArgumentException("Provide a fixed value for " + Value.Label + ".");
        }

        return result;
    }
}
