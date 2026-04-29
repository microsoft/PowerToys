// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using PowerToys.MacroCommon.Models;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public sealed class MacroStepViewModel : Observable
{
    private StepType _type;
    private string? _key;
    private string? _text;
    private int? _ms;
    private int? _count;

    public StepType Type
    {
        get => _type;
        set
        {
            if (Set(ref _type, value))
            {
                OnPropertyChanged(nameof(TypeLabel));
                OnPropertyChanged(nameof(IsPressKey));
                OnPropertyChanged(nameof(IsTypeText));
                OnPropertyChanged(nameof(IsWait));
                OnPropertyChanged(nameof(IsRepeat));
            }
        }
    }

    public string? Key
    {
        get => _key;
        set => Set(ref _key, value);
    }

    public string? Text
    {
        get => _text;
        set => Set(ref _text, value);
    }

    public int? Ms
    {
        get => _ms;
        set => Set(ref _ms, value);
    }

    public int? Count
    {
        get => _count;
        set => Set(ref _count, value);
    }

    public ObservableCollection<MacroStepViewModel> SubSteps { get; } = [];

    public double MsDouble
    {
        get => _ms ?? 0;
        set
        {
            Ms = Math.Max(0, (int)value);
            OnPropertyChanged();
        }
    }

    public double CountDouble
    {
        get => Math.Max(1, _count ?? 1);
        set
        {
            Count = Math.Max(1, (int)value);
            OnPropertyChanged();
        }
    }

    public string TypeLabel => Type switch
    {
        StepType.PressKey => "Key",
        StepType.TypeText => "Text",
        StepType.Wait => "Wait (ms)",
        StepType.Repeat => "Repeat",
        _ => Type.ToString(),
    };

    public bool IsPressKey => Type == StepType.PressKey;

    public bool IsTypeText => Type == StepType.TypeText;

    public bool IsWait => Type == StepType.Wait;

    public bool IsRepeat => Type == StepType.Repeat;

    public static MacroStepViewModel FromModel(MacroStep step)
    {
        MacroStepViewModel vm = new()
        {
            Type = step.Type,
            Key = step.Key,
            Text = step.Text,
            Ms = step.Ms,
            Count = step.Count,
        };

        if (step.Steps != null)
        {
            foreach (MacroStep sub in step.Steps)
            {
                vm.SubSteps.Add(FromModel(sub));
            }
        }

        return vm;
    }

    public MacroStep ToModel() => new()
    {
        Type = Type,
        Key = Key,
        Text = Text,
        Ms = Ms,
        Count = Count,
        Steps = SubSteps.Count > 0 ? [.. SubSteps.Select(s => s.ToModel())] : null,
    };
}
