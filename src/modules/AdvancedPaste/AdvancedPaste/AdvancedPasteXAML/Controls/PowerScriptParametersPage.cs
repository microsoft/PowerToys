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

internal static class PowerScriptParametersPage
{
    public static async Task<IReadOnlyDictionary<string, JsonElement>> ShowAsync(PowerScriptInfo script, CancellationToken token)
    {
        if (script.Parameters.Count == 0)
        {
            return null;
        }

        var window = ((App)Application.Current).GetMainWindow();
        var completion = new TaskCompletionSource<IReadOnlyDictionary<string, JsonElement>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var integerRangeFormat = CompositeFormat.Parse(Text("PowerScriptsIntegerRange"));
        var invalidParameterFormat = CompositeFormat.Parse(Text("PowerScriptsInvalidParameter"));
        var fields = new StackPanel { Spacing = 12 };
        fields.Children.Add(new TextBlock { Text = Text("PowerScriptsParameterHelp"), TextWrapping = TextWrapping.Wrap });
        var readers = new Dictionary<string, Func<string>>(StringComparer.Ordinal);
        var includes = new Dictionary<string, CheckBox>(StringComparer.Ordinal);
        var errors = new TextBlock { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        AutomationProperties.SetAutomationId(errors, "PowerScriptParameterError");
        Control initialFocus = null;
        foreach (var parameter in script.Parameters)
        {
            var group = new StackPanel { Spacing = 8 };
            var include = new CheckBox
            {
                Content = parameter.Label ?? parameter.Name,
                IsChecked = parameter.IsRequired || parameter.Default is not null,
                IsEnabled = !parameter.IsRequired,
            };
            AutomationProperties.SetAutomationId(include, "PowerScriptParameterInclude_" + parameter.Name);
            includes.Add(parameter.Name, include);
            group.Children.Add(include);
            if (!string.IsNullOrEmpty(parameter.Description))
            {
                group.Children.Add(new TextBlock { Text = parameter.Description, TextWrapping = TextWrapping.Wrap });
            }

            if (parameter.Type == "int" && (parameter.Min is not null || parameter.Max is not null))
            {
                group.Children.Add(new TextBlock
                {
                    Text = string.Format(CultureInfo.CurrentCulture, integerRangeFormat, parameter.Min ?? int.MinValue, parameter.Max ?? int.MaxValue),
                    TextWrapping = TextWrapping.Wrap,
                });
            }

            Control editor;
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
                    var browse = new Button { Content = Text("PowerScriptsBrowse") };
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
                    group.Children.Add(browse);
                }
            }

            AutomationProperties.SetAutomationId(editor, "PowerScriptParameter_" + parameter.Name);
            AutomationProperties.SetName(editor, parameter.Label ?? parameter.Name);
            editor.IsEnabled = include.IsChecked == true;
            initialFocus ??= editor.IsEnabled ? editor : include;
            include.Checked += (_, _) => editor.IsEnabled = true;
            include.Unchecked += (_, _) => editor.IsEnabled = false;
            group.Children.Add(editor);
            fields.Children.Add(group);
        }

        var back = new Button
        {
            Content = new FontIcon { Glyph = "\uE72B" },
            Style = (Style)Application.Current.Resources["SubtleButtonStyle"],
        };
        AutomationProperties.SetAutomationId(back, "PowerScriptParametersBack");
        AutomationProperties.SetName(back, Text("PowerScriptsBack"));
        ToolTipService.SetToolTip(back, Text("PowerScriptsBack"));
        back.Click += async (_, _) => await window.MainPage.CancelPowerScriptParametersAsync();
        var header = new Grid { ColumnSpacing = 8 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.Children.Add(back);
        var title = new TextBlock
        {
            Text = script.Name,
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(title, 1);
        header.Children.Add(title);

        var heading = new StackPanel { Spacing = 12 };
        heading.Children.Add(header);
        heading.Children.Add(errors);
        var scroll = new ScrollViewer
        {
            Content = fields,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        AutomationProperties.SetAutomationId(scroll, "PowerScriptParametersScroll");
        var run = new Button
        {
            Content = Text("PowerScriptsRun"),
            HorizontalAlignment = HorizontalAlignment.Right,
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
        };
        AutomationProperties.SetAutomationId(run, "PowerScriptParametersRun");
        run.Click += (_, _) => Submit();

        var content = new Grid { Margin = new Thickness(20, 0, 20, 16), RowSpacing = 12 };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.Children.Add(heading);
        Grid.SetRow(scroll, 1);
        content.Children.Add(scroll);
        Grid.SetRow(run, 2);
        content.Children.Add(run);
        AutomationProperties.SetAutomationId(content, "PowerScriptParametersPage");

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
        var result = await window.MainPage.ShowPowerScriptParametersAsync(content, initialFocus ?? back, completion.Task);
        token.ThrowIfCancellationRequested();
        return result;
    }

    private static string Text(string key) => ResourceLoaderInstance.ResourceLoader.GetString(key);
}
