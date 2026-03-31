// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PowerDisplay.Common.Models;
using PowerDisplay.Configuration;
using PowerDisplay.Helpers;
using PowerDisplay.Models;
using PowerDisplay.ViewModels;
using Windows.Graphics;
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
        private readonly SettingsUtils _settingsUtils = SettingsUtils.Default;
        private MainViewModel? _viewModel;
        private HotkeyService? _hotkeyService;

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
                Logger.LogTrace("MainWindow constructor: InitializeComponent completed");

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

                Logger.LogTrace("MainWindow constructor: Setting IsShownInSwitchers property");
                this.SetIsShownInSwitchers(false);
                Logger.LogTrace("MainWindow constructor: Set IsShownInSwitchers property successfully");

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
            AdjustWindowSizeToContent();
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
            if (args.WindowActivationState == WindowActivationState.Deactivated)
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

                // Adjust size BEFORE showing to prevent flicker
                // This measures content and positions window at correct size
                Logger.LogTrace("ShowWindow: Adjusting window size to content");
                AdjustWindowSizeToContent();

                // CRITICAL: WinUI3 windows must be Activated at least once to display properly.
                // In PowerToys mode, window is created but never activated until first show.
                // Without Activate(), Show() may not actually render the window on screen.
                Logger.LogTrace("ShowWindow: Calling this.Activate()");
                this.Activate();

                // Now show the window - it should appear at the correct size
                Logger.LogTrace("ShowWindow: Calling this.Show()");
                this.Show();

                // Ensure window stays on top of other windows
                this.IsAlwaysOnTop = true;
                Logger.LogTrace("ShowWindow: IsAlwaysOnTop set to true");

                // Ensure window gets keyboard focus using WinUIEx's BringToFront
                // This is necessary for Tab navigation to work without clicking first
                this.BringToFront();
                Logger.LogTrace("ShowWindow: BringToFront called");

                // Clear focus from any interactive element (e.g., Slider) to prevent
                // showing the value tooltip when the window opens
                RootGrid.Focus(FocusState.Programmatic);

                // Verify window is visible
                bool isVisible = IsWindowVisible();
                Logger.LogInfo($"ShowWindow: Window visibility after show: {isVisible}");
                if (!isVisible)
                {
                    Logger.LogError("ShowWindow: Window not visible after show attempt, forcing visibility");
                    this.Activate();
                    this.Show();
                    this.BringToFront();
                    Logger.LogInfo($"ShowWindow: After forced show, visibility: {IsWindowVisible()}");
                }
                else
                {
                    Logger.LogInfo("ShowWindow: Window shown successfully");
                }
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

            // Hide window
            this.Hide();

            Logger.LogTrace($"HideWindow: Window hidden, visibility now: {IsWindowVisible()}");
        }

        /// <summary>
        /// Check if window is currently visible
        /// </summary>
        /// <returns>True if window is visible, false otherwise</returns>
        public bool IsWindowVisible()
        {
            bool visible = this.Visible;
            Logger.LogTrace($"IsWindowVisible: Returning {visible}");
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
                // Window properties (IsResizable, IsMaximizable, IsMinimizable,
                // IsTitleBarVisible, IsShownInSwitchers) are set in XAML

                // Set a minimal initial window size in DIP - it will be adjusted before showing.
                // Using minimal height prevents the "large window shrinking" flicker.
                this.SetWindowSize(AppConstants.UI.WindowWidthDip, AppConstants.UI.WindowMinHeightDip);

                // Position window at bottom right corner
                PositionWindowAtBottomRight();

                // Set window title
                this.AppWindow.Title = "PowerDisplay";

                // Custom title bar - completely remove all buttons
                var titleBar = this.AppWindow.TitleBar;
                if (titleBar != null)
                {
                    // Extend content into title bar area
                    titleBar.ExtendsContentIntoTitleBar = true;

                    // Completely remove title bar height
                    titleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;

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

                // Use Win32 API to further disable window moving (removes WS_CAPTION, WS_SYSMENU, etc.)
                var hWnd = this.GetWindowHandle();
                WindowHelper.DisableWindowMovingAndResizing(hWnd);
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
                if (RootGrid == null)
                {
                    return;
                }

                // Force layout update and measure content height
                RootGrid.UpdateLayout();
                MainContainer?.Measure(new Windows.Foundation.Size(AppConstants.UI.WindowWidthDip, double.PositiveInfinity));
                var contentHeight = (int)Math.Ceiling(MainContainer?.DesiredSize.Height ?? 0);
                var maxHeightDip = GetAdaptiveWindowMaxHeightDip();

                // Apply min/max height limits and reposition using DIP values.
                // Min height ensures window is visible even if content hasn't loaded yet
                var finalHeightDip = Math.Max(AppConstants.UI.WindowMinHeightDip, Math.Min(contentHeight, maxHeightDip));
                Logger.LogTrace($"AdjustWindowSizeToContent: contentHeight={contentHeight}, maxHeightDip={maxHeightDip}, finalHeightDip={finalHeightDip}");
                WindowHelper.PositionWindowBottomRight(
                    this,
                    AppConstants.UI.WindowWidthDip,
                    finalHeightDip,
                    AppConstants.UI.WindowRightMarginDip,
                    AppConstants.UI.WindowBottomMarginDip);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adjusting window size: {ex.Message}");
            }
        }

        private static int GetAdaptiveWindowMaxHeightDip()
        {
            if (!WindowHelper.TryGetDisplayAreaAtCursor(out var displayArea) || displayArea is null)
            {
                return AppConstants.UI.WindowMaxHeightDip;
            }

            double dpiScale = WindowHelper.GetDpiScale(displayArea);
            int workAreaHeightDip = WindowHelper.ScaleToDip(displayArea.WorkArea.Height, dpiScale);
            int adaptiveMaxHeightDip = (int)Math.Floor(workAreaHeightDip * AppConstants.UI.WindowMaxWorkAreaHeightRatio);

            return Math.Max(
                AppConstants.UI.WindowMinHeightDip,
                Math.Min(AppConstants.UI.WindowMaxHeightDip, adaptiveMaxHeightDip));
        }

        private static double GetAdaptiveFlyoutMaxWidthDip()
        {
            if (!WindowHelper.TryGetDisplayAreaAtCursor(out var displayArea) || displayArea is null)
            {
                return AppConstants.UI.FlyoutContextMenuMaxWidthDip;
            }

            double dpiScale = WindowHelper.GetDpiScale(displayArea);
            int workAreaWidthDip = WindowHelper.ScaleToDip(displayArea.WorkArea.Width, dpiScale);
            double adaptiveMaxWidthDip = Math.Floor(workAreaWidthDip * AppConstants.UI.FlyoutContextMenuMaxWorkAreaWidthRatio);

            return Math.Max(
                AppConstants.UI.FlyoutContextMenuMaxWidthDip,
                Math.Min(AppConstants.UI.FlyoutContextMenuAdaptiveMaxWidthDip, adaptiveMaxWidthDip));
        }

        private void PositionWindowAtBottomRight()
        {
            try
            {
                var windowHeightDip = this.Height > 0
                    ? (int)Math.Ceiling(this.Height)
                    : AppConstants.UI.WindowMinHeightDip;

                WindowHelper.PositionWindowBottomRight(
                    this,  // MainWindow inherits from WindowEx
                    AppConstants.UI.WindowWidthDip,
                    windowHeightDip,
                    AppConstants.UI.WindowRightMarginDip,
                    AppConstants.UI.WindowBottomMarginDip);
            }
            catch (Exception)
            {
                // Window positioning failures are non-critical, silently ignore
            }
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
        /// Flyout opened event handler - sets focus to the first focusable element inside the flyout.
        /// This enables keyboard navigation when the flyout opens.
        /// </summary>
        private void Flyout_Opened(object sender, object e)
        {
            if (sender is Flyout flyout && flyout.Content is FrameworkElement content)
            {
                content.MaxWidth = GetAdaptiveFlyoutMaxWidthDip();

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
