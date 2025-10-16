// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Management;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Common.UI;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Composition;
using PowerDisplay.Core;
using PowerDisplay.Core.Interfaces;
using PowerDisplay.Core.Models;
using PowerDisplay.Helpers;
using PowerDisplay.Native;
using PowerDisplay.ViewModels;
using Windows.Graphics;
using WinRT.Interop;
using Monitor = PowerDisplay.Core.Models.Monitor;

namespace PowerDisplay
{
    /// <summary>
    /// PowerDisplay main window
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private MainViewModel _viewModel = null!;
        private TrayIconHelper _trayIcon = null!;
        private AppWindow _appWindow = null!;
        private bool _isExiting = false;
        private readonly ISettingsUtils _settingsUtils = new SettingsUtils();

        public MainWindow()
        {
            try
            {
                this.InitializeComponent();

                // Initialize ViewModel and bind to root Grid
                _viewModel = new MainViewModel();
                RootGrid.DataContext = _viewModel;

                // Initialize ViewModel event handlers
                _viewModel.UIRefreshRequested += OnUIRefreshRequested;
                _viewModel.Monitors.CollectionChanged += OnMonitorsCollectionChanged;
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                _viewModel.ThemeChangeRequested += OnThemeChangeRequested;

                // Bind button events
                LinkButton.Click += OnLinkClick;
                DisableButton.Click += OnDisableClick;
                ThemeButton.Click += OnThemeButtonClick;
                RefreshButton.Click += OnRefreshClick;

                // Setup window properties
                SetupWindow();

                // Initialize theme and icons
                InitializeTheme();

                // Initialize tray icon
                InitializeTrayIcon();

                // Clean up resources on window close
                this.Closed += OnWindowClosed;

                // Initialize UI text
                InitializeUIText();

                // Initialize on startup
                _ = InitializeAsync();

                // Hide window on startup, show only tray icon
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100); // Wait for window to fully initialize
                    DispatcherQueue.TryEnqueue(() => HideWindow());
                });

            }
            catch (Exception e)
            {
                ShowError($"Unable to start main window: {e.Message}");
            }
        }
        private async Task InitializeAsync()
        {
            try
            {
                await Task.Delay(500);

                await _viewModel.RefreshMonitorsAsync();
                _viewModel.ReloadMonitorSettings();

                // Delay to allow UI to render, then adjust window size
                await Task.Delay(100);
                DispatcherQueue.TryEnqueue(() => AdjustWindowSizeToContent());
            }
            catch (System.Management.ManagementException)
            {
                ShowError("Unable to access internal display control, administrator privileges may be required.");
            }
            catch (Exception ex)
            {
                ShowError($"Initialization failed: {ex.Message}");
            }
        }







        private void InitializeUIText()
        {
            try
            {
                var loader = ResourceLoaderInstance.ResourceLoader;

                // Set text block content
                ScanningMonitorsTextBlock.Text = loader.GetString("ScanningMonitorsText");
                NoMonitorsTextBlock.Text = loader.GetString("NoMonitorsText");
                AdjustBrightnessTextBlock.Text = loader.GetString("AdjustBrightnessText");

                // Set button tooltips
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(LinkButton, loader.GetString("SyncAllMonitorsTooltip"));
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(DisableButton, loader.GetString("ToggleControlTooltip"));
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(ThemeButton, loader.GetString("ToggleThemeTooltip"));
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(RefreshButton, loader.GetString("RefreshTooltip"));
            }
            catch
            {
                // Use English defaults if resource loading fails
                ScanningMonitorsTextBlock.Text = "Scanning monitors...";
                NoMonitorsTextBlock.Text = "No monitors detected";
                AdjustBrightnessTextBlock.Text = "Adjust Brightness";

                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(LinkButton, "Synchronize all monitors to the same brightness");
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(DisableButton, "Enable or disable brightness control");
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(ThemeButton, "Switch between light and dark themes");
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(RefreshButton, "Rescan connected monitors");
            }
        }

        private void ShowError(string message)
        {
            _viewModel.StatusText = $"Error: {message}";
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            // Allow window to close if program is exiting
            if (_isExiting)
            {
                // Clean up event subscriptions
                if (_viewModel != null)
                {
                    _viewModel.UIRefreshRequested -= OnUIRefreshRequested;
                    _viewModel.Monitors.CollectionChanged -= OnMonitorsCollectionChanged;
                    _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                    _viewModel.ThemeChangeRequested -= OnThemeChangeRequested;
                }
                args.Handled = false;
                return;
            }

            // If only user operation (although we hide close button), just hide window
            args.Handled = true; // Prevent window closing
            HideWindow();
        }

        private void InitializeTrayIcon()
        {
            _trayIcon = new TrayIconHelper(this);
            _trayIcon.SetCallbacks(
                onShow: ShowWindow,
                onExit: ExitApplication,
                onRefresh: () => _viewModel?.RefreshCommand?.Execute(null),
                onSettings: OpenSettings
            );
        }

        private void OpenSettings()
        {
            try
            {
                // Open PowerToys Settings to PowerDisplay page
                // Use true for WinUI 3 apps as PowerToys.exe is in parent directory
                SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.PowerDisplay, true);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to open settings: {ex.Message}");
            }
        }

        private void ShowWindow()
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            // Adjust window size before showing
            AdjustWindowSizeToContent();

            // Reposition to bottom right (set position before showing)
            if (_appWindow != null)
            {
                PositionWindowAtBottomRight(_appWindow);
            }

            // Set initial state for animation
            RootGrid.Opacity = 0;

            // Show window
            WindowHelper.ShowWindow(hWnd, true);

            // Bring window to foreground
            WindowHelper.SetForegroundWindow(hWnd);

            // Use storyboard animation for window entrance
            if (RootGrid.Resources.ContainsKey("SlideInStoryboard"))
            {
                var slideInStoryboard = (Storyboard)RootGrid.Resources["SlideInStoryboard"];
                slideInStoryboard.Begin();
            }
        }

        private void HideWindow()
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            // Use storyboard animation for window exit
            if (RootGrid.Resources.ContainsKey("SlideOutStoryboard"))
            {
                var slideOutStoryboard = (Storyboard)RootGrid.Resources["SlideOutStoryboard"];
                slideOutStoryboard.Completed += (s, e) =>
                {
                    // Hide window after animation completes
                    WindowHelper.ShowWindow(hWnd, false);
                };
                slideOutStoryboard.Begin();
            }
            else
            {
                // Fallback: hide immediately if animation not found
                WindowHelper.ShowWindow(hWnd, false);
            }
        }

        private void OnUIRefreshRequested(object? sender, EventArgs e)
        {
            Logger.LogInfo("UI refresh requested due to settings change");
            _viewModel.ReloadMonitorSettings();

            // Adjust window size after settings change to accommodate visibility changes
            _ = Task.Run(async () =>
            {
                await Task.Delay(100); // Allow UI to update with new visibility settings
                DispatcherQueue.TryEnqueue(() => AdjustWindowSizeToContent());
            });
        }

        private void OnThemeChangeRequested(object? sender, ElementTheme theme)
        {
            Logger.LogInfo($"Theme change requested: {theme}");
            ApplyThemeFromSettings(theme);
        }

        private void ApplyThemeFromSettings(ElementTheme theme)
        {
            try
            {
                // Apply theme to window
                PowerDisplay.Helpers.ThemeManager.ApplyTheme(this, theme);

                // Update theme icon
                UpdateThemeIcon(theme == ElementTheme.Dark);

                Logger.LogInfo($"Theme applied: {theme}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to apply theme: {ex.Message}");
            }
        }

        private void OnMonitorsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Adjust window size when monitors collection changes (add/remove monitors)
            _ = Task.Run(async () =>
            {
                await Task.Delay(100); // Small delay to allow UI to update
                DispatcherQueue.TryEnqueue(() => AdjustWindowSizeToContent());
            });
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Adjust window size when relevant properties change
            if (e.PropertyName == nameof(_viewModel.IsScanning) ||
                e.PropertyName == nameof(_viewModel.HasMonitors) ||
                e.PropertyName == nameof(_viewModel.ShowNoMonitorsMessage))
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(50); // Small delay to allow UI to update
                    DispatcherQueue.TryEnqueue(() => AdjustWindowSizeToContent());
                });
            }
        }

        /// <summary>
        /// Set exit flag to allow window to close normally
        /// </summary>
        public void SetExiting()
        {
            _isExiting = true;
        }

        /// <summary>
        /// 快速关闭窗口，跳过动画和复杂清理
        /// </summary>
        public void FastShutdown()
        {
            try
            {
                _isExiting = true;

                // 立即释放托盘图标
                _trayIcon?.Dispose();

                // 快速清理 ViewModel
                if (_viewModel != null)
                {
                    // 取消事件订阅
                    _viewModel.UIRefreshRequested -= OnUIRefreshRequested;
                    _viewModel.Monitors.CollectionChanged -= OnMonitorsCollectionChanged;
                    _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                    _viewModel.ThemeChangeRequested -= OnThemeChangeRequested;

                    // 立即释放
                    _viewModel.Dispose();
                }

                // 直接关闭窗口，不等待动画
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WindowHelper.ShowWindow(hWnd, false);
            }
            catch
            {
                // 忽略清理错误，确保能够关闭
            }
        }

        private void ExitApplication()
        {
            try
            {
                // 使用快速关闭
                FastShutdown();

                // 直接调用应用程序快速退出
                if (Application.Current is App app)
                {
                    app.Shutdown();
                }

                // 确保立即退出
                Environment.Exit(0);
            }
            catch
            {
                // 确保能够退出
                Environment.Exit(0);
            }
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Add button press animation
                if (sender is Button button)
                {
                    await AnimateButtonPress(button);
                }

                // Refresh monitor list
                if (_viewModel?.RefreshCommand?.CanExecute(null) == true)
                {
                    _viewModel.RefreshCommand.Execute(null);
                    // Adjust window size after refresh
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(200); // Allow data to load
                        DispatcherQueue.TryEnqueue(() => AdjustWindowSizeToContent());
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"OnRefreshClick failed: {ex}");
                if (_viewModel != null)
                {
                    _viewModel.StatusText = "Refresh failed";
                }
            }
        }

        private async void OnThemeButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Add button press animation
                if (sender is Button button)
                {
                    await AnimateButtonPress(button);
                }

                var currentTheme = PowerDisplay.Helpers.ThemeManager.GetCurrentTheme(this);
                var newTheme = currentTheme == ElementTheme.Light ? ElementTheme.Dark : ElementTheme.Light;

                // Apply theme and sync to both settings systems
                PowerDisplay.Helpers.ThemeManager.ApplyThemeAndSync(this, newTheme);
                UpdateThemeIcon(newTheme == ElementTheme.Dark);

                Logger.LogInfo($"Theme toggled to: {newTheme}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"OnThemeButtonClick failed: {ex}");
            }
        }

        private void UpdateThemeIcon(bool isDark)
        {
            if (ThemeIcon != null)
            {
                // Dark theme shows sun icon, light theme shows moon icon
                ThemeIcon.Glyph = isDark ? "\uE706" : "\uE708";
            }
        }

        private void InitializeTheme()
        {
            // Load saved theme settings (with PowerToys settings priority)
            var savedTheme = PowerDisplay.Helpers.ThemeManager.GetSavedThemeWithPriority();
            if (savedTheme != ElementTheme.Default)
            {
                PowerDisplay.Helpers.ThemeManager.ApplyTheme(this, savedTheme);
            }

            // Update theme icon
            var isDark = PowerDisplay.Helpers.ThemeManager.IsDarkTheme(this);
            UpdateThemeIcon(isDark);
        }

        private async void OnLinkClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Add button press animation
                if (sender is Button button)
                {
                    await AnimateButtonPress(button);
                }

                // Link all monitor brightness (synchronized adjustment)
                if (_viewModel != null && _viewModel.Monitors.Count > 0)
                {
                    // Get first monitor brightness as reference
                    var baseBrightness = _viewModel.Monitors.First().Brightness;
                    _ = _viewModel.SetAllBrightnessAsync(baseBrightness);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"OnLinkClick failed: {ex}");
            }
        }

        private async void OnDisableClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Add button press animation
                if (sender is Button button)
                {
                    await AnimateButtonPress(button);
                }

                // Disable/enable all monitor controls
                if (_viewModel != null)
                {
                    foreach (var monitor in _viewModel.Monitors)
                    {
                        monitor.IsAvailable = !monitor.IsAvailable;
                    }
                    _viewModel.StatusText = _viewModel.Monitors.Any(m => m.IsAvailable)
                        ? "Display control enabled"
                        : "Display control disabled";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"OnDisableClick failed: {ex}");
            }
        }



        /// <summary>
        /// Get internal monitor name, consistent with SettingsManager logic
        /// </summary>

        private async void OnTestClick(object sender, RoutedEventArgs e)
        {
            ContentDialog? dlg = null;
            Core.MonitorManager? manager = null;
            
            try
            {
                // Test monitor discovery functionality
                dlg = new ContentDialog
                {
                    Title = "Monitor Detection Test",
                    Content = "Starting monitor detection...",
                    CloseButtonText = "Close",
                    XamlRoot = this.Content.XamlRoot
                };

                _ = dlg.ShowAsync();

                manager = new Core.MonitorManager();
                var monitors = await manager.DiscoverMonitorsAsync(new System.Threading.CancellationToken());

                string message = $"Found {monitors.Count} monitors:\n\n";
                foreach (var monitor in monitors)
                {
                    message += $"• {monitor.Name}\n";
                    message += $"  Type: {monitor.Type}\n";
                    message += $"  Brightness: {monitor.CurrentBrightness}%\n\n";
                }

                if (monitors.Count == 0)
                {
                    message = "No monitors found.\n\n";
                    message += "Possible reasons:\n";
                    message += "• DDC/CI not supported\n";
                    message += "• Driver issues\n";
                    message += "• Permission issues\n";
                    message += "• Cable doesn't support DDC/CI";
                }

                dlg.Content = message;

                // Don't dispose manager, use existing manager
                // Initialize ViewModel and bind to root Grid refresh
                if (monitors.Count > 0)
                {
                    // Use existing refresh command
                    await _viewModel.RefreshMonitorsAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"OnTestClick failed: {ex}");
                if (dlg != null)
                {
                    dlg.Content = $"Error: {ex.Message}\n\nType: {ex.GetType().Name}";
                }
            }
            finally
            {
                manager?.Dispose();
            }
        }
        
        private void SetupWindow()
        {
            try
            {
                // Get window handle
                var hWnd = WindowNative.GetWindowHandle(this);
                var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
                _appWindow = AppWindow.GetFromWindowId(windowId);

                if (_appWindow != null)
                {
                    // Set initial window size - will be adjusted later based on content
                    _appWindow.Resize(new SizeInt32 { Width = 640, Height = 480 });

                    // Position window at bottom right corner
                    PositionWindowAtBottomRight(_appWindow);

                    // Set window icon and title bar
                    _appWindow.Title = "PowerDisplay";

                    // Remove title bar and system buttons
                    var presenter = _appWindow.Presenter as OverlappedPresenter;
                    if (presenter != null)
                    {
                        // Disable resizing
                        presenter.IsResizable = false;
                        // Disable maximize button
                        presenter.IsMaximizable = false;
                        // Disable minimize button
                        presenter.IsMinimizable = false;
                        // Set borderless mode
                        presenter.SetBorderAndTitleBar(false, false);
                    }

                    // Custom title bar - completely remove all buttons
                    var titleBar = _appWindow.TitleBar;
                    if (titleBar != null)
                    {
                        // Extend content into title bar area
                        titleBar.ExtendsContentIntoTitleBar = true;
                        // Completely remove title bar height
                        titleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Collapsed;
                        // Set all button colors to transparent
                        titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                        titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                        titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                        titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                        titleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                        titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                        titleBar.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                        titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                        // Disable title bar interaction area
                        titleBar.SetDragRectangles(new Windows.Graphics.RectInt32[0]);
                    }

                    // Set modern Mica Alt backdrop for Windows 11
                    try
                    {
                        // Use Mica Alt for a more modern appearance
                        if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
                        {
                            this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
                        }
                        else
                        {
                            // Fallback to basic backdrop for older systems
                            this.SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
                        }
                    }
                    catch
                    {
                        // Fallback: use solid color background
                        this.SystemBackdrop = null;
                    }

                    // Use Win32 API to further disable window moving
                    WindowHelper.DisableWindowMovingAndResizing(hWnd);
                    // Hide window from taskbar
                    WindowHelper.HideFromTaskbar(hWnd);
                    // Optional: set window topmost
                    // WindowHelper.SetWindowTopmost(hWnd, true);
                }
            }
            catch
            {
                // Ignore window setup errors
            }
        }
        
        private void AdjustWindowSizeToContent()
        {
            try
            {
                if (_appWindow == null || RootGrid == null)
                    return;

                // Force layout update to ensure proper measurement
                RootGrid.UpdateLayout();

                // Get precise content height
                var availableWidth = 640.0;
                var contentHeight = GetContentHeight(availableWidth);

                // Account for display scaling
                var scale = RootGrid.XamlRoot?.RasterizationScale ?? 1.0;
                var scaledHeight = (int)Math.Ceiling(contentHeight * scale);

                // Only set maximum height for scrollable content
                scaledHeight = Math.Min(scaledHeight, 650);

                // Check if resize is needed
                var currentSize = _appWindow.Size;
                if (Math.Abs(currentSize.Height - scaledHeight) > 1)
                {
                    Logger.LogInfo($"Adjusting window height from {currentSize.Height} to {scaledHeight} (content: {contentHeight})");
                    _appWindow.Resize(new SizeInt32 { Width = 640, Height = scaledHeight });

                    // Update clip region to match new window size
                    UpdateClipRegion(640, scaledHeight / scale);

                    // Reposition to maintain bottom-right position
                    PositionWindowAtBottomRight(_appWindow);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adjusting window size: {ex.Message}");
            }
        }

        private double GetContentHeight(double availableWidth)
        {
            // Try to measure MainContainer directly for precise content size
            if (RootGrid.FindName("MainContainer") is Border mainContainer)
            {
                mainContainer.Measure(new Windows.Foundation.Size(availableWidth, double.PositiveInfinity));
                return mainContainer.DesiredSize.Height;
            }

            // Fallback: Measure the root grid
            RootGrid.Measure(new Windows.Foundation.Size(availableWidth, double.PositiveInfinity));
            return RootGrid.DesiredSize.Height + 4; // Small padding for fallback method
        }

        private void UpdateClipRegion(double width, double height)
        {
            // Clip region removed to allow automatic sizing
            // No longer needed as we removed the fixed clip from RootGrid
        }

        private void PositionWindowAtBottomRight(AppWindow appWindow)
        {
            try
            {
                // Get display area
                var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Nearest);
                if (displayArea != null)
                {
                    var workArea = displayArea.WorkArea;
                    var windowSize = appWindow.Size;

                    // Calculate bottom-right position, close to taskbar
                    // WorkArea already excludes taskbar area, so use WorkArea bottom directly
                    int rightMargin = 10; // Small margin from right edge
                    int x = workArea.Width - windowSize.Width - rightMargin;
                    int y = workArea.Height - windowSize.Height; // Close to taskbar top, no gap

                    // Move window to bottom right
                    appWindow.Move(new PointInt32 { X = x, Y = y });
                }
            }
            catch
            {
                // Ignore errors when positioning window
            }
        }

        /// <summary>
        /// Animates button press for modern interaction feedback
        /// </summary>
        /// <param name="button">The button to animate</param>
        private async Task AnimateButtonPress(Button button)
        {
            // Button animation disabled to avoid compilation errors
            // Using default button visual states instead
            await Task.CompletedTask;
        }

    }
}
