// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using ScreencastModeUI.Keyboard;
using Windows.System;
using WinUIEx;

namespace ScreencastModeUI
{
    /// <summary>
    /// Main window that displays keystrokes for screencast mode
    /// This is a simple overlay that shows keystrokes during presentations and screen recordings
    /// </summary>
    public sealed partial class MainWindow : WindowEx, IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(nint hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

        // private const int MaxKeysToDisplay = 22;
        private const int DefaultHideDelayMs = 2000;
        private const int MinWindowWidth = 200;
        private const int MinWindowHeight = 40;
        private const int EdgeMargin = 20;

        // Extra buffer to prevent text clipping (accounts for rendering differences, corner radius, etc.)
        private const double ExtraWidthBuffer = 20;
        private const double ExtraHeightBuffer = 20;

        private readonly SettingsUtils _settingsUtils = new();
        private readonly DispatcherTimer _hideTimer;

        // Track displayed keys in order - each entry is a display string
        private readonly List<string> _displayedKeys = new();

        // Track currently held modifiers
        private readonly HashSet<VirtualKey> _activeModifiers = new();

        private readonly DispatcherTimer _settingsDebounceTimer;

        // Flag to track if we need to add "+" before the next key
        private bool _needsPlusSeparator;

        private KeyboardListener? _keyboardListener;
        private System.IO.FileSystemWatcher? _settingsWatcher;

        private string _textColor = "#FFFFFF";
        private string _backgroundColor = "#000000";
        private string _displayPosition = "TopRight";
        private int _textSize = 18;

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

            KeystrokePanel.Visibility = Visibility.Collapsed;

            SubscribeToSettingsChanges();

            ConfigureOverlayWindow();

            UpdateWindowPosition();
        }

        private void ConfigureOverlayWindow()
        {
            try
            {
                // Use WinUIEx properties to configure the overlay window
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
                this.SetWindowSize(MinWindowWidth, MinWindowHeight);

                ApplyClickThroughStyle();

                Logger.LogInfo("Configured window as overlay using WinUIEx");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to configure overlay window: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies extended window styles to make the window click-through (transparent to mouse input)
        /// and hidden from Task Manager's window list
        /// </summary>
        private void ApplyClickThroughStyle()
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

                // GWL_EXSTYLE = -20
                int extendedStyle = GetWindowLong(hwnd, -20);

                // Add extended window styles:
                // 0x00000020 = WS_EX_TRANSPARENT - Makes window transparent to mouse input (click-through)
                // 0x00000080 = WS_EX_TOOLWINDOW - Hides from Task Manager window list and taskbar
                // 0x08000000 = WS_EX_NOACTIVATE - Prevents window from being activated/focused
                // 0x00080000 = WS_EX_LAYERED - Still necesseary even though its enabled in WinUI
                extendedStyle |= 0x00000020 | 0x00000080 | 0x08000000 | 0x00080000;

                _ = SetWindowLong(hwnd, -20, extendedStyle);

                Logger.LogInfo("Applied click-through and tool window styles");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to apply click-through style: {ex.Message}");
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
            // Without this, the color would not change, possibly due to file locking
            DispatcherQueue.TryEnqueue(() =>
            {
                _settingsDebounceTimer.Stop();
                _settingsDebounceTimer.Start();
            });
        }

        /// <summary>
        /// Add a delay before reloading settings to debounce multiple file change events
        /// </summary>
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

        /// <summary>
        /// Loads settings from the ScreencastMode settings file
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                var settings = _settingsUtils.GetSettingsOrDefault<ScreencastModeSettings>(
                    ScreencastModeSettings.ModuleName);

                _textColor = settings.Properties.TextColor.Value;
                _backgroundColor = settings.Properties.BackgroundColor.Value;
                _displayPosition = settings.Properties.DisplayPosition.Value;
                _textSize = settings.Properties.TextSize.Value;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Parse the color settings and apply them to the keystroke panel
        /// </summary>
        private void ApplyColorSettings()
        {
            try
            {
                // Parse background color
                byte bgAlpha = 230;
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
                byte txtAlpha = 255;
                int txtROffset = 1;
                int txtGOffset = 3;
                int txtBOffset = 5;

                // Even though the color picker in the settings UI only allows #RRGGBB,
                // the settings.json file saves it as #AARRGGBB, where #AA is always FF
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

        /// <summary>
        /// Move the overlaw to the updated position based on settings
        /// </summary>
        private void UpdateWindowPosition()
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                var workArea = displayArea.WorkArea;

                double dpiScale = (float)this.GetDpiForWindow() / 96;

                // Get the current actual window size (not the minimum)
                var appWindow = AppWindow.GetFromWindowId(windowId);
                int currentWidth = appWindow.Size.Width;
                int currentHeight = appWindow.Size.Height;

                int scaledMargin = (int)(EdgeMargin * dpiScale);

                int x, y;

                switch (_displayPosition)
                {
                    case "Top Left":
                        x = workArea.X + scaledMargin;
                        y = workArea.Y + scaledMargin;
                        break;

                    case "Top Right":
                        x = workArea.X + workArea.Width - currentWidth - scaledMargin;
                        y = workArea.Y + scaledMargin;
                        break;

                    case "Top Center":
                        x = workArea.X + ((workArea.Width - currentWidth) / 2);
                        y = workArea.Y + scaledMargin;
                        break;

                    case "Center":
                        x = workArea.X + ((workArea.Width - currentWidth) / 2);
                        y = workArea.Y + ((workArea.Height - currentHeight) / 2);
                        break;

                    case "Bottom Left":
                        x = workArea.X + scaledMargin;
                        y = workArea.Y + workArea.Height - currentHeight - scaledMargin;
                        break;

                    case "Bottom Center":
                        x = workArea.X + ((workArea.Width - currentWidth) / 2);
                        y = workArea.Y + workArea.Height - currentHeight - scaledMargin;
                        break;

                    case "Bottom Right":
                    default:
                        x = workArea.X + workArea.Width - currentWidth - scaledMargin;
                        y = workArea.Y + workArea.Height - currentHeight - scaledMargin;
                        break;
                }

                this.Move(x, y);

                KeystrokePanel.HorizontalAlignment = HorizontalAlignment.Center;
                KeystrokePanel.VerticalAlignment = VerticalAlignment.Center;
                KeystrokePanel.Margin = new Thickness(0);

                Logger.LogInfo($"Positioned window at ({x}, {y}) with size {currentWidth}x{currentHeight}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to update window position: {ex.Message}");
            }
        }

        /// <summary>
        /// Intializes the global keyboard hook to listen for keystrokes
        /// </summary>
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

        /// <summary>
        /// Enqueues processing of a keyboard event on the UI thread
        /// </summary>
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

        /// <summary>
        /// Handle when a key is being pressed
        /// </summary>
        /// <param name="key">The key that is currently being held down</param>
        private void HandleKeyDown(VirtualKey key)
        {
            // Normalize modifier keys (e.g., LeftShift -> Shift)
            var normalizedKey = KeyDisplayNameProvider.IsModifierKey(key)
                ? KeyDisplayNameProvider.NormalizeModifierKey(key)
                : key;

            // Handle modifier keys
            if (KeyDisplayNameProvider.IsModifierKey(key))
            {
                // Only add modifier if not already held (Add returns false if already present)
                if (_activeModifiers.Add(normalizedKey))
                {
                    var keyName = KeyDisplayNameProvider.GetKeyDisplayName(normalizedKey);

                    // Check if adding would overflow
                    string previewText = BuildPreviewText(keyName);
                    if (WillOverflow(previewText))
                    {
                        // Clear and start fresh with just this modifier
                        _displayedKeys.Clear();
                        _needsPlusSeparator = false;
                    }

                    // Add "+" if we already have content and need separator
                    if (_needsPlusSeparator && _displayedKeys.Count > 0)
                    {
                        _displayedKeys.Add("+");
                    }

                    _displayedKeys.Add(keyName);

                    // Next key should have a "+" before it
                    _needsPlusSeparator = true;
                }
            }

            // Backspace and Escape keys clear the current display
            else if (KeyDisplayNameProvider.IsClearKey(key))
            {
                // Clear keys (Backspace, Esc) - clear and show just this key
                _displayedKeys.Clear();
                _activeModifiers.Clear();
                _needsPlusSeparator = false;

                _displayedKeys.Add(KeyDisplayNameProvider.GetKeyDisplayName(normalizedKey));
                _needsPlusSeparator = false; // Clear keys don't expect continuation
            }
            else
            {
                // Regular key
                var keyName = KeyDisplayNameProvider.GetKeyDisplayName(normalizedKey);

                // Check if adding would overflow
                string previewText = BuildPreviewText(keyName);
                if (WillOverflow(previewText))
                {
                    // Clear and start fresh - but keep active modifiers shown
                    _displayedKeys.Clear();
                    _needsPlusSeparator = false;

                    // Re-add currently held modifiers
                    foreach (var mod in _activeModifiers)
                    {
                        if (_displayedKeys.Count > 0)
                        {
                            _displayedKeys.Add("+");
                        }

                        _displayedKeys.Add(KeyDisplayNameProvider.GetKeyDisplayName(mod));
                    }

                    if (_displayedKeys.Count > 0)
                    {
                        _needsPlusSeparator = true;
                    }
                }

                // Add "+" if we have modifiers held or previous content
                if (_needsPlusSeparator && _displayedKeys.Count > 0)
                {
                    _displayedKeys.Add("+");
                }

                _displayedKeys.Add(keyName);

                // If modifiers are still held, next key should have "+"
                // If no modifiers, this is a standalone key, so start fresh next time
                _needsPlusSeparator = _activeModifiers.Count > 0;
            }

            UpdateDisplay();
        }

        /// <summary>
        /// Handle key release events
        /// </summary>
        /// <param name="key">The key that is released</param>
        private void HandleKeyUp(VirtualKey key)
        {
            if (KeyDisplayNameProvider.IsModifierKey(key))
            {
                var normalizedKey = KeyDisplayNameProvider.NormalizeModifierKey(key);
                _activeModifiers.Remove(normalizedKey);

                // When all modifiers are released, reset the separator flag
                // This allows the next keystroke to start a new sequence
                if (_activeModifiers.Count == 0)
                {
                    _needsPlusSeparator = false;
                }
            }
        }

        /// <summary>
        /// Updates the displayed text and resizes the window accordingly
        /// </summary>
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
                KeystrokeText.FontSize = _textSize;

                // Update padding proportionally to text size
                // Horizontal: ~0.9x font size per side, Vertical: ~0.45x font size per side
                double paddingH = _textSize * 0.9;
                double paddingV = _textSize * 0.45;
                KeystrokePanel.Padding = new Thickness(paddingH, paddingV, paddingH, paddingV);

                KeystrokePanel.Visibility = Visibility.Visible;

                // Force layout update before measuring
                KeystrokeText.UpdateLayout();

                // Measure and resize window based on text content
                ResizeWindowToFitContent();

                _hideTimer.Stop();
                _hideTimer.Start();
            }
        }

        /// <summary>
        /// Dynamically resizes the overlay window based on size and number of text characters
        /// </summary>
        private void ResizeWindowToFitContent()
        {
            try
            {
                // Get window and DPI info
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);
                double dpiScale = (float)this.GetDpiForWindow() / 96;

                // Measure the actual TextBlock after it has been updated
                KeystrokeText.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
                double textWidth = KeystrokeText.DesiredSize.Width;
                double textHeight = KeystrokeText.DesiredSize.Height;

                // Get the current padding from the Border
                var padding = KeystrokePanel.Padding;
                double totalPaddingH = padding.Left + padding.Right;
                double totalPaddingV = padding.Top + padding.Bottom;

                // Calculate logical size (text + padding + extra buffer for safety)
                double logicalWidth = textWidth + totalPaddingH + ExtraWidthBuffer;
                double logicalHeight = textHeight + totalPaddingV + ExtraHeightBuffer;

                // Convert to physical pixels for window sizing
                int windowWidth = (int)Math.Ceiling(logicalWidth * dpiScale);
                int windowHeight = (int)Math.Ceiling(logicalHeight * dpiScale);

                // Calculate minimum sizes based on font size
                // After testing, min height 2.5x the font is a good fit
                int minWidth = (int)(MinWindowWidth * dpiScale);
                int minHeight = (int)(_textSize * 2.5 * dpiScale);

                // Ensure minimum size
                windowWidth = Math.Max(windowWidth, minWidth);
                windowHeight = Math.Max(windowHeight, minHeight);

                Logger.LogInfo($"Resizing window to {windowWidth}x{windowHeight} (text: {textWidth}x{textHeight}, padding: {totalPaddingH}x{totalPaddingV}, DPI: {dpiScale})");

                // Resize using AppWindow API
                appWindow.Resize(new Windows.Graphics.SizeInt32(windowWidth, windowHeight));

                // Update position after resize
                UpdateWindowPosition();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to resize window: {ex.Message}");
            }
        }

        /// <summary>
        /// Builds the display text from the displayed keys list.
        /// Keys are shown in the exact order they were added.
        /// </summary>
        private string BuildDisplayText()
        {
            if (_displayedKeys.Count == 0)
            {
                return string.Empty;
            }

            // Join with spaces for visual separation, but "+" entries are already in the list
            var result = new StringBuilder();
            foreach (var part in _displayedKeys)
            {
                if (part == "+")
                {
                    // Add space before and after the plus for readability
                    result.Append(" + ");
                }
                else
                {
                    if (result.Length > 0 && !result.ToString().EndsWith(' '))
                    {
                        // Only add space if not coming right after a "+"
                        // Check if last thing added was " + "
                        if (!result.ToString().EndsWith("+ ", StringComparison.Ordinal))
                        {
                            result.Append(' ');
                        }
                    }

                    result.Append(part);
                }
            }

            return result.ToString().Trim();
        }

        /// <summary>
        /// Builds a preview of what the display text would look like if we add a new key.
        /// </summary>
        private string BuildPreviewText(string newKey)
        {
            var tempList = new List<string>(_displayedKeys);
            if (_needsPlusSeparator && tempList.Count > 0)
            {
                tempList.Add("+");
            }

            tempList.Add(newKey);

            var result = new StringBuilder();
            foreach (var part in tempList)
            {
                if (part == "+")
                {
                    result.Append(" + ");
                }
                else
                {
                    if (result.Length > 0 && !result.ToString().EndsWith(' '))
                    {
                        if (!result.ToString().EndsWith("+ ", StringComparison.Ordinal))
                        {
                            result.Append(' ');
                        }
                    }

                    result.Append(part);
                }
            }

            return result.ToString().Trim();
        }

        private bool WillOverflow(string nextText)
        {
            // Rough width check using character count vs. a max visible chars estimate
            const int maxVisibleChars = 40; // tune this based on your font/width
            return nextText.Length > maxVisibleChars;
        }

        private void HideTimer_Tick(object? sender, object e)
        {
            _hideTimer.Stop();

            // Clear all tracked keys and modifiers when the overlay times out
            _displayedKeys.Clear();
            _activeModifiers.Clear();
            _needsPlusSeparator = false;

            KeystrokeText.Text = string.Empty;
            KeystrokePanel.Visibility = Visibility.Collapsed;
        }
    }
}
