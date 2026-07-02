// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Awake.Properties;
using Awake.ViewModels;
using ManagedCommon;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinUIEx;

namespace Awake
{
    /// <summary>
    /// The Awake tray flyout window. Hidden at startup; shown when the user clicks the tray icon.
    /// Auto-hides when it loses activation (same behavior as the PowerDisplay flyout).
    /// The flyout body lives in <see cref="AwakeShellPage"/> (a navigation frame whose root
    /// is <see cref="AwakeLaunchPage"/>); this window owns the
    /// window lifecycle, positioning, and the countdown timer.
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

        // Delay the working-set trim after hide so quick toggles don't trigger aggressive
        // GC; cancel it on re-show. The trim only releases idle UI/heap pages back to the OS —
        // it has no effect on the keep-awake state (driven by SetThreadExecutionState in Manager).
        private const int MemoryTrimDelayMs = 2000;

        private readonly AwakeFlyoutViewModel _viewModel;
        private readonly int _designWidthDip;
        private readonly int _designHeightDip;
        private readonly DispatcherTimer _countdownTimer;
        private CancellationTokenSource? _trimCts;
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

                ShellHost.Initialize(_viewModel);
                ShellHost.CloseRequested += OnFlyoutCloseRequested;

                // The window title isn't a XAML element, so it can't use x:Uid; set it here.
                // All other UI strings are localized via x:Uid against Strings\<lang>\Resources.resw.
                this.AppWindow.Title = Resources.AWAKE_FLYOUT_TITLE;

                ConfigureWindow();
                RegisterEventHandlers();

                _countdownTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1),
                };
                _countdownTimer.Tick += OnCountdownTick;

                this.SetIsShownInSwitchers(false);

                // Window starts hidden at launch; trim the initial working set so the idle
                // background footprint drops without waiting for a first show/hide cycle.
                ScheduleMemoryTrim();
            }
            catch (Exception ex)
            {
                Logger.LogError($"MainWindow constructor failed: {ex}");
                throw;
            }
        }

        private void OnCountdownTick(object? sender, object e)
        {
            _viewModel.UpdateCountdown();
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
        }

        private void OnFlyoutCloseRequested(object? sender, EventArgs e)
        {
            HideWindow();
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

        public void ShowWindow()
        {
            _isShowingWindow = true;
            try
            {
                CancelMemoryTrim();
                _viewModel.Refresh();
                ShellHost.NavigateToLaunch();
                ShellHost.RefreshGlow();
                PositionFlyout();
                this.Activate();
                this.Show();
                this.IsAlwaysOnTop = true;
                this.BringToFront();
                ShellHost.FocusContent();
                _countdownTimer.Start();
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
                _countdownTimer.Stop();
                this.Hide();
                ScheduleMemoryTrim();
            }
            catch (Exception ex)
            {
                Logger.LogError($"HideWindow failed: {ex}");
            }
        }

        // Releases idle pages back to the OS ~2s after the flyout is hidden so the background
        // working set drops. Cancelled on re-show to avoid GC churn during quick toggles.
        private void ScheduleMemoryTrim()
        {
            CancelMemoryTrim();
            _trimCts = new CancellationTokenSource();
            var token = _trimCts.Token;

            Task.Delay(MemoryTrimDelayMs, token).ContinueWith(
                _ =>
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, -1, -1);
                },
                token,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);
        }

        private void CancelMemoryTrim()
        {
            _trimCts?.Cancel();
            _trimCts?.Dispose();
            _trimCts = null;
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
            CancelMemoryTrim();
            _countdownTimer.Stop();
            _countdownTimer.Tick -= OnCountdownTick;
            ShellHost.CloseRequested -= OnFlyoutCloseRequested;
            _viewModel.Dispose();
        }

        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, int dwMinimumWorkingSetSize, int dwMaximumWorkingSetSize);
    }
}
