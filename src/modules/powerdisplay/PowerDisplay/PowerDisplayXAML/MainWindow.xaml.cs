// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using PowerDisplay.Core;
using PowerDisplay.Core.Interfaces;
using PowerDisplay.Core.Models;
using PowerDisplay.Helpers;
using PowerDisplay.Native;
using PowerDisplay.ViewModels;
using Windows.Graphics;
using WinRT.Interop;
using static PowerDisplay.Native.PInvoke;
using Monitor = PowerDisplay.Core.Models.Monitor;

namespace PowerDisplay
{
    /// <summary>
    /// PowerDisplay main window
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicMethods)]
    public sealed partial class MainWindow : Window, IDisposable
    {
        private readonly ISettingsUtils _settingsUtils = new SettingsUtils();
        private MainViewModel _viewModel = null!;
        private TrayIconHelper _trayIcon = null!;
        private AppWindow _appWindow = null!;
        private bool _isExiting;

        // Expose ViewModel as property for x:Bind
        public MainViewModel ViewModel => _viewModel;

        // Conversion functions for x:Bind (AOT-compatible alternative to converters)
        public Visibility ConvertBoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

        public MainWindow()
        {
            try
            {
                this.InitializeComponent();

                // Lightweight initialization - no heavy operations in constructor
                // Setup window properties
                SetupWindow();

                // Initialize tray icon
                InitializeTrayIcon();

                // Initialize UI text
                InitializeUIText();

                // Clean up resources on window close
                this.Closed += OnWindowClosed;

                // Delay ViewModel creation until first activation (async)
                this.Activated += OnFirstActivated;
            }
            catch (Exception ex)
            {
                Logger.LogError($"MainWindow initialization failed: {ex.Message}");
                ShowError($"Unable to start main window: {ex.Message}");
            }
        }

        private bool _hasInitialized;

        private async void OnFirstActivated(object sender, WindowActivatedEventArgs args)
        {
            // Only initialize once on first activation
            if (_hasInitialized)
            {
                return;
            }

            _hasInitialized = true;
            this.Activated -= OnFirstActivated; // Unsubscribe after first run

            // Create and initialize ViewModel asynchronously
            // This will trigger Loading UI (IsScanning) during monitor discovery
            _viewModel = new MainViewModel();
            RootGrid.DataContext = _viewModel;

            // Notify bindings that ViewModel is now available (for x:Bind)
            Bindings.Update();

            // Initialize ViewModel event handlers
            _viewModel.UIRefreshRequested += OnUIRefreshRequested;
            _viewModel.Monitors.CollectionChanged += OnMonitorsCollectionChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // Bind button events
            LinkButton.Click += OnLinkClick;
            DisableButton.Click += OnDisableClick;
            RefreshButton.Click += OnRefreshClick;

            // Start async initialization (monitor scanning happens here)
            await InitializeAsync();

            // Hide window after initialization completes
            HideWindow();
        }

        private async Task InitializeAsync()
        {
            try
            {
                // No delays! Direct async operation
                await _viewModel.RefreshMonitorsAsync();
                await _viewModel.ReloadMonitorSettingsAsync();

                // Adjust window size after data is loaded (event-driven)
                AdjustWindowSizeToContent();
            }
            catch (WmiLight.WmiException ex)
            {
                Logger.LogError($"WMI access failed: {ex.Message}");
                ShowError("Unable to access internal display control, administrator privileges may be required.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Initialization failed: {ex.Message}");
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
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(RefreshButton, loader.GetString("RefreshTooltip"));
            }
            catch (Exception ex)
            {
                // Use English defaults if resource loading fails
                Logger.LogWarning($"Failed to load localized strings: {ex.Message}");
                ScanningMonitorsTextBlock.Text = "Scanning monitors...";
                NoMonitorsTextBlock.Text = "No monitors detected";
                AdjustBrightnessTextBlock.Text = "PowerDisplay";

                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(LinkButton, "Synchronize all monitors to the same brightness");
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(DisableButton, "Enable or disable brightness control");
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(RefreshButton, "Rescan connected monitors");
            }
        }

        private void ShowError(string message)
        {
            if (_viewModel != null)
            {
                _viewModel.StatusText = $"Error: {message}";
            }
            else
            {
                Logger.LogError($"Error (ViewModel not yet initialized): {message}");
            }
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
                PowerDisplay.Helpers.SettingsDeepLink.OpenPowerDisplaySettings();
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
            PInvoke.SetForegroundWindow(hWnd);

            // Use storyboard animation for window entrance
            if (RootGrid.Resources.ContainsKey("SlideInStoryboard"))
            {
                var slideInStoryboard = RootGrid.Resources["SlideInStoryboard"] as Storyboard;
                slideInStoryboard?.Begin();
            }
        }

        private void HideWindow()
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            // Use storyboard animation for window exit
            if (RootGrid.Resources.ContainsKey("SlideOutStoryboard"))
            {
                var slideOutStoryboard = RootGrid.Resources["SlideOutStoryboard"] as Storyboard;
                if (slideOutStoryboard != null)
                {
                    slideOutStoryboard.Completed += (s, e) =>
                    {
                        // Hide window after animation completes
                        WindowHelper.ShowWindow(hWnd, false);
                    };
                    slideOutStoryboard.Begin();
                }
            }
            else
            {
                // Fallback: hide immediately if animation not found
                WindowHelper.ShowWindow(hWnd, false);
            }
        }

        private async void OnUIRefreshRequested(object? sender, EventArgs e)
        {
            Logger.LogInfo("UI refresh requested due to settings change");
            await _viewModel.ReloadMonitorSettingsAsync();

            // Adjust window size after settings are reloaded (no delay needed!)
            DispatcherQueue.TryEnqueue(() => AdjustWindowSizeToContent());
        }

        private void OnMonitorsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Adjust window size when monitors collection changes (event-driven!)
            // The UI binding will update first, then we adjust size
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                AdjustWindowSizeToContent();
            });
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Adjust window size when relevant properties change (event-driven!)
            if (e.PropertyName == nameof(_viewModel.IsScanning) ||
                e.PropertyName == nameof(_viewModel.HasMonitors) ||
                e.PropertyName == nameof(_viewModel.ShowNoMonitorsMessage))
            {
                // Use Low priority to ensure UI bindings update first
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    AdjustWindowSizeToContent();
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

                    // 立即释放
                    _viewModel.Dispose();
                }

                // 直接关闭窗口，不等待动画
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WindowHelper.ShowWindow(hWnd, false);
            }
            catch (Exception ex)
            {
                // 忽略清理错误，确保能够关闭
                Logger.LogWarning($"FastShutdown error: {ex.Message}");
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
            catch (Exception ex)
            {
                // 确保能够退出
                Logger.LogError($"ExitApplication error: {ex.Message}");
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
                    // Window size will be adjusted automatically by OnMonitorsCollectionChanged event!
                    // No delay needed - event-driven design
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
                    XamlRoot = this.Content.XamlRoot,
                };

                _ = dlg.ShowAsync();

                manager = new Core.MonitorManager();
                var monitors = await manager.DiscoverMonitorsAsync(default(System.Threading.CancellationToken));

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
                        titleBar.SetDragRectangles(Array.Empty<Windows.Graphics.RectInt32>());
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
            catch (Exception ex)
            {
                // Ignore window setup errors
                Logger.LogWarning($"Window setup error: {ex.Message}");
            }
        }

        private void AdjustWindowSizeToContent()
        {
            try
            {
                if (_appWindow == null || RootGrid == null)
                {
                    return;
                }

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
            catch (Exception ex)
            {
                // Ignore errors when positioning window
                Logger.LogDebug($"Failed to position window: {ex.Message}");
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

        /// <summary>
        /// Slider ValueChanged event handler - does nothing during drag
        /// This allows the slider UI to update smoothly without triggering hardware operations
        /// </summary>
        private void Slider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            // During drag, this event fires 60-120 times per second
            // We intentionally do nothing here to keep UI smooth
            // The actual ViewModel update happens in PointerCaptureLost after drag completes
        }

        /// <summary>
        /// Slider PointerCaptureLost event handler - updates ViewModel when drag completes
        /// This is the WinUI3 recommended way to detect drag completion
        /// </summary>
        private void Slider_PointerCaptureLost(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var slider = sender as Slider;
            if (slider == null)
            {
                return;
            }

            var propertyName = slider.Tag as string;
            var monitorVm = slider.DataContext as MonitorViewModel;

            if (monitorVm == null || propertyName == null)
            {
                return;
            }

            // Get final value after drag completes
            int finalValue = (int)slider.Value;

            // Now update the ViewModel, which will trigger hardware operation
            switch (propertyName)
            {
                case "Brightness":
                    monitorVm.Brightness = finalValue;
                    Logger.LogDebug($"[UI] Brightness drag completed: {finalValue}");
                    break;
                case "ColorTemperature":
                    monitorVm.ColorTemperaturePercent = finalValue;
                    Logger.LogDebug($"[UI] ColorTemperature drag completed: {finalValue}%");
                    break;
                case "Contrast":
                    monitorVm.ContrastPercent = finalValue;
                    Logger.LogDebug($"[UI] Contrast drag completed: {finalValue}%");
                    break;
                case "Volume":
                    monitorVm.Volume = finalValue;
                    Logger.LogDebug($"[UI] Volume drag completed: {finalValue}");
                    break;
            }
        }

        public void Dispose()
        {
            _viewModel?.Dispose();
            _trayIcon?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
