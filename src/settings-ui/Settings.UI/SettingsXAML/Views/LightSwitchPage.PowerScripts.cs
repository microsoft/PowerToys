// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using PowerScripts.Client;

namespace Microsoft.PowerToys.Settings.UI.Views;

public sealed partial class LightSwitchPage
{
    private bool _editingThemeScript;

    private static string ScriptResource(string key) =>
        ResourceLoaderInstance.ResourceLoader.GetString(key);

    private async void ThemeScript_Click(object sender, RoutedEventArgs e)
    {
        if (_editingThemeScript || sender is not Button { Tag: string theme })
        {
            return;
        }

        _editingThemeScript = true;
        var light = theme == "light";
        var binding = (light ? ViewModel.ModuleSettings.Properties.LightThemeScript : ViewModel.ModuleSettings.Properties.DarkThemeScript)
            ?? new LightSwitchScriptBinding();
        try
        {
            using var client = new PowerScriptsClient(PowerScriptConsumer.LightSwitch);
            IReadOnlyList<PowerScriptInfo> scripts = Array.Empty<PowerScriptInfo>();
            var status = new InfoBar { IsClosable = false, Severity = InfoBarSeverity.Warning };
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var state = await client.GetConsumerStateAsync(timeout.Token);
                if (!state.IsEnabled)
                {
                    status.Message = ScriptResource("LightSwitch_ThemeScriptsEnable");
                    status.IsOpen = true;
                }
                else
                {
                    scripts = (await client.ListAsync(timeout.Token))
                        .Where(script => script.Input == PowerScriptIO.None && script.Output == PowerScriptIO.None)
                        .OrderBy(script => script.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ToArray();
                }
            }
            catch (Exception exception)
            {
                status.Message = ScriptResource("LightSwitch_ThemeScriptsUnavailable") + Environment.NewLine + exception.Message;
                status.IsOpen = true;
            }

            var selector = new ComboBox
            {
                Header = ScriptResource("LightSwitch_ThemeScriptSelector"),
                DisplayMemberPath = nameof(PowerScriptInfo.Name),
                ItemsSource = scripts,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            AutomationProperties.SetAutomationId(selector, "LightSwitchThemeScriptSelector");
            var parameters = new StackPanel { Spacing = 12 };
            var fields = new Dictionary<string, (CheckBox Include, TextBox Value)>(StringComparer.Ordinal);
            var content = new StackPanel { Spacing = 16 };
            content.Children.Add(new TextBlock { Text = ScriptResource("LightSwitch_ThemeScriptsHelp"), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(status);
            content.Children.Add(selector);
            content.Children.Add(parameters);
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = ScriptResource(light ? "LightSwitch_LightThemeScript/Header" : "LightSwitch_DarkThemeScript/Header"),
                Content = new ScrollViewer { Content = content },
                PrimaryButtonText = ScriptResource("LightSwitch_ThemeScriptSave"),
                SecondaryButtonText = ScriptResource("LightSwitch_ThemeScriptClear"),
                CloseButtonText = ScriptResource("LightSwitch_ThemeScriptCancel"),
                DefaultButton = ContentDialogButton.Primary,
                IsPrimaryButtonEnabled = false,
                IsSecondaryButtonEnabled = !string.IsNullOrEmpty(binding.Id),
            };
            AutomationProperties.SetAutomationId(dialog, "LightSwitchThemeScriptDialog");
            selector.SelectionChanged += (_, _) =>
            {
                fields.Clear();
                parameters.Children.Clear();
                if (selector.SelectedItem is not PowerScriptInfo selected)
                {
                    dialog.IsPrimaryButtonEnabled = false;
                    return;
                }

                dialog.IsPrimaryButtonEnabled = true;
                status.Message = selected.Trusted ? string.Empty : ScriptResource("LightSwitch_ThemeScriptApproval");
                status.IsOpen = !selected.Trusted;
                foreach (var parameter in selected.Parameters)
                {
                    var supplied = selected.Id == binding.Id && binding.Parameters.TryGetValue(parameter.Name, out _);
                    var required = parameter.IsRequired && parameter.Default is null;
                    var include = new CheckBox
                    {
                        Content = ScriptResource("LightSwitch_ThemeScriptSupplyValue"),
                        IsChecked = supplied || required,
                        IsEnabled = !required,
                    };
                    var value = new TextBox
                    {
                        Header = (parameter.Label ?? parameter.Name) + " (" + parameter.Type + ")",
                        Text = supplied ? binding.Parameters[parameter.Name] : string.Empty,
                        PlaceholderText = parameter.Default ?? string.Join(", ", parameter.Options),
                        IsEnabled = include.IsChecked == true,
                    };
                    AutomationProperties.SetAutomationId(include, "LightSwitchUseParameter_" + parameter.Name);
                    AutomationProperties.SetAutomationId(value, "LightSwitchParameter_" + parameter.Name);
                    AutomationProperties.SetName(include, parameter.Label ?? parameter.Name);
                    include.Checked += (_, _) => value.IsEnabled = true;
                    include.Unchecked += (_, _) => value.IsEnabled = false;
                    parameters.Children.Add(value);
                    parameters.Children.Add(include);
                    fields.Add(parameter.Name, (include, value));
                }
            };
            selector.SelectedItem = scripts.SingleOrDefault(script => script.Id == binding.Id);
            if (!string.IsNullOrEmpty(binding.Id) && selector.SelectedItem is null)
            {
                status.Message = ScriptResource("LightSwitch_ThemeScriptMissing") + Environment.NewLine + binding.Id;
                status.IsOpen = true;
            }

            dialog.PrimaryButtonClick += async (dialogSender, args) =>
            {
                var deferral = args.GetDeferral();
                args.Cancel = true;
                try
                {
                    if (selector.SelectedItem is not PowerScriptInfo selected)
                    {
                        return;
                    }

                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var current = (await client.ListAsync(timeout.Token)).SingleOrDefault(script => script.Id == selected.Id);
                    if (current is null || !PowerScriptParameterValues.HasSameInputContract(selected, current) ||
                        current.Output != PowerScriptIO.None)
                    {
                        throw new InvalidOperationException(ScriptResource("LightSwitch_ThemeScriptChanged"));
                    }

                    var values = fields.Where(field => field.Value.Include.IsChecked == true)
                        .ToDictionary(field => field.Key, field => (string?)field.Value.Value.Text, StringComparer.Ordinal);
                    if (!PowerScriptParameterValues.TryCreate(current, values, out _, out var error))
                    {
                        throw new InvalidOperationException(error);
                    }

                    ViewModel.SetThemeScript(light, new LightSwitchScriptBinding
                    {
                        Id = current.Id,
                        Parameters = values.ToDictionary(value => value.Key, value => value.Value!, StringComparer.Ordinal),
                    });
                    args.Cancel = false;
                }
                catch (Exception exception)
                {
                    status.Message = exception.Message;
                    status.IsOpen = true;
                }
                finally
                {
                    deferral.Complete();
                }
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Secondary)
            {
                ViewModel.SetThemeScript(light, new LightSwitchScriptBinding());
            }
        }
        catch (Exception exception)
        {
            ThemeScriptError.Message = ScriptResource("LightSwitch_ThemeScriptsUnavailable") + Environment.NewLine + exception.Message;
            ThemeScriptError.IsOpen = true;
        }
        finally
        {
            _editingThemeScript = false;
        }
    }
}
