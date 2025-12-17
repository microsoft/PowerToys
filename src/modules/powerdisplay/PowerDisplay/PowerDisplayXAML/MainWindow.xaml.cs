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
using PowerDisplay.Common.Models;
using PowerDisplay.Configuration;
using PowerDisplay.Helpers;
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
        private readonly ISettingsUtils _settingsUtils = SettingsUtils.Default;
        private MainViewModel? _viewModel;

        // Expose ViewModel as property for x:Bind
        public MainViewModel ViewModel => _viewModel ?? throw new InvalidOperationException("ViewModel not initialized");

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
            Logger.LogError($"Error: {message}");
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
            // If only user operation (although we hide close button), just hide window
            args.Handled = true; // Prevent window closing
            HideWindow();
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

                // Adjust size BEFORE showing to prevent flicker
                // This measures content and positions window at correct size
                AdjustWindowSizeToContent();

                // Now show the window - it should appear at the correct size (WinUIEx simplified)
                this.Show();
                this.BringToFront();

                // Clear focus from any interactive element (e.g., Slider) to prevent
                // showing the value tooltip when the window opens
                RootGrid.Focus(FocusState.Programmatic);

                // Verify window is visible
                if (!IsWindowVisible())
                {
                    Logger.LogError("Window not visible after show attempt, forcing visibility");
                    this.Show();
                    this.BringToFront();
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
            // Hide window using WinUIEx simplified API
            this.Hide();
        }

        /// <summary>
        /// Check if window is currently visible
        /// </summary>
        /// <returns>True if window is visible, false otherwise</returns>
        public bool IsWindowVisible()
        {
            // Use WinUIEx Visible property
            return this.Visible;
        }

        /// <summary>
        /// Toggle window visibility (show if hidden, hide if visible)
        /// </summary>
        public void ToggleWindow()
        {
            try
            {
                if (IsWindowVisible())
                {
                    HideWindow();
                }
                else
                {
                    ShowWindow();
                }
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
            SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.PowerDisplay, true);
        }

        /// <summary>
        /// Configure window properties (synchronous, no data dependency)
        /// </summary>
        private void ConfigureWindow()
        {
            try
            {
                // Use WindowEx properties to configure presenter (simplified)
                this.IsResizable = false;
                this.IsMaximizable = false;
                this.IsMinimizable = false;
                this.IsTitleBarVisible = false;

                // Set minimal initial window size - will be adjusted before showing
                // Using minimal height to prevent "large window shrinking" flicker
                this.AppWindow.Resize(new SizeInt32 { Width = AppConstants.UI.WindowWidth, Height = 100 });

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

                // Use Win32 API to further disable window moving
                var hWnd = this.GetWindowHandle();
                WindowHelper.DisableWindowMovingAndResizing(hWnd);

                // Hide window from taskbar
                WindowHelper.HideFromTaskbar(hWnd);

                // Optional: set window topmost
                // WindowHelper.SetWindowTopmost(hWnd, true);
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
                MainContainer?.Measure(new Windows.Foundation.Size(AppConstants.UI.WindowWidth, double.PositiveInfinity));
                var contentHeight = (int)Math.Ceiling(MainContainer?.DesiredSize.Height ?? 0);

                // Apply max height limit and reposition (WindowEx handles DPI automatically)
                var finalHeight = Math.Min(contentHeight, AppConstants.UI.MaxWindowHeight);
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
                var windowSize = this.AppWindow.Size;
                WindowHelper.PositionWindowBottomRight(
                    this,  // MainWindow inherits from WindowEx
                    AppConstants.UI.WindowWidth,
                    windowSize.Height,
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

                // ColorTemperature case removed - now controlled via Settings UI
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
