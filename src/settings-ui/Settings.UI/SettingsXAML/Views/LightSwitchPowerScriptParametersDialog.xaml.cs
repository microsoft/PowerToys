// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using CommunityToolkit.WinUI.Controls;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using PowerScripts.Client;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class LightSwitchPowerScriptParametersDialog : ContentDialog
    {
        private readonly PowerScriptInfo script;
        private readonly Dictionary<string, Func<string?>> valueReaders = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

        public LightSwitchPowerScriptParametersDialog(
            PowerScriptInfo script,
            IReadOnlyDictionary<string, string> savedValues)
        {
            ArgumentNullException.ThrowIfNull(script);
            ArgumentNullException.ThrowIfNull(savedValues);

            this.script = script;
            InitializeComponent();

            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            Title = resourceLoader.GetString("LightSwitch_PowerScriptParametersDialog_Title")
                .Replace("{0}", script.Name, StringComparison.Ordinal);
            DescriptionText.Text = resourceLoader.GetString("LightSwitch_PowerScriptParametersDialog_Description");
            PrimaryButtonText = resourceLoader.GetString("LightSwitch_PowerScriptParametersDialog_Save");
            CloseButtonText = resourceLoader.GetString("LightSwitch_PowerScriptParametersDialog_Cancel");

            foreach (var parameter in script.Parameters)
            {
                ParameterPanel.Children.Add(CreateParameterCard(parameter, savedValues));
            }
        }

        public IReadOnlyDictionary<string, string> Values => values;

        private SettingsCard CreateParameterCard(
            PowerScriptParameter parameter,
            IReadOnlyDictionary<string, string> savedValues)
        {
            var label = string.IsNullOrWhiteSpace(parameter.Label) ? parameter.Name : parameter.Label;
            var card = new SettingsCard
            {
                Header = label + (parameter.IsRequired ? " *" : string.Empty),
                Description = parameter.Description ?? string.Empty,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };

            var initialValue = savedValues.TryGetValue(parameter.Name, out var savedValue)
                ? savedValue
                : parameter.Default;
            card.Content = CreateParameterControl(parameter, initialValue);
            return card;
        }

        private FrameworkElement CreateParameterControl(PowerScriptParameter parameter, string? initialValue)
        {
            if (parameter.Type.Equals("int", StringComparison.OrdinalIgnoreCase))
            {
                var numberBox = new NumberBox
                {
                    MinWidth = 240,
                    Minimum = parameter.Min ?? double.NegativeInfinity,
                    Maximum = parameter.Max ?? double.PositiveInfinity,
                    SmallChange = 1,
                    SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                };
                if (int.TryParse(initialValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                {
                    numberBox.Value = number;
                }

                SetAutomation(numberBox, parameter);
                valueReaders[parameter.Name] = () =>
                    double.IsNaN(numberBox.Value)
                        ? null
                        : numberBox.Value.ToString(CultureInfo.InvariantCulture);
                return numberBox;
            }

            if (parameter.Type.Equals("bool", StringComparison.OrdinalIgnoreCase))
            {
                var toggle = new ToggleSwitch
                {
                    IsOn = string.Equals(initialValue, "true", StringComparison.OrdinalIgnoreCase),
                };
                SetAutomation(toggle, parameter);
                valueReaders[parameter.Name] = () => toggle.IsOn ? "true" : "false";
                return toggle;
            }

            if (parameter.Type.Equals("choice", StringComparison.OrdinalIgnoreCase))
            {
                var comboBox = new ComboBox
                {
                    MinWidth = 240,
                    ItemsSource = parameter.Options,
                    SelectedItem = initialValue,
                };
                SetAutomation(comboBox, parameter);
                valueReaders[parameter.Name] = () => comboBox.SelectedItem as string;
                return comboBox;
            }

            if (parameter.Type.Equals("file", StringComparison.OrdinalIgnoreCase))
            {
                var pathBox = new TextBox
                {
                    MinWidth = 240,
                    IsReadOnly = true,
                    Text = initialValue ?? string.Empty,
                };
                SetAutomation(pathBox, parameter);

                var browseButton = new Button
                {
                    Content = ResourceLoaderInstance.ResourceLoader.GetString("LightSwitch_PowerScriptParametersDialog_Browse"),
                };
                AutomationProperties.SetAutomationId(browseButton, $"{GetAutomationId(parameter.Name)}_Browse");
                browseButton.Click += (_, _) =>
                {
                    var selectedPath = PickFileDialog();
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        pathBox.Text = selectedPath;
                    }
                };

                var grid = new Grid { ColumnSpacing = 8 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Grid.SetColumn(browseButton, 1);
                grid.Children.Add(pathBox);
                grid.Children.Add(browseButton);

                valueReaders[parameter.Name] = () => pathBox.Text;
                return grid;
            }

            var textBox = new TextBox
            {
                MinWidth = 240,
                Text = initialValue ?? string.Empty,
            };
            SetAutomation(textBox, parameter);
            valueReaders[parameter.Name] = () => textBox.Text;
            return textBox;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            values.Clear();
            foreach (var parameter in script.Parameters)
            {
                var value = valueReaders[parameter.Name]();
                var error = Validate(parameter, value);
                if (error is not null)
                {
                    ValidationInfoBar.Message = error;
                    ValidationInfoBar.IsOpen = true;
                    args.Cancel = true;
                    return;
                }

                if (!string.IsNullOrEmpty(value))
                {
                    values[parameter.Name] = value;
                }
            }

            ValidationInfoBar.IsOpen = false;
        }

        private static string? Validate(PowerScriptParameter parameter, string? value)
        {
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            var label = string.IsNullOrWhiteSpace(parameter.Label) ? parameter.Name : parameter.Label;
            if (parameter.IsRequired && string.IsNullOrWhiteSpace(value))
            {
                return resourceLoader.GetString("LightSwitch_PowerScriptParameter_Required")
                    .Replace("{0}", label, StringComparison.Ordinal);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (parameter.Type.Equals("int", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                {
                    return resourceLoader.GetString("LightSwitch_PowerScriptParameter_Integer")
                        .Replace("{0}", label, StringComparison.Ordinal);
                }

                if (parameter.Min is { } min && number < min)
                {
                    return resourceLoader.GetString("LightSwitch_PowerScriptParameter_Minimum")
                        .Replace("{0}", label, StringComparison.Ordinal)
                        .Replace("{1}", min.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
                }

                if (parameter.Max is { } max && number > max)
                {
                    return resourceLoader.GetString("LightSwitch_PowerScriptParameter_Maximum")
                        .Replace("{0}", label, StringComparison.Ordinal)
                        .Replace("{1}", max.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
                }
            }

            if (parameter.Type.Equals("choice", StringComparison.OrdinalIgnoreCase) &&
                !parameter.Options.Contains(value, StringComparer.Ordinal))
            {
                return resourceLoader.GetString("LightSwitch_PowerScriptParameter_Choice")
                    .Replace("{0}", label, StringComparison.Ordinal);
            }

            if (parameter.Type.Equals("file", StringComparison.OrdinalIgnoreCase) && !File.Exists(value))
            {
                return resourceLoader.GetString("LightSwitch_PowerScriptParameter_File")
                    .Replace("{0}", label, StringComparison.Ordinal);
            }

            return null;
        }

        private static void SetAutomation(FrameworkElement control, PowerScriptParameter parameter)
        {
            var label = string.IsNullOrWhiteSpace(parameter.Label) ? parameter.Name : parameter.Label;
            AutomationProperties.SetAutomationId(control, GetAutomationId(parameter.Name));
            AutomationProperties.SetName(control, label);
        }

        private static string GetAutomationId(string parameterName) =>
            $"LightSwitchPowerScriptParameter_{parameterName}";

        private static string? PickFileDialog()
        {
            var openFileName = new OpenFileName
            {
                StructSize = Marshal.SizeOf<OpenFileName>(),
                Filter = "All files\0*.*\0",
                File = new string(new char[4096]),
                MaxFile = 4096,
                FileTitle = new string(new char[256]),
                MaxFileTitle = 256,
                Title = ResourceLoaderInstance.ResourceLoader.GetString("LightSwitch_PowerScriptParametersDialog_SelectFile"),
                Flags = (int)OpenFileNameFlags.OFN_NOCHANGEDIR,
                Hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.GetSettingsWindow()),
            };

            return NativeMethods.GetOpenFileName(openFileName) ? openFileName.File : null;
        }
    }
}
