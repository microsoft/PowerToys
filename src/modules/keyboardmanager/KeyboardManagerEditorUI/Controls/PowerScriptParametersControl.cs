// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using KeyboardManagerEditorUI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using PowerScripts.Client;

namespace KeyboardManagerEditorUI.Controls
{
    /// <summary>Collects standard script parameters; no CLI-specific settings are stored here.</summary>
    public sealed partial class PowerScriptParametersControl : UserControl
    {
        private readonly StackPanel _panel = new() { Spacing = 12 };
        private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);
        private PowerScripts.Client.PowerScriptInfo? _script;
        private int _generation;
        private bool _updating;

        public PowerScriptParametersControl()
        {
            Content = _panel;
            Unloaded += (_, _) => _generation++;
        }

        public event EventHandler? ValuesChanged;

        public IReadOnlyDictionary<string, string?> GetValues() => new Dictionary<string, string?>(_values, StringComparer.Ordinal);

        public void SetScript(PowerScripts.Client.PowerScriptInfo? script, IReadOnlyDictionary<string, string?>? values = null)
        {
            _generation++;
            _updating = true;
            _script = script;
            _values.Clear();
            if (values is not null)
            {
                foreach (var pair in values)
                {
                    _values[pair.Key] = pair.Value;
                }
            }

            _panel.Children.Clear();
            if (script is not null)
            {
                foreach (var parameter in script.Parameters)
                {
                    AddParameter(parameter);
                }
            }

            _updating = false;
            ValuesChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool TryValidate(out string? error)
        {
            error = null;
            return _script is not null && PowerScriptParameterValues.TryCreate(_script, _values, out _, out error);
        }

        private void AddParameter(PowerScriptParameter parameter)
        {
            string displayName = string.IsNullOrWhiteSpace(parameter.Label) ? parameter.Name : parameter.Label;
            var group = new StackPanel { Spacing = 4 };
            group.Children.Add(new TextBlock
            {
                Text = displayName,
                TextWrapping = TextWrapping.Wrap,
            });
            if (!string.IsNullOrWhiteSpace(parameter.Description))
            {
                group.Children.Add(new TextBlock { Text = parameter.Description, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
            }

            bool supplied = _values.TryGetValue(parameter.Name, out string? suppliedValue);
            bool requiredInput = parameter.IsRequired && parameter.Default is null;
            bool provideValue = supplied || requiredInput;
            string text = supplied ? suppliedValue ?? string.Empty : parameter.Default ?? string.Empty;
            Func<string> read;
            FrameworkElement editor;

            if (parameter.Type.Equals("bool", StringComparison.OrdinalIgnoreCase))
            {
                var toggle = new ToggleSwitch { IsOn = string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) };
                read = () => toggle.IsOn ? "true" : "false";
                toggle.Toggled += (_, _) => UpdateValue(parameter.Name, read(), toggle.IsEnabled);
                editor = toggle;
            }
            else if (parameter.Options.Count > 0)
            {
                var choice = new ComboBox { ItemsSource = parameter.Options, HorizontalAlignment = HorizontalAlignment.Stretch, SelectedItem = text };
                read = () => choice.SelectedItem as string ?? string.Empty;
                choice.SelectionChanged += (_, _) => UpdateValue(parameter.Name, read(), choice.IsEnabled);
                editor = choice;
            }
            else if (parameter.Type.Equals("int", StringComparison.OrdinalIgnoreCase))
            {
                var number = new NumberBox
                {
                    Minimum = parameter.Min ?? int.MinValue,
                    Maximum = parameter.Max ?? int.MaxValue,
                    SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                    Value = double.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out double value) ? value : double.NaN,
                };
                read = () => double.IsNaN(number.Value) ? string.Empty : number.Value.ToString(CultureInfo.InvariantCulture);
                number.ValueChanged += (_, _) => UpdateValue(parameter.Name, read(), number.IsEnabled);
                editor = number;
            }
            else
            {
                var box = new TextBox { Text = text, HorizontalAlignment = HorizontalAlignment.Stretch };
                read = () => box.Text;
                box.TextChanged += (_, _) => UpdateValue(parameter.Name, read(), box.IsEnabled);
                editor = box;
                if (parameter.Type.Equals("file", StringComparison.OrdinalIgnoreCase))
                {
                    var fileRow = new Grid { ColumnSpacing = 8 };
                    fileRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    fileRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var browse = new Button { Content = ResourceHelper.GetString("PowerScript_Browse") };
                    Grid.SetColumn(browse, 1);
                    fileRow.Children.Add(box);
                    fileRow.Children.Add(browse);
                    int generation = _generation;
                    browse.Click += async (_, _) =>
                    {
                        try
                        {
                            var picker = new Microsoft.Windows.Storage.Pickers.FileOpenPicker(App.MainWindow.AppWindow.Id);
                            picker.FileTypeFilter.Add("*");
                            var selected = await picker.PickSingleFileAsync();
                            if (generation == _generation && selected is not null)
                            {
                                box.Text = selected.Path;
                            }
                        }
                        catch (Exception)
                        {
                            if (generation == _generation)
                            {
                                group.Children.Add(new TextBlock { Text = ResourceHelper.GetString("PowerScript_FilePickerFailed"), TextWrapping = TextWrapping.Wrap });
                            }
                        }
                    };
                    editor = fileRow;
                }
            }

            var input = new ContentControl
            {
                Content = editor,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                IsEnabled = provideValue,
                IsTabStop = false,
            };
            AutomationProperties.SetName(editor, displayName);
            AutomationProperties.SetHelpText(editor, parameter.Description ?? string.Empty);
            if (editor is Grid fileInput)
            {
                foreach (var child in fileInput.Children)
                {
                    if (child is TextBox filePath)
                    {
                        AutomationProperties.SetName(filePath, displayName);
                        AutomationProperties.SetHelpText(filePath, parameter.Description ?? string.Empty);
                    }
                }
            }

            if (!requiredInput)
            {
                var provide = new CheckBox
                {
                    Content = ResourceHelper.GetString(parameter.Default is null ? "PowerScript_ProvideValue" : "PowerScript_OverrideDefault"),
                    IsChecked = provideValue,
                };
                AutomationProperties.SetName(provide, displayName + ": " + provide.Content);
                provide.Checked += (_, _) =>
                {
                    input.IsEnabled = true;
                    UpdateValue(parameter.Name, read(), true);
                };
                provide.Unchecked += (_, _) =>
                {
                    input.IsEnabled = false;
                    _values.Remove(parameter.Name);
                    RaiseChanged();
                };
                group.Children.Add(provide);
            }
            else if (!supplied)
            {
                _values[parameter.Name] = read();
            }

            group.Children.Add(input);
            _panel.Children.Add(group);
        }

        private void UpdateValue(string name, string value, bool provided)
        {
            if (_updating || !provided)
            {
                return;
            }

            _values[name] = value;
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            if (!_updating)
            {
                ValuesChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
