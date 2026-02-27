// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using PowerDisplay.Common.Models;
using PowerDisplay.Configuration;
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
        private readonly SettingsUtils _settingsUtils = SettingsUtils.Default;
        private MainViewModel? _viewModel;
        private HotkeyService? _hotkeyService;
        private nint _hWnd;
        private bool _isWindowCloaked;
        private DispatcherTimer? _resizeDebounceTimer;

        // Expose ViewModel as property for x:Bind
        public MainViewModel ViewModel => _viewModel ?? throw new InvalidOperationException("ViewModel not initialized");

        public MainWindow()
        {
            Logger.LogInfo("MainWindow constructor: Starting");
            try
            {
                // 1. Create ViewModel BEFORE InitializeComponent to avoid x:Bind failures
                // x:Bind evaluates during InitializeComponent, so ViewModel must exist first
                Logger.LogTrace("MainWindow constructor: Creating MainViewModel");
                _viewModel = new MainViewModel();
                Logger.LogTrace("MainWindow constructor: MainViewModel created");

                Logger.LogTrace("MainWindow constructor: Calling InitializeComponent");
                this.InitializeComponent();
                _hWnd = WindowNative.GetWindowHandle(this);

                // Cloak window immediately to prevent any visual artifacts during setup.
                // The window stays cloaked until the first ShowWindow() call.
                _isWindowCloaked = WindowHelper.CloakWindow(_hWnd);
                Logger.LogTrace("MainWindow constructor: InitializeComponent completed, window cloaked");

                // 2. Configure window immediately (synchronous, no data dependency)
                Logger.LogTrace("MainWindow constructor: Configuring window");
                ConfigureWindow();

                // 3. Set up data context and update bindings
                RootGrid.DataContext = _viewModel;
                Bindings.Update();
                Logger.LogTrace("MainWindow constructor: Data context set and bindings updated");

                // 4. Register event handlers
                RegisterEventHandlers();
                Logger.LogTrace("MainWindow constructor: Event handlers registered");

                // 5. Initialize HotkeyService for in-process hotkey handling (CmdPal pattern)
                // This avoids IPC timing issues with Runner's centralized hotkey mechanism
                Logger.LogTrace("MainWindow constructor: Initializing HotkeyService");
                _hotkeyService = new HotkeyService(_settingsUtils, ToggleWindow);
                _hotkeyService.Initialize(this);
                Logger.LogTrace("MainWindow constructor: HotkeyService initialized");

                // Note: ViewModel handles all async initialization internally.
                // We listen to InitializationCompleted event to know when data is ready.
                // No duplicate initialization here - single responsibility in ViewModel.
                Logger.LogInfo("MainWindow constructor: Completed");
            }
            catch (Exception ex)
            {
                Logger.LogError($"MainWindow constructor: Initialization failed: {ex.Message}\n{ex.StackTrace}");
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
                _viewModel.InitializationCompleted += OnViewModelInitializationCompleted;
                _viewModel.UIRefreshRequested += OnUIRefreshRequested;
                _viewModel.Monitors.CollectionChanged += OnMonitorsCollectionChanged;
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            }
        }

        /// <summary>
        /// Called when ViewModel completes initial monitor discovery.
        /// This is the single source of truth for initialization state.
        /// </summary>
        private void OnViewModelInitializationCompleted(object? sender, EventArgs e)
        {
            _hasInitialized = true;
            Logger.LogInfo("MainWindow: Initialization completed via ViewModel event, _hasInitialized=true");
            ScheduleAdjustWindowSize();
        }

        private bool _hasInitialized;

        private void ShowError(string message)
        {
            Logger.LogError($"Error: {message}");
        }

        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            Logger.LogTrace($"OnWindowActivated: WindowActivationState={args.WindowActivationState}");

            // Auto-hide window when it loses focus (deactivated)
            // Only hide if window is actually visible (not cloaked), to avoid
            // spurious hide events when the window is activated while cloaked during setup.
            if (args.WindowActivationState == WindowActivationState.Deactivated && !_isWindowCloaked)
            {
                Logger.LogInfo("OnWindowActivated: Window deactivated, hiding window");
                HideWindow();
            }
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            // If only user operation (although we hide close button), just hide window
            args.Handled = true; // Prevent window closing
            HideWindow();
        }

        public void ShowWindow()
        {
            Logger.LogInfo($"ShowWindow: Called, _hasInitialized={_hasInitialized}");
            try
            {
                // If not initialized, log warning but continue showing
                if (!_hasInitialized)
                {
                    Logger.LogWarning("ShowWindow: Window not fully initialized yet, showing anyway");
                }

                // Ensure window is cloaked during setup to prevent flicker.
                // All positioning, DPI transitions, and layout happen invisibly.
                if (!_isWindowCloaked)
                {
                    _isWindowCloaked = WindowHelper.CloakWindow(_hWnd);
                }

                // Adjust size and position while cloaked.
                // Two-phase positioning in PositionWindowBottomRight handles cross-monitor
                // DPI transitions correctly (Move to target monitor first, then MoveAndResize).
                Logger.LogTrace("ShowWindow: Adjusting window size to content (cloaked)");
                AdjustWindowSizeToContent();

                // Activate and show window (still cloaked - invisible to user)
                Logger.LogTrace("ShowWindow: Activating window (cloaked)");
                this.Activate();
                WindowHelper.ShowWindow(_hWnd, true);
                WindowHelper.SetWindowTopmost(_hWnd, true);
                WindowHelper.BringToFront(_hWnd);

                // Clear focus from any interactive element (e.g., Slider) to prevent
                // showing the value tooltip when the window opens
                RootGrid.Focus(FocusState.Programmatic);

                // Uncloak to reveal the window at its final position and size.
                // All DPI transitions and layout are already complete.
                if (_isWindowCloaked)
                {
                    if (WindowHelper.UncloakWindow(_hWnd))
                    {
                        _isWindowCloaked = false;
                        Logger.LogTrace("ShowWindow: Window uncloaked");
                    }
                    else
                    {
                        Logger.LogError("ShowWindow: Failed to uncloak window");
                    }
                }

                Logger.LogInfo("ShowWindow: Window shown successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError($"ShowWindow: Failed to show window: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        public void HideWindow()
        {
            Logger.LogInfo("HideWindow: Hiding window");

            // Cloak the window to make it instantly invisible at the DWM level.
            // The window stays "shown" so the XAML rendering pipeline remains warm,
            // avoiding cold-start flicker on the next ShowWindow() call.
            if (!_isWindowCloaked)
            {
                _isWindowCloaked = WindowHelper.CloakWindow(_hWnd);
            }

            Logger.LogTrace("HideWindow: Window cloaked (hidden)");
        }

        /// <summary>
        /// Check if window is currently visible (not cloaked)
        /// </summary>
        /// <returns>True if window is visible and not cloaked, false otherwise</returns>
        public bool IsWindowVisible()
        {
            // Window is only truly visible when it's not cloaked
            bool visible = !_isWindowCloaked && WindowHelper.IsWindowVisible(_hWnd);
            Logger.LogTrace($"IsWindowVisible: Returning {visible} (cloaked={_isWindowCloaked})");
            return visible;
        }

        /// <summary>
        /// Toggle window visibility (show if hidden, hide if visible)
        /// </summary>
        public void ToggleWindow()
        {
            bool currentlyVisible = IsWindowVisible();
            Logger.LogInfo($"ToggleWindow: Called, current visibility={currentlyVisible}");
            try
            {
                if (currentlyVisible)
                {
                    Logger.LogInfo("ToggleWindow: Window is visible, hiding");
                    HideWindow();
                }
                else
                {
                    Logger.LogInfo("ToggleWindow: Window is hidden, showing");
                    ShowWindow();
                }

                Logger.LogInfo($"ToggleWindow: Completed, new visibility={IsWindowVisible()}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"ToggleWindow: Failed to toggle window: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        private void OnUIRefreshRequested(object? sender, EventArgs e)
        {
            // Adjust window size when UI configuration changes (feature visibility toggles)
            ScheduleAdjustWindowSize();
        }

        private void OnMonitorsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Adjust window size when monitors collection changes (event-driven!)
            ScheduleAdjustWindowSize();
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Adjust window size when relevant properties change (event-driven!)
            if (e.PropertyName == nameof(_viewModel.IsScanning) ||
                e.PropertyName == nameof(_viewModel.HasMonitors) ||
                e.PropertyName == nameof(_viewModel.ShowNoMonitorsMessage))
            {
                ScheduleAdjustWindowSize();
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
            }
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            // Open PowerDisplay settings in PowerToys Settings UI
            // mainExecutableIsOnTheParentFolder = true because PowerDisplay is a WinUI 3 app
            // deployed in a subfolder (PowerDisplay\) while PowerToys.exe is in the parent folder
            SettingsDeepLink.OpenSettings(true);
        }

        /// <summary>
        /// Configure window properties (synchronous, no data dependency)
        /// </summary>
        private void ConfigureWindow()
        {
            try
            {
                // Base popup window configuration (presenter, taskbar, title bar collapse)
                WindowHelper.ConfigureAsPopupWindow(this, _hWnd);

                // Set minimal initial window size - will be adjusted before showing
                // Using minimal height to prevent "large window shrinking" flicker
                this.AppWindow.Resize(new SizeInt32 { Width = AppConstants.UI.WindowWidth, Height = 100 });

                // Position window at bottom right corner
                PositionWindowAtBottomRight();

                // Set window title
                this.AppWindow.Title = "PowerDisplay";

                // Additional title bar customization: make all buttons fully transparent
                var titleBar = this.AppWindow.TitleBar;
                if (titleBar != null)
                {
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

                // Use Win32 API to further disable window moving (removes WS_CAPTION, WS_SYSMENU, etc.)
                WindowHelper.DisableWindowMovingAndResizing(_hWnd);
            }
            catch (Exception ex)
            {
                // Ignore window setup errors
                Logger.LogWarning($"Window configuration error: {ex.Message}");
            }
        }

        /// <summary>
        /// Schedule a debounced window size adjustment. Multiple calls within the
        /// debounce interval (50ms) are coalesced into a single resize, preventing
        /// flicker when multiple events fire in quick succession (e.g., monitors
        /// being added one by one, property changes during scan completion).
        /// </summary>
        private void ScheduleAdjustWindowSize()
        {
            if (_resizeDebounceTimer == null)
            {
                _resizeDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                _resizeDebounceTimer.Tick += (s, e) =>
                {
                    _resizeDebounceTimer.Stop();
                    AdjustWindowSizeToContent();
                };
            }

            // Restart the timer on each call - only the last one fires
            _resizeDebounceTimer.Stop();
            _resizeDebounceTimer.Start();
        }

        private void AdjustWindowSizeToContent()
        {
            try
            {
                if (RootGrid == null)
                {
                    return;
                }

                // Force layout update and measure content height
                RootGrid.UpdateLayout();
                MainContainer?.Measure(new Windows.Foundation.Size(AppConstants.UI.WindowWidth, double.PositiveInfinity));
                var contentHeight = (int)Math.Ceiling(MainContainer?.DesiredSize.Height ?? 0);

                // Apply min/max height limits and reposition
                // Min height ensures window is visible even if content hasn't loaded yet
                var finalHeight = Math.Max(AppConstants.UI.MinWindowHeight, Math.Min(contentHeight, AppConstants.UI.MaxWindowHeight));
                Logger.LogTrace($"AdjustWindowSizeToContent: contentHeight={contentHeight}, finalHeight={finalHeight}");
                WindowHelper.PositionWindowBottomRight(this, AppConstants.UI.WindowWidth, finalHeight, AppConstants.UI.WindowRightMargin);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adjusting window size: {ex.Message}");
            }
        }

        private void PositionWindowAtBottomRight()
        {
            try
            {
                // AppWindow.Size returns physical pixels, but PositionWindowBottomRight
                // expects DIU and will scale internally. Convert back to DIU to avoid
                // double-scaling.
                var windowSize = this.AppWindow.Size;
                double dpiScale = WindowHelper.GetDpiScale(this);
                int heightDiu = (int)(windowSize.Height / dpiScale);
                WindowHelper.PositionWindowBottomRight(
                    this,
                    AppConstants.UI.WindowWidth,
                    heightDiu,
                    AppConstants.UI.WindowRightMargin);
            }
            catch (Exception)
            {
                // Window positioning failures are non-critical, silently ignore
            }
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
                case "Contrast":
                    monitorVm.ContrastPercent = finalValue;
                    break;
                case "Volume":
                    monitorVm.Volume = finalValue;
                    break;
            }
        }

        /// <summary>
        /// Slider KeyUp event handler - updates ViewModel when arrow keys are released
        /// This handles keyboard navigation for accessibility
        /// </summary>
        private void Slider_KeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            // Only handle arrow keys (Left, Right, Up, Down)
            if (e.Key != Windows.System.VirtualKey.Left &&
                e.Key != Windows.System.VirtualKey.Right &&
                e.Key != Windows.System.VirtualKey.Up &&
                e.Key != Windows.System.VirtualKey.Down)
            {
                return;
            }

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

            // Get the current value after key press
            int finalValue = (int)slider.Value;

            // Update the ViewModel, which will trigger hardware operation
            switch (propertyName)
            {
                case "Brightness":
                    monitorVm.Brightness = finalValue;
                    break;
                case "Contrast":
                    monitorVm.ContrastPercent = finalValue;
                    break;
                case "Volume":
                    monitorVm.Volume = finalValue;
                    break;
            }
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
        }

        /// <summary>
        /// Power state ListView selection changed handler - switches the monitor power state.
        /// Note: Selecting any state other than "On" will turn off the display.
        /// </summary>
        private async void PowerStateListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListView listView)
            {
                return;
            }

            // Get the selected power state item
            var selectedItem = listView.SelectedItem as PowerStateItem;
            if (selectedItem == null)
            {
                return;
            }

            // Skip if "On" is selected - the monitor is already on
            if (selectedItem.Value == PowerStateItem.PowerStateOn)
            {
                return;
            }

            Logger.LogInfo($"[UI] PowerStateListView_SelectionChanged: Selected {selectedItem.Name} (0x{selectedItem.Value:X2}) for monitor {selectedItem.MonitorId}");

            // Find the monitor by ID
            MonitorViewModel? monitorVm = null;
            if (!string.IsNullOrEmpty(selectedItem.MonitorId) && _viewModel != null)
            {
                monitorVm = _viewModel.Monitors.FirstOrDefault(m => m.Id == selectedItem.MonitorId);
            }

            if (monitorVm == null)
            {
                Logger.LogWarning("[UI] PowerStateListView_SelectionChanged: Could not find MonitorViewModel");
                return;
            }

            // Set the power state - this will turn off the display
            await monitorVm.SetPowerStateAsync(selectedItem.Value);
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

        /// <summary>
        /// Color temperature selection changed handler - applies the selected color temperature preset
        /// </summary>
        private async void ColorTemperatureListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListView listView)
            {
                return;
            }

            var selectedItem = listView.SelectedItem as ColorTemperatureItem;
            if (selectedItem == null)
            {
                return;
            }

            Logger.LogInfo($"[UI] ColorTemperatureListView_SelectionChanged: Selected {selectedItem.DisplayName} (0x{selectedItem.VcpValue:X2}) for monitor {selectedItem.MonitorId}");

            // Find the monitor by ID
            MonitorViewModel? monitorVm = null;
            if (!string.IsNullOrEmpty(selectedItem.MonitorId) && _viewModel != null)
            {
                monitorVm = _viewModel.Monitors.FirstOrDefault(m => m.Id == selectedItem.MonitorId);
            }

            if (monitorVm == null)
            {
                Logger.LogWarning("[UI] ColorTemperatureListView_SelectionChanged: Could not find MonitorViewModel");
                return;
            }

            // Apply the color temperature
            await monitorVm.SetColorTemperatureAsync(selectedItem.VcpValue);

            // Clear selection to allow reselecting the same preset
            listView.SelectedItem = null;
        }

        /// <summary>
        /// Flyout opened event handler - sets focus to the first focusable element inside the flyout.
        /// This enables keyboard navigation when the flyout opens.
        /// </summary>
        private void Flyout_Opened(object sender, object e)
        {
            if (sender is Flyout flyout && flyout.Content is FrameworkElement content)
            {
                // Use DispatcherQueue to ensure the flyout content is fully rendered before setting focus
                DispatcherQueue.TryEnqueue(() =>
                {
                    var firstFocusable = FocusManager.FindFirstFocusableElement(content);
                    if (firstFocusable is Control control)
                    {
                        control.Focus(FocusState.Programmatic);
                    }
                });
            }
        }

        public void Dispose()
        {
            _resizeDebounceTimer?.Stop();
            _hotkeyService?.Dispose();
            _viewModel?.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Reload hotkey settings. Call this when settings change.
        /// </summary>
        public void ReloadHotkeySettings()
        {
            _hotkeyService?.ReloadSettings();
        }
    }
}
