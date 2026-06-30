// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

using KeyboardManagerEditorUI.Helpers;
using KeyboardManagerEditorUI.Templates;

namespace KeyboardManagerEditorUI.ViewModels
{
    public sealed class TemplateParameterViewModel : INotifyPropertyChanged
    {
        private string _value = string.Empty;
        private TemplateChoiceViewModel? _selectedChoice;

        public TemplateParameterViewModel(TemplateParameter definition)
        {
            ArgumentNullException.ThrowIfNull(definition);

            Name = definition.Name;
            Label = ResourceHelper.GetString(definition.LabelResourceKey);
            Type = definition.Type;
            Required = definition.Required;

            if (definition.Choices is not null)
            {
                Choices = definition.Choices
                    .Select(c => new TemplateChoiceViewModel(c.Value, ResourceHelper.GetString(c.DisplayResourceKey)))
                    .ToList();
            }
        }

        public string Name { get; }

        public string Label { get; }

        public string Type { get; }

        public bool Required { get; }

        public IReadOnlyList<TemplateChoiceViewModel>? Choices { get; }

        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value ?? string.Empty;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsValid));
                }
            }
        }

        public TemplateChoiceViewModel? SelectedChoice
        {
            get => _selectedChoice;
            set
            {
                if (!ReferenceEquals(_selectedChoice, value))
                {
                    _selectedChoice = value;
                    Value = value?.Value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsValid => !Required || !string.IsNullOrEmpty(Value);

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
