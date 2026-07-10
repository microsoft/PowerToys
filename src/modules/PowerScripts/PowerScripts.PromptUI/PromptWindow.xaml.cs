// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace PowerScripts.PromptUI;

/// <summary>
/// The parameter prompt window. Controls are built dynamically from the <see cref="PromptSpec"/>:
/// choice → <see cref="ComboBox"/>, bool → <see cref="ToggleSwitch"/>, int → <see cref="NumberBox"/>,
/// string → <see cref="TextBox"/>. Confirming writes the collected values to the out file and exits 0;
/// cancelling or closing exits 2 so the Host treats it as "cancelled at the parameter prompt".
/// </summary>
public sealed partial class PromptWindow : Window
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private readonly string _outPath;
    private readonly List<(PromptParam Param, FrameworkElement Control)> _fields = new();
    private bool _completed;

    public PromptWindow(PromptSpec spec, string outPath)
    {
        this.InitializeComponent();

        _outPath = outPath;

        Title = string.IsNullOrWhiteSpace(spec.Title) ? "PowerScript" : spec.Title;
        TitleText.Text = Title;

        if (!string.IsNullOrWhiteSpace(spec.Description))
        {
            DescriptionText.Text = spec.Description;
            DescriptionText.Visibility = Visibility.Visible;
        }

        foreach (var p in spec.Parameters)
        {
            var field = new StackPanel { Spacing = 4 };
            field.Children.Add(new TextBlock
            {
                Text = p.DisplayLabel,
                Style = (Microsoft.UI.Xaml.Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            });

            if (!string.IsNullOrWhiteSpace(p.Description))
            {
                field.Children.Add(new TextBlock
                {
                    Text = p.Description,
                    Style = (Microsoft.UI.Xaml.Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    TextWrapping = TextWrapping.Wrap,
                });
            }

            var control = BuildControl(p);
            field.Children.Add(control);
            FieldsPanel.Children.Add(field);
            _fields.Add((p, control));
        }

        Closed += OnClosed;
        Activated += OnFirstActivated;

        ResizeAndCenter(spec.Parameters.Count);
    }

    private static FrameworkElement BuildControl(PromptParam p)
    {
        switch (p.Type?.ToLowerInvariant())
        {
            case "choice":
            {
                var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
                if (p.Options is not null)
                {
                    foreach (var option in p.Options)
                    {
                        combo.Items.Add(option);
                    }
                }

                if (p.Value is not null && combo.Items.Contains(p.Value))
                {
                    combo.SelectedItem = p.Value;
                }
                else if (combo.Items.Count > 0)
                {
                    combo.SelectedIndex = 0;
                }

                return combo;
            }

            case "bool":
                return new ToggleSwitch
                {
                    IsOn = string.Equals(p.Value, "true", StringComparison.OrdinalIgnoreCase),
                };

            case "int":
            {
                var number = new NumberBox
                {
                    SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                if (p.Min is int min)
                {
                    number.Minimum = min;
                }

                if (p.Max is int max)
                {
                    number.Maximum = max;
                }

                if (int.TryParse(p.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    number.Value = parsed;
                }

                return number;
            }

            default:
                return new TextBox
                {
                    Text = p.Value ?? string.Empty,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
        }
    }

    private static string ReadValue(FrameworkElement control) => control switch
    {
        ComboBox combo => combo.SelectedItem?.ToString() ?? string.Empty,
        ToggleSwitch toggle => toggle.IsOn ? "true" : "false",
        NumberBox number => double.IsNaN(number.Value)
            ? "0"
            : ((int)Math.Round(number.Value)).ToString(CultureInfo.InvariantCulture),
        TextBox text => text.Text,
        _ => string.Empty,
    };

    private void OnRun(object sender, RoutedEventArgs e)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (param, control) in _fields)
        {
            values[param.Name] = ReadValue(control);
        }

        try
        {
            File.WriteAllText(_outPath, JsonSerializer.Serialize(values, App.JsonOptions));
        }
        catch (Exception)
        {
            Environment.Exit(2);
            return;
        }

        _completed = true;
        Environment.Exit(0);
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Environment.Exit(2);

    private void OnClosed(object sender, WindowEventArgs args)
    {
        if (!_completed)
        {
            Environment.Exit(2);
        }
    }

    private void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        SetForegroundWindow(hwnd);
    }

    private void ResizeAndCenter(int parameterCount)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        if (appWindow is null)
        {
            return;
        }

        int width = 460;
        int height = Math.Min(720, 200 + (Math.Max(parameterCount, 1) * 92));
        appWindow.Resize(new SizeInt32(width, height));

        // Keep the prompt above other windows. It is launched in response to an explicit user action
        // (hotkey / context menu / paste) and blocks the run, so it must not get lost behind the
        // foreground app the user triggered it from.
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
        }

        var area = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        if (area is not null)
        {
            int x = area.WorkArea.X + ((area.WorkArea.Width - width) / 2);
            int y = area.WorkArea.Y + ((area.WorkArea.Height - height) / 2);
            appWindow.Move(new PointInt32(x, y));
        }
    }
}
