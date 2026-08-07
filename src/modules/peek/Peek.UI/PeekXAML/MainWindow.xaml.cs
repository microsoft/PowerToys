// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Peek.Common.Constants;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers;
using Peek.UI.Extensions;
using Peek.UI.Helpers;
using Peek.UI.Models;
using Peek.UI.Native;
using Peek.UI.Telemetry.Events;
using Windows.Foundation;
using WinUIEx;

namespace Peek.UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : WindowEx, IDisposable
    {
        public MainWindowViewModel ViewModel { get; }

        private readonly ThemeListener? themeListener;
        private readonly IUserSettings userSettings;

        /// <summary>
        /// Whether the delete confirmation dialog is currently open. Used to ensure only one
        /// dialog is open at a time.
        /// </summary>
        private bool _isDeleteInProgress;
        private bool _exitAfterClose;

        private IntPtr _keyboardHookHandle;
        private NativeMethods.LowLevelKeyboardProc? _keyboardHookProc;
        private Windows.Win32.Foundation.HWND _cachedWindowHandle;

        public MainWindow()
        {
            InitializeComponent();
            this.Activated += PeekWindow_Activated;

            try
            {
                themeListener = new ThemeListener();
                themeListener.ThemeChanged += (_) => HandleThemeChange();
            }
            catch (Exception e)
            {
                Logger.LogError($"HandleThemeChange exception. Please install .NET 4.", e);
            }

            ViewModel = Application.Current.GetService<MainWindowViewModel>();

            TitleBarControl.SetTitleBarToWindow(this);
            ExtendsContentIntoTitleBar = true;
            WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(this.GetWindowHandle());
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            AppWindow.SetIcon("Assets/Peek/Icon.ico");

            AppWindow.Closing += AppWindow_Closing;

            userSettings = Application.Current.GetService<IUserSettings>();
            userSettings.Changed += UpdateWindowBySettings;
            UpdateWindowBySettings(null, EventArgs.Empty);
        }

        private void UpdateWindowBySettings(object? sender, EventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                IsAlwaysOnTop = userSettings.AlwaysOnTop;
                IsShownInSwitchers = userSettings.ShowTaskbarIcon;
            });
        }

        private async void Content_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Delete)
            {
                e.Handled = true;
                await DeleteItem();
            }
        }

        private async Task DeleteItem()
        {
            if (ViewModel.CurrentItem == null || _isDeleteInProgress)
            {
                return;
            }

            try
            {
                _isDeleteInProgress = true;

                if (userSettings.ConfirmFileDelete)
                {
                    if (await ShowDeleteConfirmationDialogAsync() == ContentDialogResult.Primary)
                    {
                        // Delete after asking for confirmation. Persist the "Don't warn again" choice if set.
                        ViewModel.DeleteItem(DeleteDontWarnCheckbox.IsChecked, this.GetWindowHandle());
                    }
                }
                else
                {
                    // Delete without confirmation.
                    ViewModel.DeleteItem(true, this.GetWindowHandle());
                }
            }
            finally
            {
                _isDeleteInProgress = false;
            }
        }

        private async Task<ContentDialogResult> ShowDeleteConfirmationDialogAsync()
        {
            DeleteDontWarnCheckbox.IsChecked = false;
            DeleteConfirmationDialog.XamlRoot = Content.XamlRoot;

            return await DeleteConfirmationDialog.ShowAsync();
        }

        /// <summary>
        /// Toggling the window visibility and querying files when necessary.
        /// </summary>
        public void Toggle(bool firstActivation, SelectedItem selectedItem, bool exitAfterClose)
        {
            if (exitAfterClose)
            {
                _exitAfterClose = true;
            }

            if (firstActivation)
            {
                Activate();
                Initialize(selectedItem);
                return;
            }

            if (DeleteConfirmationDialog.Visibility == Visibility.Visible)
            {
                DeleteConfirmationDialog.Hide();
            }

            if (AppWindow.IsVisible)
            {
                if (IsNewSingleSelectedItem(selectedItem))
                {
                    Initialize(selectedItem);
                    Activate(); // Brings existing window into focus in case it was previously minimized
                }
                else
                {
                    Uninitialize();
                }
            }
            else
            {
                Initialize(selectedItem);
            }
        }

        private void HandleThemeChange()
        {
            AppWindow appWindow = this.AppWindow;

            appWindow.TitleBar.ButtonForegroundColor = ThemeHelpers.GetAppTheme() == AppTheme.Light ? Colors.DarkSlateGray : Colors.White;
        }

        private void PeekWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                var userSettings = Application.Current.GetService<IUserSettings>();
                if (userSettings.CloseAfterLosingFocus)
                {
                    Uninitialize();
                }
            }
        }

        private void PreviousNavigationInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            ViewModel.AttemptPreviousNavigation();
        }

        private void NextNavigationInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            ViewModel.AttemptNextNavigation();
        }

        private void CloseInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            Uninitialize();
        }

        private void Initialize(SelectedItem selectedItem)
        {
            var bootTime = new System.Diagnostics.Stopwatch();
            bootTime.Start();

            FilePreviewer.ShowFilePreviewTooltip = Application.Current.GetService<IUserSettings>().ShowFilePreviewTooltip;

            ViewModel.Initialize(selectedItem);

            // If no files were found (e.g., user is typing in rename/search box, or in virtual folders),
            // don't show anything - just return silently to avoid stealing focus
            if (ViewModel.CurrentItem == null)
            {
                return;
            }

            ViewModel.ScalingFactor = this.GetMonitorScale();
            this.Content.KeyUp -= Content_KeyUp;
            this.Content.KeyUp += Content_KeyUp;

            _cachedWindowHandle = new Windows.Win32.Foundation.HWND(this.GetWindowHandle());
            InstallKeyboardHook();

            bootTime.Stop();

            PowerToysTelemetry.Log.WriteEvent(new OpenedEvent() { FileExtension = ViewModel.CurrentItem?.Extension ?? string.Empty, HotKeyToVisibleTimeMs = bootTime.ElapsedMilliseconds });
        }

        private void Uninitialize()
        {
            try
            {
                // Keep teardown best-effort: one failure must not skip later cleanup
                // or prevent the CLI/-FilePath exit-after-close contract.
                TryRunUninitializeStep(UninstallKeyboardHook, nameof(UninstallKeyboardHook));
                TryRunUninitializeStep(this.Restore, "Restore");
                TryRunUninitializeStep(() => this.Hide(), "Hide");
                TryRunUninitializeStep(ViewModel.Uninitialize, nameof(ViewModel.Uninitialize));
                TryRunUninitializeStep(() => ViewModel.ScalingFactor = 1, nameof(ViewModel.ScalingFactor));
                TryRunUninitializeStep(() => this.Content.KeyUp -= Content_KeyUp, nameof(Content_KeyUp));
                TryRunUninitializeStep(ShellPreviewHandlerPreviewer.ReleaseHandlerFactories, nameof(ShellPreviewHandlerPreviewer.ReleaseHandlerFactories));
            }
            finally
            {
                if (_exitAfterClose)
                {
                    Environment.Exit(0);
                }
            }
        }

        private static void TryRunUninitializeStep(Action action, string stepName)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Unhandled exception in Peek MainWindow.Uninitialize step '{stepName}'; continuing cleanup.", ex);
            }
        }

        /// <summary>
        /// Handle FilePreviewerSizeChanged event to adjust window size and position accordingly.
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">PreviewSizeChangedArgs</param>
        private void FilePreviewer_PreviewSizeChanged(object sender, PreviewSizeChangedArgs e)
        {
            var foregroundWindowHandle = Windows.Win32.PInvoke_PeekUI.GetForegroundWindow();

            var monitorSize = foregroundWindowHandle.GetMonitorSize();
            var monitorScale = foregroundWindowHandle.GetMonitorScale();

            // If no size is requested, try to fit to the monitor size.
            Size requestedSize = e.PreviewSize.MonitorSize ?? monitorSize;
            var contentScale = e.PreviewSize.UseEffectivePixels ? 1 : monitorScale;
            Size scaledRequestedSize = new(requestedSize.Width / contentScale, requestedSize.Height / contentScale);

            // TODO: Investigate why portrait images do not perfectly fit edge-to-edge --> WindowHeightContentPadding can be 0 (or close to that) if custom? [Jay]
            Size monitorMinContentSize = GetMonitorMinContentSize(monitorScale);
            Size monitorMaxContentSize = GetMonitorMaxContentSize(monitorSize, monitorScale);
            Size adjustedContentSize = scaledRequestedSize.Fit(monitorMaxContentSize, monitorMinContentSize);

            var titleBarHeight = TitleBarControl.ActualHeight;
            var desiredWindowWidth = adjustedContentSize.Width;
            var desiredWindowHeight = adjustedContentSize.Height + titleBarHeight;

            if (!TitleBarControl.Pinned)
            {
                this.CenterOnMonitor(foregroundWindowHandle, desiredWindowWidth, desiredWindowHeight);
            }

            this.Show();
            WindowHelpers.BringToForeground(this.GetWindowHandle());
        }

        private Size GetMonitorMaxContentSize(Size monitorSize, double scaling)
        {
            var titleBarHeight = TitleBarControl.ActualHeight;
            var maxContentWidth = monitorSize.Width * WindowConstants.MaxWindowToMonitorRatio;
            var maxContentHeight = (monitorSize.Height - titleBarHeight) * WindowConstants.MaxWindowToMonitorRatio;
            return new Size(maxContentWidth / scaling, maxContentHeight / scaling);
        }

        private Size GetMonitorMinContentSize(double scaling)
        {
            var titleBarHeight = TitleBarControl.ActualHeight;
            var minContentWidth = WindowConstants.MinWindowWidth;
            var minContentHeight = WindowConstants.MinWindowHeight - titleBarHeight;
            return new Size(minContentWidth / scaling, minContentHeight / scaling);
        }

        /// <summary>
        /// Handle AppWindow closing to prevent app termination on close.
        /// </summary>
        /// <param name="sender">AppWindow</param>
        /// <param name="args">AppWindowClosingEventArgs</param>
        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            // Any exception that escapes a WinRT event handler is projected back to
            // the CsWinRT dispatcher as a failed HRESULT, and CFlat fail-fasts the
            // process. We want a Closing handler that can never crash Peek, even if
            // a callee (e.g., a cached preview-handler RCW that has been separated
            // during teardown) throws InvalidComObjectException.
            try
            {
                args.Cancel = true;
                PowerToysTelemetry.Log.WriteEvent(new ClosedEvent());
                Uninitialize();
            }
            catch (Exception ex)
            {
                Logger.LogError("Unhandled exception in Peek MainWindow.AppWindow_Closing; suppressing to avoid fail-fast.", ex);
                args.Cancel = true;
            }
        }

        private bool IsNewSingleSelectedItem(SelectedItem selectedItem)
        {
            try
            {
                return selectedItem.Matches(ViewModel.CurrentItem?.Path);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }

            return false;
        }

        private void InstallKeyboardHook()
        {
            if (_keyboardHookHandle != IntPtr.Zero)
            {
                return;
            }

            _keyboardHookProc = LowLevelKeyboardHookCallback;
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            var moduleHandle = NativeMethods.GetModuleHandle(process.MainModule?.ModuleName);

            _keyboardHookHandle = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_KEYBOARD_LL,
                _keyboardHookProc!,
                moduleHandle,
                0);

            if (_keyboardHookHandle == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                _keyboardHookProc = null;
                Logger.LogError($"Failed to install keyboard hook for Peek window. Win32 error: {error}");
            }
        }

        private void UninstallKeyboardHook()
        {
            if (_keyboardHookHandle != IntPtr.Zero)
            {
                if (NativeMethods.UnhookWindowsHookEx(_keyboardHookHandle))
                {
                    _keyboardHookHandle = IntPtr.Zero;
                    _keyboardHookProc = null;
                }
                else
                {
                    var error = Marshal.GetLastWin32Error();
                    Logger.LogError($"Failed to uninstall keyboard hook for Peek window. Win32 error: {error}");
                }
            }
        }

        private IntPtr LowLevelKeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            const int WM_KEYDOWN = 0x0100;
            const uint VK_W = 0x57;
            const uint VK_ESCAPE = 0x1B;
            const uint VK_LEFT = 0x25;
            const uint VK_UP = 0x26;
            const uint VK_RIGHT = 0x27;
            const uint VK_DOWN = 0x28;
            const int VK_CONTROL = 0x11;
            const int VK_ALT = 0x12;
            const int VK_SHIFT = 0x10;
            const int VK_LWIN = 0x5B;
            const int VK_RWIN = 0x5C;
            const int KEY_PRESSED_MASK = 0x8000;

            try
            {
                if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN && lParam != IntPtr.Zero)
                {
                    // Only handle when our window is in the foreground (cheap check before marshaling)
                    var foreground = Windows.Win32.PInvoke_PeekUI.GetForegroundWindow();
                    if (foreground != _cachedWindowHandle)
                    {
                        return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
                    }

                    var hookStruct = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

                    // Fast-path: skip keys we never handle
                    if (hookStruct.vkCode != VK_W && hookStruct.vkCode != VK_ESCAPE &&
                        hookStruct.vkCode != VK_LEFT && hookStruct.vkCode != VK_RIGHT &&
                        hookStruct.vkCode != VK_UP && hookStruct.vkCode != VK_DOWN)
                    {
                        return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
                    }

                    bool ctrlPressed = (NativeMethods.GetAsyncKeyState(VK_CONTROL) & KEY_PRESSED_MASK) != 0;
                    bool altPressed = (NativeMethods.GetAsyncKeyState(VK_ALT) & KEY_PRESSED_MASK) != 0;
                    bool shiftPressed = (NativeMethods.GetAsyncKeyState(VK_SHIFT) & KEY_PRESSED_MASK) != 0;
                    bool winPressed = (NativeMethods.GetAsyncKeyState(VK_LWIN) & KEY_PRESSED_MASK) != 0 ||
                                      (NativeMethods.GetAsyncKeyState(VK_RWIN) & KEY_PRESSED_MASK) != 0;
                    bool handled = false;

                    // Pass keys through while delete confirmation dialog is showing
                    if (_isDeleteInProgress)
                    {
                        return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
                    }

                    if (ctrlPressed && !altPressed && !shiftPressed && !winPressed && hookStruct.vkCode == VK_W)
                    {
                        handled = DispatcherQueue.TryEnqueue(() =>
                        {
                            if (!_isDeleteInProgress)
                            {
                                Uninitialize();
                            }
                        });
                    }
                    else if (!ctrlPressed && !altPressed && !shiftPressed && !winPressed && hookStruct.vkCode == VK_ESCAPE)
                    {
                        handled = DispatcherQueue.TryEnqueue(() =>
                        {
                            if (!_isDeleteInProgress)
                            {
                                Uninitialize();
                            }
                        });
                    }
                    else if (!ctrlPressed && !altPressed && !shiftPressed && !winPressed && hookStruct.vkCode == VK_LEFT)
                    {
                        if (ViewModel.DisplayItemCount > 1)
                        {
                            handled = DispatcherQueue.TryEnqueue(() => ViewModel.AttemptPreviousNavigation());
                        }
                    }
                    else if (!ctrlPressed && !altPressed && !shiftPressed && !winPressed && hookStruct.vkCode == VK_RIGHT)
                    {
                        if (ViewModel.DisplayItemCount > 1)
                        {
                            handled = DispatcherQueue.TryEnqueue(() => ViewModel.AttemptNextNavigation());
                        }
                    }
                    else if (!ctrlPressed && !altPressed && !shiftPressed && !winPressed && hookStruct.vkCode == VK_UP)
                    {
                        if (ViewModel.DisplayItemCount > 1)
                        {
                            handled = DispatcherQueue.TryEnqueue(() => ViewModel.AttemptPreviousNavigation());
                        }
                    }
                    else if (!ctrlPressed && !altPressed && !shiftPressed && !winPressed && hookStruct.vkCode == VK_DOWN)
                    {
                        if (ViewModel.DisplayItemCount > 1)
                        {
                            handled = DispatcherQueue.TryEnqueue(() => ViewModel.AttemptNextNavigation());
                        }
                    }

                    if (handled)
                    {
                        return (IntPtr)1;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Unhandled exception in Peek keyboard hook.", ex);
            }

            return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            UninstallKeyboardHook();
            themeListener?.Dispose();
            userSettings.Changed -= UpdateWindowBySettings;
        }

        /// <summary>
        /// Returns Visibility.Collapsed when error is showing, Visibility.Visible when not.
        /// </summary>
        public Visibility ContentVisibility(bool isErrorVisible)
        {
            return isErrorVisible ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>
        /// Handle InfoBar closed - if there's no current item, close the window.
        /// </summary>
        private void ErrorInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args)
        {
            if (ViewModel.CurrentItem == null)
            {
                Uninitialize();
            }
        }
    }
}
