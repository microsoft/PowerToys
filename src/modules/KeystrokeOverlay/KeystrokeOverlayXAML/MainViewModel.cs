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
using KeystrokeOverlayUI.Helpers;
using KeystrokeOverlayUI.Models;
using KeystrokeOverlayUI.Services;
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
        private DisplayMode _displayMode = DisplayMode.Last5;

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

        [ObservableProperty]
        private string _displayModeText = string.Empty;

        [ObservableProperty]
        private bool _isDisplayModeVisible = false;

        [ObservableProperty]
        private bool _isVisibleHotkey = false;

        private int _maxKeystrokesShown = 5;

        public HotkeySettings ActivationShortcut { get; set; }
            = new HotkeySettings(true, false, false, true, 0x4B);

        public HotkeySettings SwitchMonitorHotkey { get; set; }
            = new HotkeySettings(true, true, false, false, 0x4B);

        public HotkeySettings SwitchDisplayModeHotkey { get; set; }
            = new HotkeySettings(true, false, false, true, 0x44);

        public event EventHandler<HotkeyAction> HotkeyActionTriggered;

        public async void ShowLabel(HotkeyAction action, string text, int durationMs = 2000)
        {
            switch (action)
            {
                case HotkeyAction.Monitor:
                    MonitorLabelText = text;
                    IsMonitorLabelVisible = true;
                    break;
                case HotkeyAction.Activation:
                    ActivationLabelText = text;
                    IsActivationLabelVisible = true;
                    break;
                case HotkeyAction.DisplayMode:
                    DisplayModeText = text;
                    IsDisplayModeVisible = true;
                    break;
            }

            IsVisibleHotkey = IsMonitorLabelVisible || IsActivationLabelVisible || IsDisplayModeVisible;

            try
            {
                await Task.Delay(durationMs);
            }
            catch
            {
                // write to logs
                Logger.LogError("KeystrokeOverlay: Error showing label delay.");
            }

            switch (action)
            {
                case HotkeyAction.Monitor:
                    IsMonitorLabelVisible = false;
                    break;
                case HotkeyAction.Activation:
                    IsActivationLabelVisible = false;
                    break;
                case HotkeyAction.DisplayMode:
                    IsDisplayModeVisible = false;
                    break;
            }

            IsVisibleHotkey = IsMonitorLabelVisible || IsActivationLabelVisible || IsDisplayModeVisible;
        }

        public void ApplySettings(ModuleProperties props)
        {
            TimeoutMs = props.OverlayTimeout.Value;
            TextSize = props.TextSize.Value;

            TextColor = CustomColorHelper.GetBrushFromHex(props.TextColor.Value);
            BackgroundColor = CustomColorHelper.GetBrushFromHex(props.BackgroundColor.Value);

            IsDraggable = props.IsDraggable.Value;
            DisplayMode = (DisplayMode)props.DisplayMode.Value;

            _maxKeystrokesShown = DisplayMode == DisplayMode.SingleCharactersOnly ? 1 : 5;

            ActivationShortcut = props.ActivationShortcut;
            SwitchMonitorHotkey = props.SwitchMonitorHotkey;
            SwitchDisplayModeHotkey = props.SwitchDisplayModeHotkey;
        }

        private readonly KeystrokeProcessor _keystrokeProcessor = new();

        public void HandleKeystrokeEvent(KeystrokeEvent keystroke)
        {
            bool isDown = string.Equals(keystroke.EventType, "down", StringComparison.OrdinalIgnoreCase);

            if (isDown && keystroke.IsPressed)
            {
                if (CheckGlobalHotkeys(keystroke))
                {
                    return;
                }
            }

            if (!IsActive)
            {
                return;
            }

            // update UI
            var result = _keystrokeProcessor.Process(keystroke, DisplayMode);
            switch (result.Action)
            {
                case KeystrokeAction.Add:
                    RegisterKey(result.Text, TimeoutMs);
                    break;

                case KeystrokeAction.ReplaceLast:
                    if (PressedKeys.Count > 0)
                    {
                        PressedKeys.RemoveAt(PressedKeys.Count - 1);
                    }

                    RegisterKey(result.Text, TimeoutMs);
                    break;

                case KeystrokeAction.RemoveLast:
                    if (PressedKeys.Count > 0)
                    {
                        PressedKeys.RemoveAt(PressedKeys.Count - 1);
                        UpdateOpacities();
                    }

                    break;

                case KeystrokeAction.None:
                default:
                    break;
            }
        }

        private bool CheckGlobalHotkeys(KeystrokeEvent keystroke)
        {
            if (IsHotkeyMatch(keystroke, ActivationShortcut))
            {
                IsActive = !IsActive;
                ShowLabel(HotkeyAction.Activation, IsActive ? "Overlay On" : "Overlay Off");
                if (!IsActive)
                {
                    ClearKeys();
                    _keystrokeProcessor.ResetBuffer();
                }

                HotkeyActionTriggered?.Invoke(this, HotkeyAction.Activation);
                return true;
            }
            else if (IsHotkeyMatch(keystroke, SwitchMonitorHotkey))
            {
                HotkeyActionTriggered?.Invoke(this, HotkeyAction.Monitor);
                return true;
            }
            else if (IsHotkeyMatch(keystroke, SwitchDisplayModeHotkey))
            {
                int current = (int)DisplayMode;
                DisplayMode = (DisplayMode)((current + 1) % 4);

                _maxKeystrokesShown = DisplayMode == DisplayMode.SingleCharactersOnly ? 1 : 5;

                string modeText = DisplayMode switch
                {
                    DisplayMode.Last5 => "Last Five Keystrokes",
                    DisplayMode.SingleCharactersOnly => "Single Characters Only",
                    DisplayMode.ShortcutsOnly => "Shortcuts Only",
                    DisplayMode.Stream => "Stream",
                    _ => "Unknown",
                };

                _keystrokeProcessor.ResetBuffer();
                ShowLabel(HotkeyAction.DisplayMode, modeText);
                HotkeyActionTriggered?.Invoke(this, HotkeyAction.DisplayMode);
                return true;
            }

            return false;
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

        public void RegisterKey(string key, int durationMs, int textSize = -1)
        {
            if (textSize == -1)
            {
                textSize = TextSize;
            }

            var newItem = new KeyVisualItem { Text = key, Opacity = 1.0, TextSize = textSize };
            PressedKeys.Add(newItem);

            UpdateOpacities();

            if (PressedKeys.Count > _maxKeystrokesShown)
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
