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
using PowerDisplay.Common.Models;
using PowerDisplay.Configuration;
using PowerDisplay.Core;
using PowerDisplay.Core.Interfaces;
using PowerDisplay.Helpers;
using PowerDisplay.ViewModels;
using Windows.Graphics;
using WinRT.Interop;
using Monitor = PowerDisplay.Common.Models.Monitor;

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

                // 1. Configure window immediately (synchronous, no data dependency)
                ConfigureWindow();

                // 2. Initialize UI text (synchronous, lightweight)
                InitializeUIText();

                // 3. Create ViewModel immediately (lightweight object, no scanning yet)
                _viewModel = new MainViewModel();
                RootGrid.DataContext = _viewModel;
                Bindings.Update();

                // 4. Register event handlers
                RegisterEventHandlers();

                // 5. Start background initialization (don't wait)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await InitializeAsync();
                        _hasInitialized = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Background initialization failed: {ex.Message}");
                        DispatcherQueue.TryEnqueue(() => ShowError($"Initialization failed: {ex.Message}"));
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"MainWindow initialization failed: {ex.Message}");
                ShowError($"Unable to start main window: {ex.Message}");
            }
        }

        /// <summary>
        /// Register all event handlers for window and ViewModel
        /// </summary>
        private void RegisterEventHandlers()
        {
            // Window events
            this.Closed += OnWindowClosed;
            this.Activated += OnWindowActivated;

            // ViewModel events
            _viewModel.UIRefreshRequested += OnUIRefreshRequested;
            _viewModel.Monitors.CollectionChanged += OnMonitorsCollectionChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // Button events
            LinkButton.Click += OnLinkClick;
            DisableButton.Click += OnDisableClick;
            RefreshButton.Click += OnRefreshClick;
        }

        private bool _hasInitialized;

        /// <summary>
        /// Ensures the window is properly initialized with ViewModel and data
        /// Can be called from external code (e.g., App startup) to pre-initialize in background
        /// </summary>
        public async Task EnsureInitializedAsync()
        {
            if (_hasInitialized)
            {
                return;
            }

            // Wait for background initialization to complete
            // This is a no-op if initialization already completed
            await InitializeAsync();
            _hasInitialized = true;
        }

        private async Task InitializeAsync()
        {
            try
            {
                // Perform monitor scanning (which internally calls ReloadMonitorSettingsAsync)
                await _viewModel.RefreshMonitorsAsync();

                // Adjust window size after data is loaded (must run on UI thread)
                DispatcherQueue.TryEnqueue(() => AdjustWindowSizeToContent());
            }
            catch (WmiLight.WmiException ex)
            {
                Logger.LogError($"WMI access failed: {ex.Message}");
                DispatcherQueue.TryEnqueue(() => ShowError("Unable to access internal display control, administrator privileges may be required."));
            }
            catch (Exception ex)
            {
                Logger.LogError($"Initialization failed: {ex.Message}");
                DispatcherQueue.TryEnqueue(() => ShowError($"Initialization failed: {ex.Message}"));
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

        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            // Auto-hide window when it loses focus (deactivated)
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                HideWindow();
            }
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            // Allow window to close if program is exiting
            if (_isExiting)
            {
                UnsubscribeFromViewModelEvents();
                args.Handled = false;
                return;
            }

            // If only user operation (although we hide close button), just hide window
            args.Handled = true; // Prevent window closing
            HideWindow();
        }

        /// <summary>
        /// Unsubscribe from all ViewModel events to prevent memory leaks.
        /// </summary>
        private void UnsubscribeFromViewModelEvents()
        {
            if (_viewModel != null)
            {
                _viewModel.UIRefreshRequested -= OnUIRefreshRequested;
                _viewModel.Monitors.CollectionChanged -= OnMonitorsCollectionChanged;
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }
        }

        public void ShowWindow()
        {
            try
            {
                // If not initialized, log warning but continue showing
                if (!_hasInitialized)
                {
                    Logger.LogWarning("Window not fully initialized yet, showing anyway");
                }

                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                AdjustWindowSizeToContent();

                if (_appWindow != null)
                {
                    PositionWindowAtBottomRight(_appWindow);
                }
                else
                {
                    Logger.LogWarning("AppWindow is null, skipping window repositioning");
                }

                RootGrid.Opacity = 0;
                this.Activate();
                WindowHelper.ShowWindow(hWnd, true);
                WindowHelpers.BringToForeground(hWnd);

                if (RootGrid.Resources.ContainsKey("SlideInStoryboard"))
                {
                    var slideInStoryboard = RootGrid.Resources["SlideInStoryboard"] as Storyboard;
                    slideInStoryboard?.Begin();
                }
                else
                {
                    Logger.LogWarning("SlideInStoryboard not found, window will appear without animation");
                    RootGrid.Opacity = 1;
                }

                bool isVisible = IsWindowVisible();
                if (!isVisible)
                {
                    Logger.LogError("Window not visible after show attempt, forcing visibility");
                    RootGrid.Opacity = 1;
                    this.Activate();
                    WindowHelpers.BringToForeground(hWnd);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to show window: {ex.Message}");
                throw;
            }
        }

        public void HideWindow()
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

        /// <summary>
        /// Check if window is currently visible
        /// </summary>
        /// <returns>True if window is visible, false otherwise</returns>
        public bool IsWindowVisible()
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            return WindowHelper.IsWindowVisible(hWnd);
        }

        /// <summary>
        /// Toggle window visibility (show if hidden, hide if visible)
        /// </summary>
        public void ToggleWindow()
        {
            try
            {
                bool isVisible = IsWindowVisible();
                Logger.LogInfo($"[ToggleWindow] IsWindowVisible returned: {isVisible}");

                if (isVisible)
                {
                    Logger.LogInfo("[ToggleWindow] Window is visible, calling HideWindow");
                    HideWindow();
                }
                else
                {
                    Logger.LogInfo("[ToggleWindow] Window is hidden, calling ShowWindow");
                    ShowWindow();
                }

                Logger.LogInfo("[ToggleWindow] Toggle completed");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to toggle window: {ex.Message}");
                throw;
            }
        }

        private void OnUIRefreshRequested(object? sender, EventArgs e)
        {
            // Adjust window size when UI configuration changes (feature visibility toggles)
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
        /// Fast shutdown: skip animations and complex cleanup
        /// </summary>
        public void FastShutdown()
        {
            try
            {
                _isExiting = true;

                // Quick cleanup of ViewModel
                UnsubscribeFromViewModelEvents();
                _viewModel?.Dispose();

                // Close window directly without animations
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WindowHelper.ShowWindow(hWnd, false);
            }
            catch (Exception ex)
            {
                // Ignore cleanup errors to ensure shutdown
                Logger.LogWarning($"FastShutdown error: {ex.Message}");
            }
        }

        private void ExitApplication()
        {
            try
            {
                // Use fast shutdown
                FastShutdown();

                // Call application shutdown directly
                if (Application.Current is App app)
                {
                    app.Shutdown();
                }

                // Ensure immediate exit
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                // Ensure exit even on error
                Logger.LogError($"ExitApplication error: {ex.Message}");
                Environment.Exit(0);
            }
        }

        private void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            try
            {
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

        private void OnLinkClick(object sender, RoutedEventArgs e)
        {
            try
            {
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

        private void OnDisableClick(object sender, RoutedEventArgs e)
        {
            try
            {
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
        /// Configure window properties (synchronous, no data dependency)
        /// </summary>
        private void ConfigureWindow()
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
                    _appWindow.Resize(new SizeInt32 { Width = AppConstants.UI.WindowWidth, Height = 480 });

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
                Logger.LogWarning($"Window configuration error: {ex.Message}");
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
                var availableWidth = (double)AppConstants.UI.WindowWidth;
                var contentHeight = GetContentHeight(availableWidth);

                // Account for display scaling
                var scale = RootGrid.XamlRoot?.RasterizationScale ?? 1.0;
                var scaledHeight = (int)Math.Ceiling(contentHeight * scale);

                // Only set maximum height for scrollable content
                scaledHeight = Math.Min(scaledHeight, AppConstants.UI.MaxWindowHeight);

                // Check if resize is needed
                var currentSize = _appWindow.Size;
                if (Math.Abs(currentSize.Height - scaledHeight) > 1)
                {
                    _appWindow.Resize(new SizeInt32 { Width = AppConstants.UI.WindowWidth, Height = scaledHeight });

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
                    int rightMargin = AppConstants.UI.WindowRightMargin; // Small margin from right edge
                    int x = workArea.Width - windowSize.Width - rightMargin;
                    int y = workArea.Height - windowSize.Height; // Close to taskbar top, no gap

                    // Move window to bottom right
                    appWindow.Move(new PointInt32 { X = x, Y = y });
                }
            }
            catch (Exception ex)
            {
                // Window positioning failures are non-critical, just log for diagnostics
                Logger.LogDebug($"[PositionWindow] Failed to position window: {ex.Message}");
            }
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
                    break;

                // ColorTemperature case removed - now controlled via Settings UI
                case "Contrast":
                    monitorVm.ContrastPercent = finalValue;
                    break;
                case "Volume":
                    monitorVm.Volume = finalValue;
                    break;
            }
        }

        public void Dispose()
        {
            _viewModel?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
