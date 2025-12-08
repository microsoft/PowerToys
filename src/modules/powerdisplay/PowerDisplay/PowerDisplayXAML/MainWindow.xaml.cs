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
using PowerDisplay.Helpers;
using PowerDisplay.ViewModels;
using Windows.Graphics;
using WinRT.Interop;
using WinUIEx;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay
{
    /// <summary>
    /// PowerDisplay main window
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicMethods)]
    public sealed partial class MainWindow : WindowEx, IDisposable
    {
        private readonly ISettingsUtils _settingsUtils = SettingsUtils.Default;
        private MainViewModel? _viewModel;
        private AppWindow? _appWindow;
        private bool _isExiting;

        // Expose ViewModel as property for x:Bind
        public MainViewModel ViewModel => _viewModel ?? throw new InvalidOperationException("ViewModel not initialized");

        // Conversion functions for x:Bind (AOT-compatible alternative to converters)
        public Visibility ConvertBoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

        public MainWindow()
        {
            try
            {
                // 1. Create ViewModel BEFORE InitializeComponent to avoid x:Bind failures
                // x:Bind evaluates during InitializeComponent, so ViewModel must exist first
                _viewModel = new MainViewModel();

                this.InitializeComponent();

                // 2. Configure window immediately (synchronous, no data dependency)
                ConfigureWindow();

                // 3. Set up data context and update bindings
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

            // ViewModel events - _viewModel is guaranteed non-null here as this is called after initialization
            if (_viewModel != null)
            {
                _viewModel.UIRefreshRequested += OnUIRefreshRequested;
                _viewModel.Monitors.CollectionChanged += OnMonitorsCollectionChanged;
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            }
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
                if (_viewModel != null)
                {
                    await _viewModel.RefreshMonitorsAsync();
                }

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

                this.Activate();
                WindowHelper.ShowWindow(hWnd, true);
                WindowHelpers.BringToForeground(hWnd);

                // Force a resize AFTER the window is shown.
                // This is critical because the OS might restore the window to a previous (incorrect) size
                // when ShowWindow is called, ignoring our pre-show adjustment.
                // By queuing this on the dispatcher, we ensure it runs after the window is visible and layout is active.
                DispatcherQueue.TryEnqueue(() =>
                {
                    AdjustWindowSizeToContent();

                    // Clear focus from any interactive element (e.g., Slider) to prevent
                    // showing the value tooltip when the window opens
                    RootGrid.Focus(FocusState.Programmatic);
                });

                bool isVisible = IsWindowVisible();
                if (!isVisible)
                {
                    Logger.LogError("Window not visible after show attempt, forcing visibility");
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

            // Fallback: hide immediately if animation not found
            WindowHelper.ShowWindow(hWnd, false);
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

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            // Open PowerDisplay settings in PowerToys Settings UI
            // mainExecutableIsOnTheParentFolder = true because PowerDisplay is a WinUI 3 app
            // deployed in a subfolder (PowerDisplay\) while PowerToys.exe is in the parent folder
            SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.PowerDisplay, true);
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
                    Logger.LogWarning("[AdjustSize] _appWindow or RootGrid is null, aborting");
                    return;
                }

                Logger.LogDebug($"[AdjustSize] Starting adjustment, current window size: {_appWindow.Size.Width}x{_appWindow.Size.Height}");

                // Force layout update to ensure proper measurement
                RootGrid.UpdateLayout();

                // Get precise content height
                var availableWidth = (double)AppConstants.UI.WindowWidth;
                var contentHeight = GetContentHeight(availableWidth);

                Logger.LogDebug($"[AdjustSize] Content height from measurement: {contentHeight} DIU");

                // Use unified DPI scaling method (consistent with FlyoutWindow pattern)
                double dpiScale = WindowHelper.GetDpiScale(this);
                Logger.LogDebug($"[AdjustSize] DPI scale: {dpiScale} ({dpiScale * 100}%)");

                int scaledHeight = WindowHelper.ScaleToPhysicalPixels((int)Math.Ceiling(contentHeight), dpiScale);
                Logger.LogDebug($"[AdjustSize] Scaled height (physical pixels): {scaledHeight}");

                // Apply maximum height limit (also needs DPI scaling)
                int maxHeight = WindowHelper.ScaleToPhysicalPixels(AppConstants.UI.MaxWindowHeight, dpiScale);
                Logger.LogDebug($"[AdjustSize] Max height limit (physical pixels): {maxHeight}");

                scaledHeight = Math.Min(scaledHeight, maxHeight);
                Logger.LogDebug($"[AdjustSize] Final scaled height after limit: {scaledHeight}");

                // Check if resize is needed
                // Check if resize is needed
                var currentSize = _appWindow.Size;
                if (Math.Abs(currentSize.Height - scaledHeight) > 1)
                {
                    // Convert scaled height back to DIU and reposition using DPI-aware method
                    int heightInDiu = (int)Math.Ceiling(scaledHeight / dpiScale);

                    WindowHelper.PositionWindowBottomRight(
                        this,
                        AppConstants.UI.WindowWidth,
                        heightInDiu,
                        AppConstants.UI.WindowRightMargin);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adjusting window size: {ex.Message}");
            }
        }

        private double GetContentHeight(double availableWidth)
        {
            // Elegant solution: Measure the MainContainer directly.
            // This lets the XAML layout engine calculate the exact height required by all visible content.
            if (MainContainer != null)
            {
                MainContainer.Measure(new Windows.Foundation.Size(availableWidth, double.PositiveInfinity));
                return MainContainer.DesiredSize.Height;
            }

            return 0;
        }

        private void PositionWindowAtBottomRight(AppWindow appWindow)
        {
            try
            {
                var windowSize = appWindow.Size;
                WindowHelper.PositionWindowBottomRight(
                    this,  // MainWindow inherits from WindowEx
                    AppConstants.UI.WindowWidth,
                    windowSize.Height,
                    AppConstants.UI.WindowRightMargin);
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
            Logger.LogDebug("[UI] Slider_PointerCaptureLost event triggered");

            var slider = sender as Slider;
            if (slider == null)
            {
                Logger.LogWarning("[UI] Slider is null in PointerCaptureLost");
                return;
            }

            var propertyName = slider.Tag as string;
            var monitorVm = slider.DataContext as MonitorViewModel;

            Logger.LogDebug($"[UI] Property: {propertyName}, MonitorVM: {(monitorVm != null ? monitorVm.Name : "NULL")}, Value: {slider.Value}");

            if (monitorVm == null || propertyName == null)
            {
                Logger.LogWarning($"[UI] Null check failed - MonitorVM: {monitorVm == null}, PropertyName: {propertyName == null}");
                return;
            }

            // Get final value after drag completes
            int finalValue = (int)slider.Value;

            Logger.LogInfo($"[UI] Updating {propertyName} to {finalValue} for monitor {monitorVm.Name}");

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

            Logger.LogDebug($"[UI] ViewModel property {propertyName} updated successfully");
        }

        /// <summary>
        /// Input source item click handler - switches the monitor input source
        /// </summary>
        private async void InputSourceItem_Click(object sender, RoutedEventArgs e)
        {
            Logger.LogInfo("[UI] InputSourceItem_Click: Event triggered!");

            if (sender is not Button button)
            {
                Logger.LogWarning("[UI] InputSourceItem_Click: sender is not Button");
                return;
            }

            Logger.LogInfo($"[UI] InputSourceItem_Click: Button clicked, Tag type = {button.Tag?.GetType().Name ?? "null"}, DataContext type = {button.DataContext?.GetType().Name ?? "null"}");

            // Get the InputSourceItem from Tag (not DataContext - Flyout doesn't inherit DataContext properly)
            var inputSourceItem = button.Tag as InputSourceItem;
            if (inputSourceItem == null)
            {
                Logger.LogWarning("[UI] InputSourceItem_Click: Tag is not InputSourceItem");
                return;
            }

            Logger.LogInfo($"[UI] InputSourceItem_Click: InputSourceItem found - Value=0x{inputSourceItem.Value:X2}, Name={inputSourceItem.Name}, MonitorId={inputSourceItem.MonitorId}");

            int inputSourceValue = inputSourceItem.Value;
            string monitorId = inputSourceItem.MonitorId;

            // Use MonitorId for direct lookup (Flyout popup is not in visual tree)
            MonitorViewModel? monitorVm = null;

            if (!string.IsNullOrEmpty(monitorId) && _viewModel != null)
            {
                monitorVm = _viewModel.Monitors.FirstOrDefault(m => m.Id == monitorId);
                Logger.LogInfo($"[UI] InputSourceItem_Click: Found MonitorViewModel by ID: {monitorVm?.Name ?? "null"}");
            }

            // Fallback: search through all monitors (for backwards compatibility)
            if (monitorVm == null && _viewModel != null)
            {
                Logger.LogInfo("[UI] InputSourceItem_Click: MonitorId lookup failed, trying fallback search");
                foreach (var vm in _viewModel.Monitors)
                {
                    if (vm.SupportsInputSource && vm.AvailableInputSources != null)
                    {
                        if (vm.AvailableInputSources.Any(s => s.Value == inputSourceValue))
                        {
                            monitorVm = vm;
                            Logger.LogInfo($"[UI] InputSourceItem_Click: Found MonitorViewModel by fallback: {vm.Name}");
                            break;
                        }
                    }
                }
            }

            if (monitorVm == null)
            {
                Logger.LogWarning("[UI] InputSourceItem_Click: Could not find MonitorViewModel");
                return;
            }

            Logger.LogInfo($"[UI] Switching input source for {monitorVm.Name} to 0x{inputSourceValue:X2} ({inputSourceItem.Name})");

            // Set the input source
            await monitorVm.SetInputSourceAsync(inputSourceValue);

            Logger.LogInfo("[UI] InputSourceItem_Click: SetInputSourceAsync completed");
        }

        /// <summary>
        /// Input source ListView selection changed handler - switches the monitor input source
        /// </summary>
        private async void InputSourceListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListView listView)
            {
                return;
            }

            // Get the selected input source item
            var selectedItem = listView.SelectedItem as InputSourceItem;
            if (selectedItem == null)
            {
                return;
            }

            Logger.LogInfo($"[UI] InputSourceListView_SelectionChanged: Selected {selectedItem.Name} (0x{selectedItem.Value:X2}) for monitor {selectedItem.MonitorId}");

            // Find the monitor by ID
            MonitorViewModel? monitorVm = null;
            if (!string.IsNullOrEmpty(selectedItem.MonitorId) && _viewModel != null)
            {
                monitorVm = _viewModel.Monitors.FirstOrDefault(m => m.Id == selectedItem.MonitorId);
            }

            if (monitorVm == null)
            {
                Logger.LogWarning("[UI] InputSourceListView_SelectionChanged: Could not find MonitorViewModel");
                return;
            }

            // Set the input source
            await monitorVm.SetInputSourceAsync(selectedItem.Value);

            // Close the flyout after selection
            if (listView.Parent is StackPanel stackPanel &&
                stackPanel.Parent is Flyout flyout)
            {
                flyout.Hide();
            }
        }

        /// <summary>
        /// Rotation button click handler - changes monitor orientation
        /// </summary>
        private async void RotationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Microsoft.UI.Xaml.Controls.Primitives.ToggleButton toggleButton)
            {
                return;
            }

            // Get the orientation from the Tag
            if (toggleButton.Tag is not string tagStr || !int.TryParse(tagStr, out int orientation))
            {
                Logger.LogWarning("[UI] RotationButton_Click: Invalid Tag");
                return;
            }

            var monitorVm = toggleButton.DataContext as MonitorViewModel;
            if (monitorVm == null)
            {
                Logger.LogWarning("[UI] RotationButton_Click: Could not find MonitorViewModel");
                return;
            }

            // If clicking the current orientation, restore the checked state and do nothing
            if (monitorVm.CurrentRotation == orientation)
            {
                toggleButton.IsChecked = true;
                return;
            }

            Logger.LogInfo($"[UI] RotationButton_Click: Setting rotation for {monitorVm.Name} to {orientation}");

            // Set the rotation
            await monitorVm.SetRotationAsync(orientation);
        }

        /// <summary>
        /// Profile selection changed handler - applies the selected profile
        /// </summary>
        private void ProfileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListView listView)
            {
                return;
            }

            var selectedProfile = listView.SelectedItem as PowerDisplayProfile;
            if (selectedProfile == null || !selectedProfile.IsValid())
            {
                return;
            }

            Logger.LogInfo($"[UI] ProfileListView_SelectionChanged: Applying profile '{selectedProfile.Name}'");

            // Apply profile via ViewModel command
            if (_viewModel?.ApplyProfileCommand?.CanExecute(selectedProfile) == true)
            {
                _viewModel.ApplyProfileCommand.Execute(selectedProfile);
            }

            // Close the flyout after selection
            ProfilesFlyout?.Hide();

            // Clear selection to allow reselecting the same profile
            listView.SelectedItem = null;
        }

        public void Dispose()
        {
            _viewModel?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
