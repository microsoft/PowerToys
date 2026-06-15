// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

using KeyboardManagerEditorUI.Helpers;
using KeyboardManagerEditorUI.Templates;

namespace KeyboardManagerEditorUI.ViewModels
{
    public sealed class CommandTemplatePickerViewModel : INotifyPropertyChanged
    {
        private CommandTemplate? _selectedTemplate;
        private string _selectionDescription = string.Empty;
        private string _resolvedCommandLine = string.Empty;

        public ObservableCollection<TemplateParameterViewModel> CurrentParameters { get; } = new();

        public CommandTemplate? SelectedTemplate
        {
            get => _selectedTemplate;
            private set
            {
                _selectedTemplate = value;
                OnPropertyChanged();
            }
        }

        public string SelectionDescription
        {
            get => _selectionDescription;
            private set
            {
                _selectionDescription = value;
                OnPropertyChanged();
            }
        }

        public string ResolvedCommandLine
        {
            get => _resolvedCommandLine;
            private set
            {
                _resolvedCommandLine = value;
                OnPropertyChanged();
            }
        }

        public bool IsAllValid => CurrentParameters.All(p => p.IsValid);

        public void SelectTemplate(string templateId)
        {
            var (module, template) = FindWithModule(templateId);
            ApplyTemplate(module, template, prefilledValues: null);
        }

        public void LoadExisting(string templateId, IReadOnlyDictionary<string, string>? values)
        {
            var (module, template) = FindWithModule(templateId);
            if (template is null)
            {
                throw new InvalidOperationException($"Template '{templateId}' not found in catalog.");
            }

            ApplyTemplate(module, template, values);
        }

        public void Clear()
        {
            SelectedTemplate = null;
            SelectionDescription = string.Empty;
            ResolvedCommandLine = string.Empty;

            DetachParameterListeners();
            CurrentParameters.Clear();
        }

        public Dictionary<string, string> CollectParameterValues()
        {
            return CurrentParameters.ToDictionary(p => p.Name, p => p.Value);
        }

        private (CommandTemplateModule? Module, CommandTemplate? Template) FindWithModule(string templateId)
        {
            foreach (var m in CommandTemplateCatalog.Instance.Data.Modules)
            {
                var t = m.Commands.FirstOrDefault(c => c.Id == templateId);
                if (t is not null)
                {
                    return (m, t);
                }
            }

            return (null, null);
        }

        private void ApplyTemplate(
            CommandTemplateModule? module,
            CommandTemplate? template,
            IReadOnlyDictionary<string, string>? prefilledValues)
        {
            DetachParameterListeners();
            CurrentParameters.Clear();

            SelectedTemplate = template;

            if (template is null || module is null)
            {
                SelectionDescription = string.Empty;
                ResolvedCommandLine = string.Empty;
                OnPropertyChanged(nameof(IsAllValid));
                return;
            }

            SelectionDescription =
                $"{ResourceHelper.GetString(module.DisplayResourceKey)} → {ResourceHelper.GetString(template.DisplayResourceKey)}";

            foreach (var p in template.Parameters)
            {
                var vm = new TemplateParameterViewModel(p);
                if (prefilledValues is not null && prefilledValues.TryGetValue(p.Name, out var v))
                {
                    if (vm.Choices is not null)
                    {
                        vm.SelectedChoice = vm.Choices.FirstOrDefault(c => c.Value == v);
                    }
                    else
                    {
                        vm.Value = v;
                    }
                }

                vm.PropertyChanged += Parameter_PropertyChanged;
                CurrentParameters.Add(vm);
            }

            RecomputePreview();

            // Notify so the host re-evaluates Save-button enablement on template selection — not only
            // on later parameter edits (the load/edit path does not raise SelectionChanged itself).
            OnPropertyChanged(nameof(IsAllValid));
        }

        private void DetachParameterListeners()
        {
            foreach (var p in CurrentParameters)
            {
                p.PropertyChanged -= Parameter_PropertyChanged;
            }
        }

        private void Parameter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TemplateParameterViewModel.Value))
            {
                RecomputePreview();
                OnPropertyChanged(nameof(IsAllValid));
            }
        }

        private void RecomputePreview()
        {
            if (_selectedTemplate is null)
            {
                ResolvedCommandLine = string.Empty;
                return;
            }

            var resolved = TemplateResolver.Resolve(_selectedTemplate, CollectParameterValues());
            ResolvedCommandLine = string.IsNullOrEmpty(resolved.Args)
                ? resolved.Executable
                : $"{resolved.Executable} {resolved.Args}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
