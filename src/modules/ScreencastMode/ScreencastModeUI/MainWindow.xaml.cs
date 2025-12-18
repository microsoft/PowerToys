// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using ScreencastModeUI.Keyboard;
using WinUIEx;

namespace ScreencastModeUI
{
    /// <summary>
    /// Main window that displays keystrokes for screencast mode
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

        // Extra buffer to prevent text clipping
        private const double ExtraWidthBuffer = 20;
        private const double ExtraHeightBuffer = 20;

        private readonly SettingsUtils _settingsUtils = SettingsUtils.Default;
        private readonly DispatcherTimer _hideTimer;
        private readonly DispatcherTimer _settingsDebounceTimer;
        private readonly KeyDisplayer _keyDisplayer = new();

        private KeyboardListener? _keyboardListener;
        private System.IO.FileSystemWatcher? _settingsWatcher;
        private bool _disposed;

        private string _textColor = "#FFFFFF";
        private string _backgroundColor = "#000000";
        private string _displayPosition = "TopRight";
        private int _textSize = 18;

        public MainWindow()
        {
            InitializeComponent();

            // Timer to hide the keystroke display after nothing is typed
            _hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(DefaultHideDelayMs),
            };
            _hideTimer.Tick += HideTimer_Tick;

            // Debounce timer for settings changes
            _settingsDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300),
            };
            _settingsDebounceTimer.Tick += SettingsDebounceTimer_Tick;

            // Subscribe to display updates from KeyDisplayer
            _keyDisplayer.DisplayUpdated += OnDisplayUpdated;

            LoadSettings();
            SetupKeyboardHook();
            ApplyColorSettings();

            KeystrokePanel.Visibility = Visibility.Collapsed;

            SubscribeToSettingsChanges();

            ConfigureOverlayWindow();

            UpdateWindowPosition();

            this.Hide();
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
                int extendedStyle = GetWindowLong(hwnd, -20);

                // Add extended window styles:
                // 0x00000020 = WS_EX_TRANSPARENT - Makes window transparent to mouse input (click-through)
                // 0x00000080 = WS_EX_TOOLWINDOW - Hides from Task Manager window list and taskbar
                // 0x08000000 = WS_EX_NOACTIVATE - Prevents window from being activated/focused
                // 0x00080000 = WS_EX_LAYERED - Still necesseary even though its enabled in WinUI
                extendedStyle |= 0x00000020 | 0x00000080 | 0x08000000 | 0x00080000;

                _ = SetWindowLong(hwnd, -20, extendedStyle);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to apply click-through style: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Stop timers
            _hideTimer.Stop();
            _settingsDebounceTimer.Stop();

            // Unsubscribe from KeyDisplayer events
            _keyDisplayer.DisplayUpdated -= OnDisplayUpdated;

            // Unsubscribe from keyboard listener events and dispose
            if (_keyboardListener != null)
            {
                _keyboardListener.KeyboardEvent -= OnKeyboardEvent;
                _keyboardListener.Dispose();
                _keyboardListener = null;
            }

            // Unsubscribe from settings watcher events and dispose
            if (_settingsWatcher != null)
            {
                _settingsWatcher.EnableRaisingEvents = false;
                _settingsWatcher.Changed -= OnSettingsFileChanged;
                _settingsWatcher.Created -= OnSettingsFileChanged;
                _settingsWatcher.Dispose();
                _settingsWatcher = null;
            }
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
        /// Move the overlay to the updated position based on settings
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
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to update window position: {ex.Message}");
            }
        }

        /// <summary>
        /// Initializes the global keyboard hook to listen for keystrokes
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
                _keyDisplayer.ProcessKeyEvent(e.Key, e.IsKeyDown);
            });
        }

        /// <summary>
        /// Handles display updates from the KeyDisplayer
        /// </summary>
        private void OnDisplayUpdated(object? sender, EventArgs e)
        {
            UpdateDisplay();
        }

        /// <summary>
        /// Updates the displayed text and resizes the window accordingly
        /// </summary>
        private void UpdateDisplay()
        {
            var displayText = _keyDisplayer.DisplayText;

            if (string.IsNullOrEmpty(displayText))
            {
                KeystrokePanel.Visibility = Visibility.Collapsed;
                _hideTimer.Stop();
                this.Hide();
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

                // Show the window when there's content to display
                this.Show();

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
        /// Hides the overlay, clears tracked keys, and stops the hide timer
        /// </summary>
        private void HideTimer_Tick(object? sender, object e)
        {
            _hideTimer.Stop();

            // Clear all tracked keys and modifiers when the overlay times out
            _keyDisplayer.Clear();

            KeystrokeText.Text = string.Empty;
            KeystrokePanel.Visibility = Visibility.Collapsed;

            // Hide the window completely when no text is displayed
            this.Hide();
        }
    }
}
