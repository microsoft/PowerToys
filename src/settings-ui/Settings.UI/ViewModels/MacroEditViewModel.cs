// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using PowerToys.MacroCommon.Models;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public sealed class MacroEditViewModel : Observable
{
    private string _name;
    private HotkeySettings _hotkey;
    private string? _appScope;

    public MacroEditViewModel()
        : this(new MacroDefinition())
    {
    }

    public MacroEditViewModel(MacroDefinition definition)
    {
        OriginalId = definition.Id;
        _name = definition.Name;
        _hotkey = MacroHotkeyConverter.ToHotkeySettings(definition.Hotkey);
        _appScope = definition.AppScope;
        Steps = new ObservableCollection<MacroStepViewModel>(
            definition.Steps.Select(MacroStepViewModel.FromModel));
    }

    public string OriginalId { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (Set(ref _name, value))
            {
                OnPropertyChanged(nameof(HasValidationError));
            }
        }
    }

    public HotkeySettings Hotkey
    {
        get => _hotkey;
        set => Set(ref _hotkey, value);
    }

    public string? AppScope
    {
        get => _appScope;
        set => Set(ref _appScope, value);
    }

    public ObservableCollection<MacroStepViewModel> Steps { get; }

    public bool HasValidationError => string.IsNullOrWhiteSpace(_name);

    public MacroDefinition ToDefinition() => new()
    {
        Id = OriginalId,
        Name = Name.Trim(),
        Hotkey = MacroHotkeyConverter.FromHotkeySettings(Hotkey),
        AppScope = string.IsNullOrWhiteSpace(AppScope) ? null : AppScope!.Trim(),
        Steps = [.. Steps.Select(s => s.ToModel())],
    };

    public void AddStep(StepType type)
    {
        Steps.Add(new MacroStepViewModel { Type = type });
    }

    public void DeleteStep(MacroStepViewModel step)
    {
        Steps.Remove(step);
    }

    public void AddSubStep(MacroStepViewModel parent, StepType type)
    {
        parent.SubSteps.Add(new MacroStepViewModel { Type = type });
    }

    public void DeleteSubStep(MacroStepViewModel parent, MacroStepViewModel child)
    {
        parent.SubSteps.Remove(child);
    }
}
