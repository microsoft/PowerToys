// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using Awake.Properties;
using Awake.ViewModels;
using ManagedCommon;
using Microsoft.PowerToys.Common.UI.Controls.Flyout;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WinUIEx;

namespace Awake
{
    /// <summary>
    /// The Awake tray flyout. Hidden at startup; shown when the user clicks the tray icon.
    /// Auto-hides when it loses activation (same behavior as the PowerDisplay flyout).
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicMethods)]
    public sealed partial class MainWindow : WindowEx, IDisposable
    {
        // 320 dip wide flyout matches PowerDisplay; tall enough to host the expirable section.
        private const int FlyoutWidthDip = 320;
        private const int FlyoutMinHeightDip = 200;
        private const int FlyoutMaxHeightDip = 520;
        private const int FlyoutRightMarginDip = 12;
        private const int FlyoutBottomMarginDip = 12;

        private readonly AwakeFlyoutViewModel _viewModel;
        private bool _isShowingWindow;
        private bool _disposed;

        public AwakeFlyoutViewModel ViewModel => _viewModel;

        public MainWindow(bool startedFromPowerToys)
        {
            try
            {
                _viewModel = new AwakeFlyoutViewModel(SettingsUtils.Default, startedFromPowerToys);

                this.InitializeComponent();

                ApplyLocalizedStrings();

                ConfigureWindow();
                RegisterEventHandlers();
                SyncModeSelection();

                this.SetIsShownInSwitchers(false);
            }
            catch (Exception ex)
            {
                Logger.LogError($"MainWindow constructor failed: {ex}");
                throw;
            }
        }

        private void ApplyLocalizedStrings()
        {
            this.AppWindow.Title = Resources.AWAKE_FLYOUT_TITLE;

            TitleText.Text = Resources.AWAKE_FLYOUT_TITLE;
            ModeHeaderText.Text = Resources.AWAKE_FLYOUT_MODE_HEADER;

            ModeOffItem.Content = Resources.AWAKE_FLYOUT_MODE_OFF;
            ModeIndefiniteItem.Content = Resources.AWAKE_FLYOUT_MODE_INDEFINITE;
            ModeTimedItem.Content = Resources.AWAKE_FLYOUT_MODE_TIMED;
            ModeExpirableItem.Content = Resources.AWAKE_FLYOUT_MODE_EXPIRABLE;

            KeepDisplayOnToggle.Header = Resources.AWAKE_KEEP_SCREEN_ON;

            TimedHeaderText.Text = Resources.AWAKE_FLYOUT_TIMED_HEADER;
            ExpirableHeaderText.Text = Resources.AWAKE_FLYOUT_EXPIRABLE_HEADER;

            ExpirationDatePicker.PlaceholderText = Resources.AWAKE_FLYOUT_EXPIRABLE_DATE;
            ExpirationTimePicker.Header = Resources.AWAKE_FLYOUT_EXPIRABLE_TIME;
            ExpirationDatePicker.Header = Resources.AWAKE_FLYOUT_EXPIRABLE_DATE;

            ApplyExpirableButton.Content = Resources.AWAKE_FLYOUT_EXPIRABLE_APPLY;
            EditPresetsLink.Content = Resources.AWAKE_FLYOUT_EDIT_PRESETS;

            OpenSettingsButtonTooltip.Text = Resources.AWAKE_FLYOUT_OPEN_SETTINGS;
            AutomationProperties.SetName(OpenSettingsButton, Resources.AWAKE_FLYOUT_OPEN_SETTINGS);

            ExitButtonTooltip.Text = Resources.AWAKE_EXIT;
            AutomationProperties.SetName(ExitButton, Resources.AWAKE_EXIT);
        }

        private void ConfigureWindow()
        {
            try
            {
                this.SetWindowSize(FlyoutWidthDip, FlyoutMinHeightDip);
                PositionFlyout();

                var titleBar = this.AppWindow.TitleBar;
                if (titleBar != null)
                {
                    titleBar.ExtendsContentIntoTitleBar = true;
                    titleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
                    titleBar.SetDragRectangles(Array.Empty<Windows.Graphics.RectInt32>());
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"ConfigureWindow: {ex.Message}");
            }
        }

        private void RegisterEventHandlers()
        {
            this.Closed += OnWindowClosed;
            this.Activated += OnWindowActivated;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AwakeFlyoutViewModel.Mode))
            {
                SyncModeSelection();
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, AdjustWindowSizeToContent);
            }
            else if (e.PropertyName == nameof(AwakeFlyoutViewModel.TimedSectionVisibility)
                  || e.PropertyName == nameof(AwakeFlyoutViewModel.ExpirableSectionVisibility))
            {
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, AdjustWindowSizeToContent);
            }
        }

        private void SyncModeSelection()
        {
            int target = _viewModel.Mode switch
            {
                AwakeMode.INDEFINITE => 1,
                AwakeMode.TIMED => 2,
                AwakeMode.EXPIRABLE => 3,
                _ => 0,
            };

            if (ModeComboBox.SelectedIndex != target)
            {
                ModeComboBox.SelectedIndex = target;
            }
        }

        private void ModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // ComboBox can fire SelectionChanged transiently with SelectedIndex == -1
            // during template apply; ignore those intermediate states.
            if (ModeComboBox.SelectedIndex < 0)
            {
                return;
            }

            var newMode = ModeComboBox.SelectedIndex switch
            {
                1 => AwakeMode.INDEFINITE,
                2 => AwakeMode.TIMED,
                3 => AwakeMode.EXPIRABLE,
                _ => AwakeMode.PASSIVE,
            };

            if (_viewModel.Mode != newMode)
            {
                _viewModel.Mode = newMode;
            }
        }

        private void TimedPresetRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton { Tag: TimedPreset preset })
            {
                _viewModel.SelectTimedPresetCommand.Execute(preset);
            }
        }

        private void OnApplyExpirableClick(object sender, RoutedEventArgs e)
        {
            _viewModel.ApplyExpirableCommand.Execute(null);
        }

        private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
        {
            _viewModel.OpenSettingsCommand.Execute(null);
            HideWindow();
        }

        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            _viewModel.ExitAwakeCommand.Execute(null);
        }

        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated && !_isShowingWindow)
            {
                HideWindow();
            }
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            args.Handled = true;
            HideWindow();
        }

        private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                HideWindow();
                e.Handled = true;
            }
        }

        public void ShowWindow()
        {
            _isShowingWindow = true;
            try
            {
                _viewModel.Refresh();
                AdjustWindowSizeToContent();
                this.Activate();
                this.Show();
                this.IsAlwaysOnTop = true;
                this.BringToFront();
                RootGrid.Focus(FocusState.Programmatic);
            }
            catch (Exception ex)
            {
                Logger.LogError($"ShowWindow failed: {ex}");
            }
            finally
            {
                _isShowingWindow = false;
            }
        }

        public void HideWindow()
        {
            try
            {
                this.Hide();
            }
            catch (Exception ex)
            {
                Logger.LogError($"HideWindow failed: {ex}");
            }
        }

        public void ToggleWindow()
        {
            if (this.Visible)
            {
                HideWindow();
            }
            else
            {
                ShowWindow();
            }
        }

        private void AdjustWindowSizeToContent()
        {
            try
            {
                if (RootGrid is null)
                {
                    return;
                }

                RootGrid.UpdateLayout();
                MainContainer.Measure(new Windows.Foundation.Size(FlyoutWidthDip, double.PositiveInfinity));
                var contentHeight = (int)Math.Ceiling(MainContainer.DesiredSize.Height);
                var finalHeightDip = Math.Clamp(contentHeight, FlyoutMinHeightDip, FlyoutMaxHeightDip);

                FlyoutWindowHelper.PositionWindowBottomRight(
                    this,
                    FlyoutWidthDip,
                    finalHeightDip,
                    FlyoutRightMarginDip,
                    FlyoutBottomMarginDip);
            }
            catch (Exception ex)
            {
                Logger.LogError($"AdjustWindowSizeToContent failed: {ex}");
            }
        }

        private void PositionFlyout()
        {
            try
            {
                FlyoutWindowHelper.PositionWindowBottomRight(
                    this,
                    FlyoutWidthDip,
                    FlyoutMinHeightDip,
                    FlyoutRightMarginDip,
                    FlyoutBottomMarginDip);
            }
            catch
            {
                // Non-critical: window positioning failures fall back to OS-default placement.
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Dispose();
        }
    }
}
