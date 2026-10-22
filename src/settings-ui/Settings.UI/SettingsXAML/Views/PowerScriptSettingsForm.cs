// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

using CommunityToolkit.WinUI.Controls;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Microsoft.PowerToys.Settings.UI.Views;

/// <summary>Shared inline and dialog editor over sparse local overrides.</summary>
public sealed partial class PowerScriptSettingsForm : UserControl
{
    private static readonly string[] DisplayNamePath = { "title" };
    private static readonly string[] DescriptionPath = { "description" };
    private static readonly string[] RuntimePath = { "x-powerscript", "runtime" };
    private static readonly string[] FunctionPath = { "x-powerscript", "function" };
    private static readonly string[] InputPath = { "x-powerscript", "input" };
    private static readonly string[] OutputPath = { "x-powerscript", "output" };
    private static readonly string[] ExtensionsPath = { "x-powerscript", "extensions" };
    private static readonly string[] MinFilesPath = { "x-powerscript", "minFiles" };
    private static readonly string[] MaxFilesPath = { "x-powerscript", "maxFiles" };
    private static readonly string[] IconPath = { "x-powerscript", "icon" };
    private static readonly string[] CapabilitiesPath = { "x-powerscript", "capabilities" };
    private static readonly string[] ConsumersPath = { "x-powerscript", "consumers" };
    private static readonly string[] ActionIdPath = { "name" };
    private static readonly string[] CommandPath = { "x-execute", "command" };
    private static readonly string[] StdinPath = { "x-execute", "stdin" };
    private static readonly string[] ArgMapPath = { "x-execute", "argMap" };
    private static readonly string[] ExitCodesPath = { "x-execute", "exitCodes" };
    private static readonly string[] RuntimeChoices = { "powershell", "python" };
    private static readonly string[] IoChoices = { "none", "text", "files" };
    private static readonly string[] ParameterTypeChoices = { "string", "integer", "boolean" };
    private static readonly string[] ParameterFormatChoices = { string.Empty, "file-path" };
    private static readonly string[] StdinChoices = { string.Empty, "json" };
    private static readonly char[] ListSeparators = { ',', '\r', '\n' };
    private static readonly char[] LineSeparators = { '\r', '\n' };

    private readonly PowerScriptSettingsEditor _editor;
    private readonly bool _inline;
    private readonly List<Action> _commit = new();
    private readonly StackPanel _form = new() { Spacing = 16 };
    private readonly InfoBar _error = new() { IsClosable = false, Severity = InfoBarSeverity.Error };

    public PowerScriptSettingsForm(PowerScriptSettingsSnapshot snapshot, bool inline = false)
    {
        _editor = new(snapshot);
        _inline = inline;
        _form.Spacing = inline ? 8 : 16;
        var content = new StackPanel { Spacing = 16 };
        content.Children.Add(_error);
        content.Children.Add(_form);
        Content = content;
        BuildForm();
        if (snapshot.ValidationErrors is { Length: > 0 })
        {
            ShowError(string.Join(Environment.NewLine, snapshot.ValidationErrors));
        }
    }

    public bool TryGetOverrides(out JsonObject overrides)
    {
        overrides = null;
        if (!TryCommit())
        {
            return false;
        }

        if (_editor.Read("x-execute") is JsonObject execution &&
            execution["command"] is not JsonArray { Count: > 0 })
        {
            ShowError(Text("CommandRequired"));
            return false;
        }

        _error.IsOpen = false;
        overrides = (JsonObject)_editor.Overrides.DeepClone();
        return true;
    }

    private static string Text(string key) =>
        ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_Settings" + key);

    private static T Identify<T>(T control, string id)
        where T : DependencyObject
    {
        AutomationProperties.SetAutomationId(control, "PowerScriptsSettings_" + id);
        return control;
    }

    private void BuildForm(string expandedParameter = null)
    {
        _form.Children.Clear();
        _commit.Clear();
        AddText(_form, "DisplayName", DisplayNamePath);
        AddText(_form, "Description", DescriptionPath, multiline: true);
        AddChoice(_form, "Runtime", RuntimePath, RuntimeChoices);
        var inputType = AddChoice(_form, "Input", InputPath, IoChoices);
        AddChoice(_form, "Output", OutputPath, IoChoices);
        var fileFields = new StackPanel { Spacing = 8 };
        void UpdateFileFields() => fileFields.Visibility =
            string.Equals((inputType.SelectedItem as ComboBoxItem)?.Tag as string, "files", StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible : Visibility.Collapsed;
        AddList(fileFields, "Extensions", ExtensionsPath);
        AddNumber(fileFields, "MinFiles", MinFilesPath);
        AddNumber(fileFields, "MaxFiles", MaxFilesPath);
        inputType.SelectionChanged += (_, _) => UpdateFileFields();
        UpdateFileFields();
        _form.Children.Add(fileFields);
        AddList(_form, "Consumers", ConsumersPath, blankMeansNull: true);

        var parameters = new StackPanel { Spacing = 12 };
        var parameterExpander = Identify(
            new Expander
            {
                Header = Text("Parameters"),
                Content = parameters,
                IsExpanded = _editor.ParameterNames.Any(),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            },
            "Parameters");
        _form.Children.Add(parameterExpander);
        AddParameters(parameters, expandedParameter);

        var advanced = new StackPanel { Spacing = 12 };
        _form.Children.Add(Identify(
            new Expander
            {
                Header = Text("Advanced"),
                Content = advanced,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            },
            "Advanced"));
        AddText(advanced, "Function", FunctionPath);
        AddText(advanced, "Icon", IconPath);
        AddList(advanced, "Capabilities", CapabilitiesPath);
        advanced.Children.Add(new TextBlock { Text = Text("ExecutionHelp"), TextWrapping = TextWrapping.Wrap });
        advanced.Children.Add(MakeButton("UseBuiltInRuntime", "UseBuiltInRuntime", () =>
        {
            if (TryCommit())
            {
                _editor.Set(null, "x-execute");
                BuildForm();
            }
        }));
        advanced.Children.Add(MakeButton("ResetExecution", "ResetRecommended", () =>
        {
            if (TryCommit())
            {
                _editor.Reset("x-execute");
                BuildForm();
            }
        }));
        AddText(advanced, "ActionId", ActionIdPath);
        AddList(advanced, "Command", CommandPath, lines: true, blankMeansNull: true);
        AddChoice(advanced, "Stdin", StdinPath, StdinChoices);
        AddMap(advanced, "ArgMap", ArgMapPath);
        AddMap(advanced, "ExitCodes", ExitCodesPath);
    }

    private void AddParameters(StackPanel panel, string expandedParameter)
    {
        var reset = MakeButton("ResetParameters", "ResetRecommended", () =>
        {
            if (TryCommit())
            {
                _editor.Reset("inputSchema");
                BuildForm();
            }
        });
        panel.Children.Add(reset);
        foreach (var name in _editor.ParameterNames.ToArray())
        {
            if (_editor.Read("inputSchema", "properties", name) is not JsonObject)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(_editor.Read("inputSchema", "properties", name, "contentMediaType")?.ToString()) &&
                !string.Equals(_editor.Read("inputSchema", "properties", name, "format")?.ToString(), "file-path", StringComparison.OrdinalIgnoreCase))
            {
                // Primary file inputs belong to the I/O contract, not the consumer's prompt form.
                continue;
            }

            var fields = new StackPanel { Spacing = 12 };
            var parameterType = _editor.Read("inputSchema", "properties", name, "type")?.ToString() ?? "string";
            var typeLabel = new TextBlock
            {
                Text = ChoiceLabel(parameterType) + " (" + parameterType + ")",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                TextWrapping = TextWrapping.Wrap,
            };
            var nameLabel = new TextBlock
            {
                Text = name,
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                TextWrapping = TextWrapping.Wrap,
            };
            var summary = new TextBlock
            {
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            AutomationProperties.SetAutomationId(typeLabel, "PowerScriptsSettings_ParameterType_" + name);
            AutomationProperties.SetAutomationId(summary, "PowerScriptsSettings_ParameterSummary_" + name);
            var header = new StackPanel { Spacing = 4 };
            header.Children.Add(nameLabel);
            header.Children.Add(typeLabel);
            header.Children.Add(summary);
            var parameterExpander = Identify(
                new Expander
                {
                    Header = header,
                    Content = fields,
                    IsExpanded = name == expandedParameter,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                },
                "Parameter_" + name);
            panel.Children.Add(parameterExpander);
            void UpdateAccessibleName() => AutomationProperties.SetName(parameterExpander, nameLabel.Text + ", " + typeLabel.Text + ", " + summary.Text);
            fields.Children.Add(MakeButton("ResetParameter_" + name, "ResetRecommended", () =>
            {
                if (TryCommit())
                {
                    _editor.ResetParameter(name);
                    BuildForm();
                }
            }));
            string[] Path(string leaf) => new[] { "inputSchema", "properties", name, leaf };
            var type = AddChoice(fields, "ParameterType", Path("type"), ParameterTypeChoices);
            type.SelectionChanged += (_, _) =>
            {
                var value = (type.SelectedItem as ComboBoxItem)?.Tag as string ?? parameterType;
                typeLabel.Text = ChoiceLabel(value) + " (" + value + ")";
                UpdateAccessibleName();
            };
            var defaultValue = AddText(fields, "Default", Path("default"), parse: value => ParseDefault(name, value));

            var required = Identify(new CheckBox { Content = Text("Required") }, "Required_" + name);
            required.IsChecked = (_editor.Read("inputSchema", "required") as JsonArray)?
                .Any(item => item?.ToString() == name) == true;
            void UpdateSummary()
            {
                summary.Text = required.IsChecked == true ? Text("Required") : Text("Optional");
                summary.Text += " \u00b7 " + Text("DefaultSummary").Replace("{0}", DisplayValue(defaultValue.Text, Path("default")), StringComparison.Ordinal);

                UpdateAccessibleName();
            }

            required.Checked += (_, _) => UpdateSummary();
            required.Unchecked += (_, _) => UpdateSummary();
            defaultValue.TextChanged += (_, _) => UpdateSummary();
            UpdateSummary();
            var requiredState = new PowerScriptSettingsEditor.FieldEditState<bool>(required.IsChecked == true);
            _commit.Add(() =>
            {
                var current = required.IsChecked == true;
                if (requiredState.HasChangedDisplay(current))
                {
                    _editor.SetParameterRequired(name, current);
                    requiredState.AcceptDisplay(current);
                }
            });
            if (_inline)
            {
                AddField(
                    fields,
                    "Requirement",
                    new[] { "Required", name },
                    required,
                    () =>
                    {
                        var recommended = (_editor.Read("inputSchema", "required") as JsonArray)?
                            .Any(item => item?.ToString() == name) == true;
                        requiredState.AcceptDisplay(recommended);
                        required.IsChecked = recommended;
                        UpdateSummary();
                    },
                    () => _editor.ResetParameterRequired(name));
            }
            else
            {
                fields.Children.Add(required);
            }

            var label = AddText(fields, "DisplayName", Path("title"));
            void UpdateLabel()
            {
                nameLabel.Text = string.IsNullOrWhiteSpace(label.Text) || label.Text == name
                    ? name : label.Text + " (" + name + ")";
                UpdateAccessibleName();
            }

            label.TextChanged += (_, _) => UpdateLabel();
            UpdateLabel();
            AddText(fields, "Description", Path("description"), multiline: true);
            var options = new StackPanel { Spacing = 8 };
            fields.Children.Add(Identify(
                new Expander
                {
                    Header = Text("ParameterOptions"),
                    Content = options,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                },
                "ParameterOptions_" + name));
            AddChoice(options, "Format", Path("format"), ParameterFormatChoices);
            var choices = new StackPanel { Spacing = 8 };
            AddList(choices, "Choices", Path("enum"), lines: true, blankMeansNull: true);
            void UpdateChoicesVisibility() => choices.Visibility =
                (type.SelectedItem as ComboBoxItem)?.Tag as string == "string"
                    ? Visibility.Visible : Visibility.Collapsed;
            type.SelectionChanged += (_, _) => UpdateChoicesVisibility();
            UpdateChoicesVisibility();
            options.Children.Add(choices);
            AddNumber(options, "Minimum", Path("minimum"));
            AddNumber(options, "Maximum", Path("maximum"));
            fields.Children.Add(MakeButton("Remove_" + name, "RemoveParameter", () =>
            {
                if (TryCommit())
                {
                    _editor.Set(null, "inputSchema", "properties", name);
                    _editor.SetParameterRequired(name, false);
                    BuildForm();
                }
            }));
        }

        var newName = Identify(new TextBox { Header = Text("ParameterName"), MinWidth = 120 }, "NewParameterName");
        var newType = Identify(
            new ComboBox
            {
                Header = Text("ParameterType"),
                ItemsSource = ParameterTypeChoices.Select(value => new ComboBoxItem { Tag = value, Content = ChoiceLabel(value) }).ToArray(),
                SelectedIndex = 0,
                MinWidth = 120,
            },
            "NewParameterType");
        AutomationProperties.SetName(newType, Text("ParameterType"));
        var add = MakeButton("AddParameter", "AddParameter", () =>
        {
            var name = newName.Text.Trim();
            if (string.IsNullOrEmpty(name) || _editor.ParameterNames.Contains(name, StringComparer.Ordinal))
            {
                ShowError(Text("ParameterNameError"));
                return;
            }

            if (newType.SelectedItem is not ComboBoxItem { Tag: string type })
            {
                ShowError(Text("ChooseParameterType"));
                return;
            }

            if (TryCommit())
            {
                if (_editor.Read("inputSchema", "type") is null)
                {
                    _editor.Set(JsonValue.Create("object"), "inputSchema", "type");
                }

                _editor.Set(new JsonObject { ["type"] = type }, "inputSchema", "properties", name);
                BuildForm(name);
            }
        });
        var addFields = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        addFields.ColumnDefinitions.Add(new ColumnDefinition());
        addFields.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        addFields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        addFields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetColumn(newType, 1);
        Grid.SetRow(add, 1);
        addFields.Children.Add(newName);
        addFields.Children.Add(newType);
        addFields.Children.Add(add);
        panel.Children.Add(addFields);
    }

    private JsonNode ParseDefault(string name, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return _editor.Read("inputSchema", "properties", name, "type")?.ToString() switch
        {
            "boolean" => JsonValue.Create(bool.Parse(value)),
            "integer" => JsonValue.Create(long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)),
            "number" => JsonValue.Create(double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture)),
            "array" or "object" => throw new FormatException(Text("StructuredDefaultHelp")),
            _ => JsonValue.Create(value),
        };
    }

    private TextBox AddText(StackPanel panel, string label, string[] path, bool multiline = false, Func<string, JsonNode> parse = null)
    {
        var initialText = _editor.Read(path)?.ToString() ?? string.Empty;
        var state = new PowerScriptSettingsEditor.FieldEditState<string>(NormalizeDisplayText(initialText));
        var input = Identify(new TextBox { AcceptsReturn = multiline, TextWrapping = TextWrapping.Wrap }, string.Join("_", path));
        input.Text = initialText;
        _commit.Add(() =>
        {
            var current = NormalizeDisplayText(input.Text);
            if (state.HasChangedDisplay(current))
            {
                _editor.Set(parse is null ? JsonValue.Create(input.Text) : parse(input.Text), path);
                state.AcceptDisplay(current);
            }
        });
        AddField(panel, label, path, input, () =>
        {
            var recommended = _editor.Read(path)?.ToString() ?? string.Empty;
            state.AcceptDisplay(NormalizeDisplayText(recommended));
            input.Text = recommended;
        });
        return input;
    }

    private ComboBox AddChoice(StackPanel panel, string label, string[] path, string[] values)
    {
        var state = new PowerScriptSettingsEditor.FieldEditState<string>(string.Empty);
        var input = Identify(new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch }, string.Join("_", path));
        void Refresh()
        {
            var selected = _editor.Read(path)?.ToString() ?? string.Empty;
            var choices = values.Contains(selected, StringComparer.OrdinalIgnoreCase) ? values : values.Append(selected).ToArray();
            var items = choices.Select(choice => new ComboBoxItem { Tag = choice, Content = ChoiceLabel(choice) }).ToArray();
            var selectedItem = items.FirstOrDefault(item => string.Equals((string)item.Tag, selected, StringComparison.OrdinalIgnoreCase));
            state.AcceptDisplay(selectedItem?.Tag as string ?? string.Empty);
            input.ItemsSource = items;
            input.SelectedItem = selectedItem;
        }

        Refresh();
        _commit.Add(() =>
        {
            var selected = (input.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty;
            if (state.HasChangedDisplay(selected))
            {
                _editor.Set(string.IsNullOrEmpty(selected) ? null : JsonValue.Create(selected), path);
                state.AcceptDisplay(selected);
            }
        });
        AddField(panel, label, path, input, Refresh);
        return input;
    }

    private static string ChoiceLabel(string value) => value.ToLowerInvariant() switch
    {
        "" => Text("ChoiceUnspecified"),
        "powershell" => "PowerShell",
        "python" => "Python",
        "json" => "JSON",
        "none" => ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_IoNone"),
        "text" => ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_IoText"),
        "files" => ResourceLoaderInstance.ResourceLoader.GetString("PowerScripts_IoFiles"),
        "string" => Text("ChoiceString"),
        "integer" => Text("ChoiceInteger"),
        "number" => Text("ChoiceNumber"),
        "boolean" => Text("ChoiceBoolean"),
        "array" => Text("ChoiceArray"),
        "object" => Text("ChoiceObject"),
        "file-path" => Text("ChoiceFilePath"),
        _ => value,
    };

    private void AddNumber(StackPanel panel, string label, string[] path)
    {
        double ReadValue() => double.TryParse(_editor.Read(path)?.ToString(), CultureInfo.InvariantCulture, out var number) ? number : double.NaN;
        var initialValue = ReadValue();
        var state = new PowerScriptSettingsEditor.FieldEditState<double>(initialValue);
        var input = Identify(new NumberBox { Value = initialValue, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact }, string.Join("_", path));
        _commit.Add(() =>
        {
            var current = input.Value;
            if (state.HasChangedDisplay(current))
            {
                if (!double.IsNaN(current) && (current != Math.Truncate(current) || current < int.MinValue || current > int.MaxValue))
                {
                    throw new FormatException(Text("IntegerError"));
                }

                _editor.Set(double.IsNaN(current) ? null : JsonValue.Create((int)current), path);
                state.AcceptDisplay(current);
            }
        });
        AddField(panel, label, path, input, () =>
        {
            var recommended = ReadValue();
            state.AcceptDisplay(recommended);
            input.Value = recommended;
        });
    }

    private void AddList(StackPanel panel, string label, string[] path, bool lines = false, bool blankMeansNull = false)
    {
        string FormatValue() => _editor.Read(path) is JsonArray array
            ? string.Join(lines ? Environment.NewLine : ", ", array.Select(item => item?.ToString()))
            : string.Empty;
        var initialText = FormatValue();
        var state = new PowerScriptSettingsEditor.FieldEditState<string>(NormalizeDisplayText(initialText));
        var input = Identify(new TextBox { Text = initialText, AcceptsReturn = lines, TextWrapping = TextWrapping.Wrap }, string.Join("_", path));
        if (path.SequenceEqual(ConsumersPath))
        {
            input.PlaceholderText = Text("AllConsumers");
        }

        _commit.Add(() =>
        {
            var current = NormalizeDisplayText(input.Text);
            if (state.HasChangedDisplay(current))
            {
                // An empty command argument is meaningful; do not split/trim it like comma-separated IDs.
                var values = lines
                    ? input.Text.Length == 0
                        ? Array.Empty<string>()
                        : input.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n')
                    : input.Text.Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries)
                        .Select(item => item.Trim()).Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
                _editor.Set(
                    values.Length == 0 && blankMeansNull
                        ? null
                        : new JsonArray(values.Select(item => (JsonNode)JsonValue.Create(item)).ToArray()),
                    path);
                state.AcceptDisplay(current);
            }
        });
        AddField(panel, label, path, input, () =>
        {
            var recommended = FormatValue();
            state.AcceptDisplay(NormalizeDisplayText(recommended));
            input.Text = recommended;
        });
    }

    private void AddMap(StackPanel panel, string label, string[] path)
    {
        string FormatValue() => _editor.Read(path) is JsonObject map
            ? string.Join(Environment.NewLine, map.Where(pair => pair.Value is not null).Select(pair => pair.Key + "=" + pair.Value))
            : string.Empty;
        var initialText = FormatValue();
        var state = new PowerScriptSettingsEditor.FieldEditState<string>(NormalizeDisplayText(initialText));
        var input = Identify(new TextBox { Text = initialText, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap }, string.Join("_", path));
        _commit.Add(() =>
        {
            var current = NormalizeDisplayText(input.Text);
            if (!state.HasChangedDisplay(current))
            {
                return;
            }

            var map = new JsonObject();
            foreach (var line in input.Text.Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    throw new FormatException(Text("MapError"));
                }

                map.Add(line[..separator].Trim(), JsonValue.Create(line[(separator + 1)..]));
            }

            var original = _editor.Read(path) as JsonObject;
            foreach (var pair in map)
            {
                if (original is null || !JsonNode.DeepEquals(original[pair.Key], pair.Value))
                {
                    _editor.Set(pair.Value, path.Append(pair.Key).ToArray());
                }
            }

            if (original is not null)
            {
                foreach (var key in original.Select(pair => pair.Key).Where(key => !map.ContainsKey(key)).ToArray())
                {
                    _editor.Set(null, path.Append(key).ToArray());
                }
            }

            state.AcceptDisplay(current);
        });
        AddField(panel, label, path, input, () =>
        {
            var recommended = FormatValue();
            state.AcceptDisplay(NormalizeDisplayText(recommended));
            input.Text = recommended;
        });
    }

    private static string NormalizeDisplayText(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private string DisplayValue(string value, string[] path)
    {
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (path.SequenceEqual(ConsumersPath))
        {
            return Text("AllConsumers");
        }

        return path[^1] == "default" && _editor.Read(path)?.ToJsonString() == "\"\"" ? "\"\"" : Text("NotSet");
    }

    private void AddField(StackPanel panel, string label, string[] path, Control input, Action refresh, Action resetOverride = null)
    {
        ToggleButton edit = null;
        Action updateDisplay = null;
        var reset = MakeButton("Reset_" + string.Join("_", path), "ResetRecommended", () =>
        {
            if (resetOverride is null)
            {
                _editor.Reset(path);
            }
            else
            {
                resetOverride();
            }

            refresh();
            updateDisplay?.Invoke();
            if (edit is not null)
            {
                edit.IsChecked = false;
            }
        });
        AutomationProperties.SetName(reset, Text("ResetRecommended") + ": " + Text(label));
        AutomationProperties.SetName(input, Text(label));
        if (_inline)
        {
            var controls = new Grid { ColumnSpacing = 8, MinWidth = 320, MaxWidth = 420 };
            controls.ColumnDefinitions.Add(new ColumnDefinition());
            controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var value = Identify(
                new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                },
                "Value_" + string.Join("_", path));
            string ReadValue() => input switch
            {
                TextBox text => text.Text,
                ComboBox choice => (choice.SelectedItem as ComboBoxItem)?.Content?.ToString(),
                NumberBox number => double.IsNaN(number.Value) ? string.Empty : number.Value.ToString(CultureInfo.CurrentCulture),
                CheckBox check => Text(check.IsChecked == true ? "Required" : "Optional"),
                _ => throw new InvalidOperationException("Unsupported script settings control."),
            };
            updateDisplay = () => value.Text = DisplayValue(ReadValue(), path);
            switch (input)
            {
                case TextBox text:
                    text.TextChanged += (_, _) => updateDisplay();
                    break;
                case ComboBox choice:
                    choice.SelectionChanged += (_, _) => updateDisplay();
                    break;
                case NumberBox number:
                    number.ValueChanged += (_, _) => updateDisplay();
                    break;
                case CheckBox check:
                    check.Checked += (_, _) => updateDisplay();
                    check.Unchecked += (_, _) => updateDisplay();
                    break;
            }

            updateDisplay();
            edit = Identify(
                new ToggleButton
                {
                    Content = new SymbolIcon(Symbol.Edit),
                    VerticalAlignment = VerticalAlignment.Center,
                },
                "Edit_" + string.Join("_", path));
            var editName = Text("Edit") + ": " + Text(label);
            AutomationProperties.SetName(edit, editName);
            ToolTipService.SetToolTip(edit, editName);
            void UpdateEditMode()
            {
                var editing = edit.IsChecked == true;
                input.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
                value.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
                if (editing)
                {
                    input.Focus(FocusState.Programmatic);
                }
            }

            edit.Checked += (_, _) => UpdateEditMode();
            edit.Unchecked += (_, _) => UpdateEditMode();
            UpdateEditMode();
            input.MinWidth = 200;
            reset.Content = new SymbolIcon(Symbol.Refresh);
            reset.VerticalAlignment = VerticalAlignment.Center;
            ToolTipService.SetToolTip(reset, Text("ResetRecommended"));
            Grid.SetColumn(edit, 1);
            Grid.SetColumn(reset, 2);
            controls.Children.Add(value);
            controls.Children.Add(input);
            controls.Children.Add(edit);
            controls.Children.Add(reset);
            panel.Children.Add(new SettingsCard
            {
                Header = label switch
                {
                    "DisplayName" => Text("ShortName"),
                    "Consumers" => Text("ShortConsumers"),
                    "Extensions" => Text("ShortExtensions"),
                    "Capabilities" => Text("ShortCapabilities"),
                    "Icon" => Text("ShortIcon"),
                    "Function" => Text("ShortFunction"),
                    "MinFiles" => Text("ShortMinFiles"),
                    "MaxFiles" => Text("ShortMaxFiles"),
                    "Default" => Text("ShortDefault"),
                    _ => Text(label),
                },
                Content = controls,
                ContentAlignment = ContentAlignment.Right,
            });
            ToolTipService.SetToolTip(input, Text(label) + (label == "Consumers" ? Environment.NewLine + Text("ConsumersHelp") : string.Empty));
            return;
        }

        var field = new StackPanel { Spacing = 4 };
        var header = new Grid { ColumnSpacing = 8 };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new TextBlock { Text = Text(label), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        header.Children.Add(title);
        Grid.SetColumn(reset, 1);
        header.Children.Add(reset);
        field.Children.Add(header);
        field.Children.Add(input);
        panel.Children.Add(field);
    }

    private static Button MakeButton(string id, string label, Action action)
    {
        var button = Identify(new Button { Content = Text(label) }, id);
        button.Click += (_, _) => action();
        return button;
    }

    private bool TryCommit()
    {
        try
        {
            foreach (var commit in _commit)
            {
                commit();
            }

            return true;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException or InvalidOperationException)
        {
            ShowError(ex.Message);
            return false;
        }
    }

    private void ShowError(string message)
    {
        _error.Message = message;
        _error.IsOpen = true;
    }
}
