// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using KeystrokeOverlayUI.Controls;
using KeystrokeOverlayUI.Models;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace KeystrokeOverlayUI
{
    public partial class MainViewModel : ObservableObject
    {
        // Changed from string to KeyVisualItem to support individual properties
        public ObservableCollection<KeyVisualItem> PressedKeys { get; } = new();

        [ObservableProperty]
        private int _timeoutMs = 3000;

        [ObservableProperty]
        private int _textSize = 24;

        [ObservableProperty]
        private SolidColorBrush _textColor = new(Colors.White);

        [ObservableProperty]
        private SolidColorBrush _backgroundColor = new(Colors.Transparent);

        [ObservableProperty]
        private bool _isDraggable = true;

        [ObservableProperty]
        private int _displayMode = 0;

        [ObservableProperty]
        private bool _isActive = true;

        [ObservableProperty]
        private string _monitorLabelText = string.Empty;

        [ObservableProperty]
        private bool _isMonitorLabelVisible = false;

        [ObservableProperty]
        private string _activationLabelText = string.Empty;

        [ObservableProperty]
        private bool _isActivationLabelVisible = false;

        public HotkeySettings ActivationShortcut { get; set; }
            = new HotkeySettings(true, false, false, true, 0x4B);

        public HotkeySettings SwitchMonitorHotkey { get; set; }
            = new HotkeySettings(true, true, false, false, 0x4B);

        public event EventHandler RequestMonitorMove;

        public async void ShowLabel(string type, string text, int durationMs = 2000)
        {
            if (type == "monitor")
            {
                MonitorLabelText = text;
                IsMonitorLabelVisible = true;
            }
            else
            {
                ActivationLabelText = text;
                IsActivationLabelVisible = true;
            }

            try
            {
                await Task.Delay(durationMs);
            }
            catch
            {
                // write to logs
                Logger.LogError("KeystrokeOverlay: Error showing label delay.");
            }

            if (type == "monitor")
            {
                IsMonitorLabelVisible = false;
            }
            else
            {
                IsActivationLabelVisible = false;
            }
        }

        public void ApplySettings(ModuleProperties props)
        {
            TimeoutMs = props.OverlayTimeout.Value;
            TextSize = props.TextSize.Value;

            TextColor = GetBrushFromHex(props.TextColor.Value);
            BackgroundColor = GetBrushFromHex(props.BackgroundColor.Value);

            IsDraggable = props.IsDraggable.Value;
            DisplayMode = props.DisplayMode.Value;

            ActivationShortcut = props.ActivationShortcut;
            SwitchMonitorHotkey = props.SwitchMonitorHotkey;
        }

        private SolidColorBrush GetBrushFromHex(string hex)
        {
            try
            {
                if (string.IsNullOrEmpty(hex))
                {
                    return new SolidColorBrush(Colors.Transparent);
                }

                // Handles #RRGGBB or #AARRGGBB
                hex = hex.Replace("#", string.Empty);
                byte a = 255;
                byte r = 0, g = 0, b = 0;

                var provider = CultureInfo.InvariantCulture;

                if (hex.Length == 6)
                {
                    r = byte.Parse(hex.AsSpan(0, 2), NumberStyles.HexNumber, provider);
                    g = byte.Parse(hex.AsSpan(2, 2), NumberStyles.HexNumber, provider);
                    b = byte.Parse(hex.AsSpan(4, 2), NumberStyles.HexNumber, provider);
                }
                else if (hex.Length == 8)
                {
                    a = byte.Parse(hex.AsSpan(0, 2), NumberStyles.HexNumber, provider);
                    r = byte.Parse(hex.AsSpan(2, 2), NumberStyles.HexNumber, provider);
                    g = byte.Parse(hex.AsSpan(4, 2), NumberStyles.HexNumber, provider);
                    b = byte.Parse(hex.AsSpan(6, 2), NumberStyles.HexNumber, provider);
                }

                return new SolidColorBrush(Color.FromArgb(a, r, g, b));
            }
            catch
            {
                // Error fallback
                return new SolidColorBrush(Colors.Magenta);
            }
        }

        public async Task HandleKeystrokeEvent(KeystrokeEvent keystroke)
        {
            bool isDown = string.Equals(keystroke.EventType, "down", StringComparison.OrdinalIgnoreCase);
            if (isDown && keystroke.IsPressed && IsHotkeyMatch(keystroke, ActivationShortcut))
            {
                IsActive = !IsActive;

                ShowLabel("activation", IsActive ? "Overlay On" : "Overlay Off");

                if (!IsActive)
                {
                    await Task.Delay(2000);
                    ClearKeys();
                }

                return;
            }

            if (isDown && keystroke.IsPressed && IsHotkeyMatch(keystroke, SwitchMonitorHotkey))
            {
                // Fire the event for the View to handle
                RequestMonitorMove?.Invoke(this, EventArgs.Empty);
                return;
            }

            // If the overlay is "OFF", stop here.
            if (!IsActive)
            {
                return;
            }

            string formattedText = keystroke.ToString();

            if (string.IsNullOrEmpty(formattedText))
            {
                return;
            }

            // 2. Filter based on DisplayMode
            bool isShortcut = keystroke.IsShortcut;

            switch (DisplayMode)
            {
                case 0: // "Last 5" (Both / All)
                    break;

                case 1: // "Single Characters Only"
                    if (isShortcut)
                    {
                        return;
                    }

                    break;

                case 2: // "Shortcuts Only"
                    if (!isShortcut)
                    {
                        return;
                    }

                    break;
            }

            RegisterKey(formattedText);
        }

        private bool IsHotkeyMatch(KeystrokeEvent kEvent, HotkeySettings settings)
        {
            if (settings == null || !settings.IsValid())
            {
                return false;
            }

            // Compare the Main Key Code
            if (kEvent.VirtualKey != settings.Code)
            {
                return false;
            }

            // Compare Modifiers
            bool hasWin = kEvent.Modifiers?.Contains("Win") ?? false;
            bool hasCtrl = kEvent.Modifiers?.Contains("Ctrl") ?? false;
            bool hasAlt = kEvent.Modifiers?.Contains("Alt") ?? false;
            bool hasShift = kEvent.Modifiers?.Contains("Shift") ?? false;

            return hasWin == settings.Win &&
                   hasCtrl == settings.Ctrl &&
                   hasAlt == settings.Alt &&
                   hasShift == settings.Shift;
        }

        public void RegisterKey(string key, int durationMs = 2000, int textSize = -1)
        {
            if (textSize == -1)
            {
                textSize = TextSize;
            }

            var newItem = new KeyVisualItem { Text = key, Opacity = 1.0, TextSize = textSize };
            PressedKeys.Add(newItem);

            UpdateOpacities();

            if (PressedKeys.Count > 5)
            {
                PressedKeys.RemoveAt(0);
                UpdateOpacities();
            }

            // Pass the duration to the removal logic
            _ = RemoveKeyAfterDelayAsync(newItem, durationMs);
        }

        // 2. Add a helper to clear keys immediately (for switching phases)
        public void ClearKeys()
        {
            PressedKeys.Clear();
        }

        private void UpdateOpacities()
        {
            // Iterate through all keys
            for (int i = 0; i < PressedKeys.Count; i++)
            {
                var item = PressedKeys[i];

                // If the item is currently running its "death animation", skip it
                if (item.IsExiting)
                {
                    continue;
                }

                // Calculate index from the end (Newest = 0)
                int indexFromEnd = PressedKeys.Count - 1 - i;

                // Decrease 15% for every step back
                double targetOpacity = 1.0 - (0.15 * indexFromEnd);

                // Clamp to valid range (e.g. don't go below 0.1 visible)
                item.Opacity = Math.Max(0.1, targetOpacity);
            }
        }

        private async Task RemoveKeyAfterDelayAsync(KeyVisualItem item, int durationMs)
        {
            // Wait the defined lifetime
            await Task.Delay(durationMs);

            // Mark as exiting so UpdateOpacities doesn't fight us
            item.IsExiting = true;

            PressedKeys.Remove(item);

            // Re-adjust remaining keys
            UpdateOpacities();
        }
    }
}
