// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using AdvancedPaste.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using PowerScripts.Client;
using Windows.Storage.Pickers;
using Windows.System;

namespace AdvancedPaste.Controls;

public sealed partial class PowerScriptParametersPage : UserControl
{
    public PowerScriptParametersPage()
    {
        InitializeComponent();
        ParameterHelp.Text = Text("PowerScriptsParameterHelp");
        RunLabel.Text = Text("PowerScriptsRun");
        AutomationProperties.SetName(BackButton, Text("PowerScriptsBack"));
        AutomationProperties.SetName(HelpButton, Text("PowerScriptsParameterHelpButton"));
        AutomationProperties.SetName(RunButton, Text("PowerScriptsRun"));
        ToolTipService.SetToolTip(BackButton, Text("PowerScriptsBack"));
        ToolTipService.SetToolTip(HelpButton, Text("PowerScriptsParameterHelpButton"));
    }

    public static async Task<IReadOnlyDictionary<string, JsonElement>> ShowAsync(PowerScriptInfo script, CancellationToken token)
    {
        if (script.Parameters.Count == 0)
        {
            return null;
        }

        var window = ((App)Application.Current).GetMainWindow();
        var page = new PowerScriptParametersPage();
        page.ScriptTitle.Text = script.Name;
        ToolTipService.SetToolTip(page.ScriptTitle, script.Name);
        var completion = new TaskCompletionSource<IReadOnlyDictionary<string, JsonElement>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var integerRangeFormat = CompositeFormat.Parse(Text("PowerScriptsIntegerRange"));
        var invalidParameterFormat = CompositeFormat.Parse(Text("PowerScriptsInvalidParameter"));
        var fields = page.ParameterFields;
        var captionStyle = (Style)Application.Current.Resources["CaptionTextBlockStyle"];
        var descriptionStyle = (Style)page.Resources["ParameterDescriptionTextStyle"];
        var readers = new Dictionary<string, Func<string>>(StringComparer.Ordinal);
        var includes = new Dictionary<string, CheckBox>(StringComparer.Ordinal);
        var errors = page.ValidationError;
        Control initialFocus = null;
        foreach (var parameter in script.Parameters)
        {
            var group = new StackPanel { Spacing = 4 };
            var label = new TextBlock { Text = parameter.Label ?? parameter.Name, Style = captionStyle, TextWrapping = TextWrapping.Wrap };
            var include = new CheckBox
            {
                Content = parameter.IsRequired ? null : label,
                IsChecked = parameter.IsRequired || parameter.Default is not null,
                IsEnabled = !parameter.IsRequired,
            };
            AutomationProperties.SetAutomationId(include, "PowerScriptParameterInclude_" + parameter.Name);
            AutomationProperties.SetName(include, parameter.Label ?? parameter.Name);
            includes.Add(parameter.Name, include);
            if (parameter.IsRequired)
            {
                group.Children.Add(label);
            }
            else
            {
                group.Children.Add(include);
            }

            if (!string.IsNullOrEmpty(parameter.Description))
            {
                group.Children.Add(new TextBlock { Text = parameter.Description, Style = descriptionStyle });
            }

            if (parameter.Type == "int" && (parameter.Min is not null || parameter.Max is not null))
            {
                group.Children.Add(new TextBlock
                {
                    Text = string.Format(CultureInfo.CurrentCulture, integerRangeFormat, parameter.Min ?? int.MinValue, parameter.Max ?? int.MaxValue),
                    Style = descriptionStyle,
                });
            }

            Control editor;
            Button browse = null;
            if (parameter.Options.Count > 0)
            {
                var combo = new ComboBox
                {
                    ItemsSource = parameter.Options,
                    SelectedItem = parameter.Default,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                readers.Add(parameter.Name, () => combo.SelectedItem as string ?? throw new ArgumentException(parameter.Label ?? parameter.Name));
                editor = combo;
            }
            else if (parameter.Type == "bool")
            {
                var toggle = new ToggleSwitch { IsOn = bool.TryParse(parameter.Default, out var value) && value };
                readers.Add(parameter.Name, () => toggle.IsOn.ToString(CultureInfo.InvariantCulture));
                editor = toggle;
            }
            else
            {
                var text = new TextBox { Text = parameter.Default ?? string.Empty };
                text.KeyDown += (_, args) =>
                {
                    if (args.Key == VirtualKey.Enter)
                    {
                        args.Handled = true;
                        Submit();
                    }
                };
                readers.Add(parameter.Name, () => text.Text);
                editor = text;
                if (parameter.Type == "file")
                {
                    browse = new Button { Content = Text("PowerScriptsBrowse") };
                    AutomationProperties.SetAutomationId(browse, "PowerScriptParameterBrowse_" + parameter.Name);
                    browse.Click += async (_, _) =>
                    {
                        try
                        {
                            var picker = new FileOpenPicker();
                            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
                            picker.FileTypeFilter.Add("*");
                            var file = await picker.PickSingleFileAsync();
                            if (file is not null && !token.IsCancellationRequested)
                            {
                                text.Text = file.Path;
                                include.IsChecked = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            ManagedCommon.Logger.LogError("Unable to select a PowerScript parameter file.", ex);
                            errors.Text = Text("PowerScriptsFilePickerFailed");
                            errors.Visibility = Visibility.Visible;
                        }
                    };
                }
            }

            AutomationProperties.SetAutomationId(editor, "PowerScriptParameter_" + parameter.Name);
            AutomationProperties.SetName(editor, parameter.Label ?? parameter.Name);
            AutomationProperties.SetIsRequiredForForm(editor, parameter.IsRequired);
            editor.IsEnabled = include.IsChecked == true;
            initialFocus ??= editor.IsEnabled ? editor : include;
            include.Checked += (_, _) => editor.IsEnabled = true;
            include.Unchecked += (_, _) => editor.IsEnabled = false;
            if (browse is null)
            {
                group.Children.Add(editor);
            }
            else
            {
                var fileEditor = new Grid { ColumnSpacing = 8 };
                fileEditor.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                fileEditor.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                fileEditor.Children.Add(editor);
                Grid.SetColumn(browse, 1);
                fileEditor.Children.Add(browse);
                group.Children.Add(fileEditor);
            }

            fields.Children.Add(group);
        }

        var back = page.BackButton;
        back.Click += async (_, _) => await window.MainPage.CancelPowerScriptParametersAsync();
        page.RunButton.Click += (_, _) => Submit();

        void Submit()
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            try
            {
                var values = readers.Where(pair => includes[pair.Key].IsChecked == true).ToDictionary(pair => pair.Key, pair => pair.Value(), StringComparer.Ordinal);
                completion.TrySetResult(PowerScriptsService.CollectParameters(script.Parameters, values));
            }
            catch (ArgumentException ex)
            {
                errors.Text = string.Format(CultureInfo.CurrentCulture, invalidParameterFormat, ex.Message);
                errors.Visibility = Visibility.Visible;
            }
        }

        using var cancellation = token.Register(() => completion.TrySetCanceled(token));
        token.ThrowIfCancellationRequested();
        var result = await window.MainPage.ShowPowerScriptParametersAsync(page, initialFocus ?? back, completion.Task);
        token.ThrowIfCancellationRequested();
        return result;
    }

    private static string Text(string key) => ResourceLoaderInstance.ResourceLoader.GetString(key);
}
