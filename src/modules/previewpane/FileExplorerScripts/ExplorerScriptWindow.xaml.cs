// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FileExplorerScripts.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using PowerScripts.Client;
using Windows.Graphics;
using Windows.Storage.Pickers;

namespace FileExplorerScripts;

public sealed partial class ExplorerScriptWindow : Window
{
    private readonly Dictionary<string, Func<string?>> _values = new(StringComparer.Ordinal);
    private readonly TaskCompletionSource<(PowerScriptInfo Script, IReadOnlyDictionary<string, JsonElement> Parameters)> _completion = new();
    private readonly bool _showParameters;
    private bool _rendering;

    internal ExplorerScriptWindow(IReadOnlyList<PowerScriptInfo> scripts, IReadOnlyList<string> files, string? selectedId, bool showParameters)
    {
        _showParameters = showParameters;
        InitializeComponent();
        Title = Strings.Get("Title");
        Heading.Text = Strings.Get("ConfigureRun");
        Summary.Text = string.Format(CultureInfo.CurrentCulture, Strings.SelectionSummary, files.Count);
        ScriptPicker.Header = Strings.Get("ChooseScript");
        CancelButton.Content = Strings.Get("Cancel");
        RunButton.Content = Strings.Get("Run");
        AppWindow.Resize(new SizeInt32(640, 620));
        ScriptPicker.ItemsSource = scripts;
        ScriptPicker.SelectedItem = scripts.FirstOrDefault(script => script.Id == selectedId) ?? scripts[0];
        Closed += (_, _) => _completion.TrySetCanceled();
    }

    internal Task<(PowerScriptInfo Script, IReadOnlyDictionary<string, JsonElement> Parameters)> ConfigureAsync()
    {
        Activate();
        return _completion.Task;
    }

    private void ScriptPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _rendering = true;
        _values.Clear();
        ParameterFields.Children.Clear();
        if (ScriptPicker.SelectedItem is PowerScriptInfo script && _showParameters)
        {
            foreach (var parameter in script.Parameters)
            {
                var row = new StackPanel { Spacing = 4 };
                row.Children.Add(new TextBlock
                {
                    Text = (parameter.Label ?? parameter.Name) + " · " + Strings.Get(parameter.IsRequired ? "Required" : "Optional"),
                    TextWrapping = TextWrapping.Wrap,
                });
                if (!string.IsNullOrEmpty(parameter.Description))
                {
                    row.Children.Add(new TextBlock { Text = parameter.Description, TextWrapping = TextWrapping.Wrap });
                }

                FrameworkElement input;
                if (parameter.Type == "bool")
                {
                    var box = new CheckBox
                    {
                        Content = parameter.Label ?? parameter.Name,
                        IsThreeState = true,
                        IsChecked = parameter.Default is null ? null : bool.Parse(parameter.Default),
                    };
                    box.Checked += (_, _) => ValidateForm();
                    box.Unchecked += (_, _) => ValidateForm();
                    box.Indeterminate += (_, _) => ValidateForm();
                    _values[parameter.Name] = () => box.IsChecked switch
                    {
                        true => "true",
                        false => "false",
                        _ => null,
                    };
                    input = box;
                }
                else if (parameter.Type == "choice")
                {
                    var combo = new ComboBox
                    {
                        ItemsSource = parameter.Options,
                        SelectedItem = parameter.Default,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                    };
                    combo.SelectionChanged += (_, _) => ValidateForm();
                    _values[parameter.Name] = () => combo.SelectedItem as string;
                    input = combo;
                }
                else
                {
                    var hasValue = parameter.Default is not null;
                    var text = new TextBox { Text = parameter.Default ?? string.Empty };
                    text.TextChanged += (_, _) =>
                    {
                        hasValue = true;
                        ValidateForm();
                    };
                    _values[parameter.Name] = () => hasValue ? text.Text : null;
                    input = text;
                    if (parameter.Type == "file")
                    {
                        var browse = new Button { Content = Strings.Get("Browse") };
                        AutomationProperties.SetAutomationId(browse, "ExplorerParameterBrowse-" + parameter.Name);
                        browse.Click += async (_, _) =>
                        {
                            var picker = new FileOpenPicker();
                            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
                            picker.FileTypeFilter.Add("*");
                            var selected = await picker.PickSingleFileAsync();
                            if (selected is not null)
                            {
                                text.Text = selected.Path;
                            }
                        };
                        row.Children.Add(browse);
                    }
                }

                AutomationProperties.SetAutomationId(input, "ExplorerParameter-" + parameter.Name);
                AutomationProperties.SetName(input, parameter.Label ?? parameter.Name);
                AutomationProperties.SetIsRequiredForForm(input, parameter.IsRequired);
                row.Children.Add(input);
                ParameterFields.Children.Add(row);
            }
        }

        _rendering = false;
        ValidateForm();
    }

    private IReadOnlyDictionary<string, JsonElement> ReadParameters(PowerScriptInfo script) =>
        _showParameters
            ? ScriptParameters.Parse(script, _values.ToDictionary(item => item.Key, item => item.Value(), StringComparer.Ordinal))
            : ScriptParameters.GetDefaults(script);

    private void ValidateForm()
    {
        if (_rendering || ScriptPicker.SelectedItem is not PowerScriptInfo script)
        {
            return;
        }

        try
        {
            _ = ReadParameters(script);
            ValidationError.IsOpen = false;
            RunButton.IsEnabled = true;
        }
        catch (ArgumentException exception)
        {
            ValidationError.Message = _showParameters ? exception.Message : Strings.Get("MissingDefaults");
            ValidationError.IsOpen = true;
            RunButton.IsEnabled = false;
        }
    }

    private void RunButton_Click(object sender, RoutedEventArgs e)
    {
        if (ScriptPicker.SelectedItem is PowerScriptInfo script)
        {
            _completion.TrySetResult((script, ReadParameters(script)));
            AppWindow.Hide();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _completion.TrySetCanceled();
        Close();
    }
}
