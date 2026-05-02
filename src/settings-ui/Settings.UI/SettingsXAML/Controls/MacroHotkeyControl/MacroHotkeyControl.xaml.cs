// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

#pragma warning disable CA1001 // _hook is disposed in Unloaded; UserControl does not implement IDisposable

using System;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualKey = Windows.System.VirtualKey;

namespace Microsoft.PowerToys.Settings.UI.Controls;

public sealed partial class MacroHotkeyControl : UserControl
{
    // ── Dependency Properties ─────────────────────────────────────────────────
    public static readonly DependencyProperty HotkeyProperty =
        DependencyProperty.Register(
            nameof(Hotkey),
            typeof(HotkeySettings),
            typeof(MacroHotkeyControl),
            new PropertyMetadata(null, OnHotkeyChanged));

    public static readonly DependencyProperty HotkeyDisplayTextProperty =
        DependencyProperty.Register(
            nameof(HotkeyDisplayText),
            typeof(string),
            typeof(MacroHotkeyControl),
            new PropertyMetadata("(none)"));

    public HotkeySettings? Hotkey
    {
        get => (HotkeySettings?)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    public string HotkeyDisplayText
    {
        get => (string)GetValue(HotkeyDisplayTextProperty);
        private set => SetValue(HotkeyDisplayTextProperty, value);
    }

    private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctl = (MacroHotkeyControl)d;
        ctl.HotkeyDisplayText = BuildDisplayText((HotkeySettings?)e.NewValue);
    }

    private static string BuildDisplayText(HotkeySettings? hs)
    {
        if (hs is null || hs.Code == 0)
        {
            return "(none)";
        }

        var keys = hs.GetKeysList();
        return keys is null || keys.Count == 0 ? "(none)" : string.Join(" + ", keys);
    }

    // ── Recording state ───────────────────────────────────────────────────────
    private HotkeySettings _internalSettings = new HotkeySettings();
    private HotkeySettingsControlHook? _hook;
    private bool _isRecording;

    public MacroHotkeyControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hook?.Dispose();
        _hook = new HotkeySettingsControlHook(
            OnKeyDown,
            OnKeyUp,
            () => _isRecording,
            (key, extraInfo) => _isRecording);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _hook?.Dispose();
        _hook = null;
    }

    // ── Button / Flyout handlers ──────────────────────────────────────────────
    private void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        _internalSettings = new HotkeySettings();
        KeysDisplay.Keys = null;
        _isRecording = true;
    }

    private void RecorderFlyout_Closed(object sender, object e)
    {
        _isRecording = false;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        Hotkey = _internalSettings;
        RecorderFlyout.Hide();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        Hotkey = null;
        RecorderFlyout.Hide();
    }

    // ── Key event handlers ────────────────────────────────────────────────────
    private void OnKeyDown(int key)
    {
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
        {
            ApplyKeyEvent((VirtualKey)key, pressed: true, rawCode: key);
            KeysDisplay.Keys = _internalSettings.GetKeysList();
        });
    }

    private void OnKeyUp(int key)
    {
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
        {
            ApplyKeyEvent((VirtualKey)key, pressed: false, rawCode: key);
            KeysDisplay.Keys = _internalSettings.GetKeysList();
        });
    }

    private void ApplyKeyEvent(VirtualKey vk, bool pressed, int rawCode)
    {
        switch (vk)
        {
            case VirtualKey.LeftWindows:
            case VirtualKey.RightWindows:
                _internalSettings.Win = pressed;
                break;
            case VirtualKey.Control:
            case VirtualKey.LeftControl:
            case VirtualKey.RightControl:
                _internalSettings.Ctrl = pressed;
                break;
            case VirtualKey.Menu:
            case VirtualKey.LeftMenu:
            case VirtualKey.RightMenu:
                _internalSettings.Alt = pressed;
                break;
            case VirtualKey.Shift:
            case VirtualKey.LeftShift:
            case VirtualKey.RightShift:
                _internalSettings.Shift = pressed;
                break;
            case VirtualKey.Escape:
                if (pressed)
                {
                    // Escape closes the flyout without saving.
                    DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, RecorderFlyout.Hide);
                }

                break;
            default:
                if (pressed)
                {
                    _internalSettings.Code = rawCode;
                }

                break;
        }
    }
}
