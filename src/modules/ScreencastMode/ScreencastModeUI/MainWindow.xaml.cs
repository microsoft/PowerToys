// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using ScreencastModeUI.Keyboard;
using Windows.Graphics;
using Windows.System;
using WinUIEx;

namespace ScreencastModeUI
{
    /// <summary>
    /// Main window that displays keystrokes for screencast mode.
    /// This is a simple overlay that shows keystrokes during presentations and screen recordings.
    /// </summary>
    public sealed partial class MainWindow : WindowEx, IDisposable
    {
        private const int MaxKeysToDisplay = 22;
        private const int DefaultHideDelayMs = 2000;
        private const int WindowWidth = 400;
        private const int WindowHeight = 60;
        private const int EdgeMargin = 20;

        private readonly SettingsUtils _settingsUtils = new();
        private readonly DispatcherTimer _hideTimer;
        private readonly List<KeyInfo> _pressedKeys = new();
        private readonly HashSet<VirtualKey> _activeModifiers = new();
        private readonly DispatcherTimer _settingsDebounceTimer;
        private KeyboardListener? _keyboardListener;
        private System.IO.FileSystemWatcher? _settingsWatcher;
        private DateTime _lastSettingsChange = DateTime.MinValue;

        private string _textColor = "#FFFFFF";
        private string _backgroundColor = "#000000";
        private string _displayPosition = "TopRight";

        public MainWindow()
        {
            InitializeComponent();

            _hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(DefaultHideDelayMs),
            };
            _hideTimer.Tick += HideTimer_Tick;

            // Debounce timer for settings changes (FileSystemWatcher can fire multiple times)
            _settingsDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300),
            };
            _settingsDebounceTimer.Tick += SettingsDebounceTimer_Tick;

            LoadSettings();
            SetupKeyboardHook();
            ApplyColorSettings();

            // Start hidden
            KeystrokePanel.Visibility = Visibility.Collapsed;

            // Subscribe to settings change notifications to reflect live changes
            SubscribeToSettingsChanges();

            // Configure window as a small overlay using WinUIEx
            ConfigureOverlayWindow();

            // Set initial position based on display position setting
            UpdateWindowPosition();
        }

        private void ConfigureOverlayWindow()
        {
            try
            {
                // Use WinUIEx properties to configure the overlay window
                // These avoid direct P/Invoke to user32.dll

                // Remove title bar and window chrome
                this.ExtendsContentIntoTitleBar = true;
                this.IsTitleBarVisible = false;

                // Disable resizing and min/max buttons
                this.IsResizable = false;
                this.IsMaximizable = false;
                this.IsMinimizable = false;

                // Keep window always on top
                this.IsAlwaysOnTop = true;

                // Hide from Alt+Tab and taskbar
                this.IsShownInSwitchers = false;

                // Set initial window size
                this.SetWindowSize(WindowWidth, WindowHeight);

                Logger.LogInfo("Configured window as overlay using WinUIEx");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to configure overlay window: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _keyboardListener?.Dispose();
            _settingsWatcher?.Dispose();
            _settingsDebounceTimer?.Stop();
        }

        private void SubscribeToSettingsChanges()
        {
            try
            {
                // Watch the ScreencastMode settings file for changes
                var settingsPath = _settingsUtils.GetSettingsFilePath(ScreencastModeSettings.ModuleName);
                var dir = System.IO.Path.GetDirectoryName(settingsPath);
                var file = System.IO.Path.GetFileName(settingsPath);

                Logger.LogInfo($"Watching settings file: {settingsPath}");

                if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(file))
                {
                    // Ensure directory exists
                    if (!System.IO.Directory.Exists(dir))
                    {
                        System.IO.Directory.CreateDirectory(dir);
                    }

                    // Store watcher as field to prevent garbage collection
                    _settingsWatcher = new System.IO.FileSystemWatcher(dir, file)
                    {
                        NotifyFilter = System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.Size | System.IO.NotifyFilters.CreationTime,
                        EnableRaisingEvents = true,
                    };

                    _settingsWatcher.Changed += OnSettingsFileChanged;
                    _settingsWatcher.Created += OnSettingsFileChanged;

                    Logger.LogInfo("FileSystemWatcher configured for settings changes");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to subscribe to settings changes: {ex.Message}");
            }
        }

        private void OnSettingsFileChanged(object sender, System.IO.FileSystemEventArgs e)
        {
            // Use debounce to avoid multiple rapid reloads
            _lastSettingsChange = DateTime.Now;
            DispatcherQueue.TryEnqueue(() =>
            {
                _settingsDebounceTimer.Stop();
                _settingsDebounceTimer.Start();
            });
        }

        private void SettingsDebounceTimer_Tick(object? sender, object e)
        {
            _settingsDebounceTimer.Stop();

            try
            {
                Logger.LogInfo("Reloading settings after file change...");
                LoadSettings();
                ApplyColorSettings();
                UpdateWindowPosition();
                UpdateDisplay();
                Logger.LogInfo($"Settings reloaded - TextColor: {_textColor}, BackgroundColor: {_backgroundColor}, Position: {_displayPosition}");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed applying live settings: {ex.Message}");
            }
        }

        private void LoadSettings()
        {
            try
            {
                var settings = _settingsUtils.GetSettingsOrDefault<ScreencastModeSettings>(
                    ScreencastModeSettings.ModuleName);

                _textColor = settings.Properties.TextColor.Value;
                _backgroundColor = settings.Properties.BackgroundColor.Value;
                _displayPosition = settings.Properties.DisplayPosition.Value;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load settings: {ex.Message}");
            }
        }

        private void ApplyColorSettings()
        {
            try
            {
                // Parse background color
                byte bgAlpha = 230; // Default semi-transparent
                int bgROffset = 1;
                int bgGOffset = 3;
                int bgBOffset = 5;

                if (_backgroundColor.Length == 9)
                {
                    // Format: #AARRGGBB
                    bgAlpha = byte.Parse(_backgroundColor.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    bgROffset = 3;
                    bgGOffset = 5;
                    bgBOffset = 7;
                }

                KeystrokePanel.Background = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(
                        bgAlpha,
                        byte.Parse(_backgroundColor.AsSpan(bgROffset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                        byte.Parse(_backgroundColor.AsSpan(bgGOffset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                        byte.Parse(_backgroundColor.AsSpan(bgBOffset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)));

                // Parse text color
                byte txtAlpha = 255; // Default fully opaque
                int txtROffset = 1;
                int txtGOffset = 3;
                int txtBOffset = 5;

                // Even though the color picker in the settings UI only allows #RRGGBB,
                // the settings.json file saves it as #AARRGGBB, where #AA is always FF.
                if (_textColor.Length == 9)
                {
                    // Format: #AARRGGBB
                    txtAlpha = byte.Parse(_textColor.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    txtROffset = 3;
                    txtGOffset = 5;
                    txtBOffset = 7;
                }

                KeystrokeText.Foreground = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(
                        txtAlpha,
                        byte.Parse(_textColor.AsSpan(txtROffset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                        byte.Parse(_textColor.AsSpan(txtGOffset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                        byte.Parse(_textColor.AsSpan(txtBOffset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)));

                Logger.LogInfo("Applied color settings to keystroke panel");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to apply color settings: {ex.Message}");
            }
        }

        private void UpdateWindowPosition()
        {
            try
            {
                // Use WinUIEx and WinUI 3 APIs for positioning
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                var workArea = displayArea.WorkArea;

                // Get DPI scale using WinUIEx
                double dpiScale = (float)this.GetDpiForWindow() / 96;
                int scaledWidth = (int)(WindowWidth * dpiScale);
                int scaledHeight = (int)(WindowHeight * dpiScale);
                int scaledMargin = (int)(EdgeMargin * dpiScale);

                int x, y;

                switch (_displayPosition)
                {
                    // Top left
                    case "TopLeft":
                    case "Top Left":
                        x = workArea.X + scaledMargin;
                        y = workArea.Y + scaledMargin;
                        break;

                    // Top right
                    case "TopRight":
                    case "Top Right":
                        x = workArea.X + workArea.Width - scaledWidth - scaledMargin;
                        y = workArea.Y + scaledMargin;
                        break;

                    // Bottom left
                    case "BottomLeft":
                    case "Bottom Left":
                        x = workArea.X + scaledMargin;
                        y = workArea.Y + workArea.Height - scaledHeight - scaledMargin;
                        break;

                    // Bottom right
                    case "BottomRight":
                    case "Bottom Right":
                        x = workArea.X + workArea.Width - scaledWidth - scaledMargin;
                        y = workArea.Y + workArea.Height - scaledHeight - scaledMargin;
                        break;

                    // Center / default
                    case "Center":
                    default:
                        x = workArea.X + ((workArea.Width - scaledWidth) / 2);
                        y = workArea.Y + workArea.Height - scaledHeight - scaledMargin;
                        break;
                }

                // Use WinUIEx's MoveAndResize method - no P/Invoke needed
                this.MoveAndResize(x, y, WindowWidth, WindowHeight);

                // Center the keystroke panel within the window
                KeystrokePanel.HorizontalAlignment = HorizontalAlignment.Center;
                KeystrokePanel.VerticalAlignment = VerticalAlignment.Center;
                KeystrokePanel.Margin = new Thickness(0);

                Logger.LogInfo($"Updated window position to: {_displayPosition} at ({x}, {y})");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to update window position: {ex.Message}");
            }
        }

        private void SetupKeyboardHook()
        {
            try
            {
                // Use our custom KeyboardListener that observes but doesn't consume keystrokes
                _keyboardListener = new KeyboardListener();
                _keyboardListener.KeyboardEvent += OnKeyboardEvent;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to setup keyboard hook: {ex.Message}");
            }
        }

        private void OnKeyboardEvent(object? sender, KeyboardEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ProcessKeyEvent(e.Key, e.IsKeyDown);
            });
        }

        private void ProcessKeyEvent(VirtualKey key, bool isKeyDown)
        {
            if (isKeyDown)
            {
                HandleKeyDown(key);
            }
            else
            {
                HandleKeyUp(key);
            }
        }

        private void HandleKeyDown(VirtualKey key)
        {
            // Check if it's a modifier key
            if (IsModifierKey(key))
            {
                var normalizedKey = NormalizeModifierKey(key);
                _activeModifiers.Add(normalizedKey);
            }

            // Check if it's a clear key (arrows, backspace, escape)
            else if (IsClearKey(key))
            {
                ClearKeys();
            }
            else
            {
                // Build what the text *would* look like if we added this key
                var keyInfo = new KeyInfo
                {
                    Key = key,
                    Modifiers = new HashSet<VirtualKey>(_activeModifiers),
                };

                _pressedKeys.Add(keyInfo);
                var preview = BuildDisplayText();

                // If that overflows our rough limit, start a fresh sequence with just this key
                if (WillOverflow(preview))
                {
                    _pressedKeys.Clear();
                    _pressedKeys.Add(keyInfo);
                }
            }

            UpdateDisplay();
        }

        private void HandleKeyUp(VirtualKey key)
        {
            if (IsModifierKey(key))
            {
                var normalizedKey = NormalizeModifierKey(key);
                _activeModifiers.Remove(normalizedKey);
            }
        }

        private void UpdateDisplay()
        {
            var displayText = BuildDisplayText();

            if (string.IsNullOrEmpty(displayText))
            {
                KeystrokePanel.Visibility = Visibility.Collapsed;
                _hideTimer.Stop();
            }
            else
            {
                KeystrokeText.Text = displayText;
                KeystrokePanel.Visibility = Visibility.Visible;

                _hideTimer.Stop();
                _hideTimer.Start();
            }
        }

        private string BuildDisplayText()
        {
            if (_pressedKeys.Count == 0 && _activeModifiers.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();

            foreach (var keyInfo in _pressedKeys)
            {
                var keyText = new StringBuilder();

                // Add modifiers for this key
                foreach (var modifier in keyInfo.Modifiers.OrderBy(GetModifierOrder))
                {
                    keyText.Append(GetKeyDisplayName(modifier));
                    keyText.Append(" + ");
                }

                // Add the key itself
                keyText.Append(GetKeyDisplayName(keyInfo.Key));

                parts.Add(keyText.ToString());
            }

            // If only modifiers are pressed
            if (_pressedKeys.Count == 0 && _activeModifiers.Count > 0)
            {
                var modifierText = string.Join(
                    " + ",
                    _activeModifiers.OrderBy(GetModifierOrder).Select(GetKeyDisplayName));

                parts.Add(modifierText);
            }

            return string.Join("  ", parts);
        }

        private bool WillOverflow(string nextText)
        {
            // Rough width check using character count vs. a max visible chars estimate
            const int maxVisibleChars = 40; // tune this based on your font/width
            return nextText.Length > maxVisibleChars;
        }

        private void ClearKeys()
        {
            _pressedKeys.Clear();
            _activeModifiers.Clear();
            UpdateDisplay();
        }

        private void HideTimer_Tick(object? sender, object e)
        {
            _hideTimer.Stop();

            // Clear all tracked keys and modifiers when the overlay times out
            _pressedKeys.Clear();
            _activeModifiers.Clear();

            KeystrokeText.Text = string.Empty;
            KeystrokePanel.Visibility = Visibility.Collapsed;
        }

        private static bool IsModifierKey(VirtualKey key)
        {
            return key is VirtualKey.Shift or
                   VirtualKey.LeftShift or
                   VirtualKey.RightShift or
                   VirtualKey.Control or
                   VirtualKey.LeftControl or
                   VirtualKey.RightControl or
                   VirtualKey.Menu or // Alt
                   VirtualKey.LeftMenu or
                   VirtualKey.RightMenu or
                   VirtualKey.LeftWindows or
                   VirtualKey.RightWindows;
        }

        private static VirtualKey NormalizeModifierKey(VirtualKey key)
        {
            return key switch
            {
                VirtualKey.LeftShift or VirtualKey.RightShift => VirtualKey.Shift,
                VirtualKey.LeftControl or VirtualKey.RightControl => VirtualKey.Control,
                VirtualKey.LeftMenu or VirtualKey.RightMenu => VirtualKey.Menu,
                VirtualKey.LeftWindows or VirtualKey.RightWindows => VirtualKey.LeftWindows,
                _ => key,
            };
        }

        private static bool IsClearKey(VirtualKey key)
        {
            return key is VirtualKey.Up or
                   VirtualKey.Down or
                   VirtualKey.Left or
                   VirtualKey.Right or
                   VirtualKey.Back or
                   VirtualKey.Escape;
        }

        private static int GetModifierOrder(VirtualKey key)
        {
            return key switch
            {
                VirtualKey.LeftWindows => 0,
                VirtualKey.Control => 1,
                VirtualKey.Menu => 2,
                VirtualKey.Shift => 3,
                _ => 4,
            };
        }

        private static string GetKeyDisplayName(VirtualKey key)
        {
            // Handle common special keys first
            return key switch
            {
                VirtualKey.LeftWindows or VirtualKey.RightWindows => "Win",
                VirtualKey.Control => "Ctrl",
                VirtualKey.Menu => "Alt",
                VirtualKey.Shift => "Shift",
                VirtualKey.Space => "Space",
                VirtualKey.Enter => "Enter",
                VirtualKey.Tab => "Tab",
                VirtualKey.Back => "Backspace",
                VirtualKey.Escape => "Esc",
                VirtualKey.Delete => "Del",
                VirtualKey.Up => "↑",
                VirtualKey.Down => "↓",
                VirtualKey.Left => "←",
                VirtualKey.Right => "→",
                VirtualKey.PageUp => "PgUp",
                VirtualKey.PageDown => "PgDn",
                VirtualKey.Home => "Home",
                VirtualKey.End => "End",
                VirtualKey.Insert => "Ins",

                // Numpad
                VirtualKey.NumberPad0 => "Num 0",
                VirtualKey.NumberPad1 => "Num 1",
                VirtualKey.NumberPad2 => "Num 2",
                VirtualKey.NumberPad3 => "Num 3",
                VirtualKey.NumberPad4 => "Num 4",
                VirtualKey.NumberPad5 => "Num 5",
                VirtualKey.NumberPad6 => "Num 6",
                VirtualKey.NumberPad7 => "Num 7",
                VirtualKey.NumberPad8 => "Num 8",
                VirtualKey.NumberPad9 => "Num 9",

                // F-keys
                VirtualKey.F1 => "F1",
                VirtualKey.F2 => "F2",
                VirtualKey.F3 => "F3",
                VirtualKey.F4 => "F4",
                VirtualKey.F5 => "F5",
                VirtualKey.F6 => "F6",
                VirtualKey.F7 => "F7",
                VirtualKey.F8 => "F8",
                VirtualKey.F9 => "F9",
                VirtualKey.F10 => "F10",
                VirtualKey.F11 => "F11",
                VirtualKey.F12 => "F12",

                // Letters A-Z
                >= VirtualKey.A and <= VirtualKey.Z => ((char)('A' + ((int)key - (int)VirtualKey.A))).ToString(),

                // Numbers 0-9
                >= VirtualKey.Number0 and <= VirtualKey.Number9 => ((char)('0' + ((int)key - (int)VirtualKey.Number0))).ToString(),

                // Punctuation using raw VK codes (these exist in VirtualKey)
                (VirtualKey)0xBD => "-",        // VK_OEM_MINUS
                (VirtualKey)0xBB => "=",        // VK_OEM_PLUS
                (VirtualKey)0xDB => "[",        // VK_OEM_4
                (VirtualKey)0xDD => "]",        // VK_OEM_6
                (VirtualKey)0xDC => "\\",       // VK_OEM_5
                (VirtualKey)0xBA => ";",        // VK_OEM_1
                (VirtualKey)0xDE => "'",        // VK_OEM_7
                (VirtualKey)0xBC => ",",        // VK_OEM_COMMA
                (VirtualKey)0xBE => ".",        // VK_OEM_PERIOD
                (VirtualKey)0xBF => "/",        // VK_OEM_2
                (VirtualKey)0xC0 => "`",        // VK_OEM_3

                _ => key.ToString(), // Fallback
            };
        }

        /// <summary>
        ///     Represents information about a pressed key along with its active modifiers.
        /// </summary>
        private sealed class KeyInfo
        {
            public VirtualKey Key { get; set; }

            public HashSet<VirtualKey> Modifiers { get; set; } = new();
        }
    }
}
