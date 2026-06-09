// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Awake.Core;
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
using Microsoft.UI.Xaml.Media.Imaging;
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
        // Flyout size is declared in XAML (Width/Height). We capture those values at
        // construction time so later DPI transitions don't perturb WindowEx.Width/Height
        // and our positioning math stays stable across multiple Show/Hide cycles
        // (same pattern as QuickAccess.UI MainWindow).
        private const int FlyoutRightMarginDip = 12;
        private const int FlyoutBottomMarginDip = 12;

        private readonly AwakeFlyoutViewModel _viewModel;
        private readonly int _designWidthDip;
        private readonly int _designHeightDip;
        private readonly Dictionary<AwakeMode, BitmapImage> _statusIconsByMode = CreateStatusIcons();
        private bool _isShowingWindow;
        private bool _disposed;

        public AwakeFlyoutViewModel ViewModel => _viewModel;

        public MainWindow(bool startedFromPowerToys)
        {
            try
            {
                _viewModel = new AwakeFlyoutViewModel(SettingsUtils.Default, startedFromPowerToys);

                this.InitializeComponent();

                // Snapshot the XAML-declared design size BEFORE anything else touches
                // the window — see comment above on _designWidthDip.
                _designWidthDip = (int)Math.Ceiling(this.Width);
                _designHeightDip = (int)Math.Ceiling(this.Height);

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

            ModeHeaderText.Text = Resources.AWAKE_FLYOUT_MODE_HEADER;

            ModeOffItem.Content = Resources.AWAKE_FLYOUT_MODE_OFF;
            ModeIndefiniteItem.Content = Resources.AWAKE_FLYOUT_MODE_INDEFINITE;
            ModeTimedItem.Content = Resources.AWAKE_FLYOUT_MODE_TIMED;
            ModeExpirableItem.Content = Resources.AWAKE_FLYOUT_MODE_EXPIRABLE;

            KeepDisplayOnCheckBox.Content = Resources.AWAKE_KEEP_SCREEN_ON;

            TimedHeaderText.Text = Resources.AWAKE_FLYOUT_TIMED_HEADER;
            ExpirableHeaderText.Text = Resources.AWAKE_FLYOUT_EXPIRABLE_HEADER;

            IntervalHoursInput.Header = Resources.AWAKE_FLYOUT_INTERVAL_HOURS;
            IntervalMinutesInput.Header = Resources.AWAKE_FLYOUT_INTERVAL_MINUTES;

            ExpirationTimePicker.Header = Resources.AWAKE_FLYOUT_EXPIRABLE_TIME;
            ExpirationDatePicker.Header = Resources.AWAKE_FLYOUT_EXPIRABLE_DATE;

            OpenSettingsButtonTooltip.Text = Resources.AWAKE_FLYOUT_OPEN_SETTINGS;
            AutomationProperties.SetName(OpenSettingsButton, Resources.AWAKE_FLYOUT_OPEN_SETTINGS);

            ExitButtonTooltip.Text = Resources.AWAKE_EXIT;
            AutomationProperties.SetName(ExitButton, Resources.AWAKE_EXIT);
        }

        private void ConfigureWindow()
        {
            try
            {
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

            if (_statusIconsByMode.TryGetValue(_viewModel.Mode, out BitmapImage? iconSource))
            {
                StatusIcon.Source = iconSource;
            }
        }

        private static Dictionary<AwakeMode, BitmapImage> CreateStatusIcons()
        {
            // Mirrors TrayIconService's mode → icon mapping so the flyout's status
            // glyph stays in lock-step with whatever is currently in the system tray.
            string baseDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Awake");

            return new Dictionary<AwakeMode, BitmapImage>
            {
                [AwakeMode.PASSIVE] = LoadIcon(Path.Combine(baseDir, "disabled.ico")),
                [AwakeMode.INDEFINITE] = LoadIcon(Path.Combine(baseDir, "indefinite.ico")),
                [AwakeMode.TIMED] = LoadIcon(Path.Combine(baseDir, "timed.ico")),
                [AwakeMode.EXPIRABLE] = LoadIcon(Path.Combine(baseDir, "expirable.ico")),
            };
        }

        private static BitmapImage LoadIcon(string path)
        {
            try
            {
                return new BitmapImage(new Uri(path));
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to load Awake status icon '{path}': {ex.Message}");
                return new BitmapImage();
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
                PositionFlyout();
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

        private void PositionFlyout()
        {
            try
            {
                // Use the cached XAML design size — this.Width/Height are runtime values
                // that can drift across DPI transitions; reusing them in PositionWindowBottomRight
                // would slowly walk the flyout off-screen over multiple Show/Hide cycles.
                FlyoutWindowHelper.PositionWindowBottomRight(
                    this,
                    _designWidthDip,
                    _designHeightDip,
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
