// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PowerDisplay.Common.Services;
using PowerDisplay.Models;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;

namespace PowerDisplay.Helpers
{
    /// <summary>
    /// Window procedure delegate for handling window messages.
    /// Uses primitive types to avoid accessibility issues with CsWin32-generated types.
    /// </summary>
    /// <param name="hwnd">Handle to the window.</param>
    /// <param name="msg">The message.</param>
    /// <param name="wParam">Additional message information.</param>
    /// <param name="lParam">Additional message.</param>
    /// <returns>The result of the message processing.</returns>
    internal delegate nint WndProcDelegate(nint hwnd, uint msg, nuint wParam, nint lParam);

    internal sealed partial class TrayIconService
    {
        private const uint MyNotifyId = 1001;
        private const uint WmTrayIcon = PInvoke.WM_USER + 1;
        private const uint WmMouseMove = 0x0200;
        private const long BoundsCacheLifetimeMs = 1000;
        private const uint MaxSampleAgeMs = 500;
        private static readonly TimeSpan RegistrationHealthInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan ImmediateRegistrationCheck = TimeSpan.FromMilliseconds(1);

        private readonly SettingsUtils _settingsUtils;
        private readonly Action _toggleWindowAction;
        private readonly Action _exitAction;
        private readonly Action _openSettingsAction;
        private readonly uint _wmTaskbarRestart;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly WheelDeltaAccumulator _wheelDeltaAccumulator = new();
        private readonly TrayIconRegistrationBackoff _registrationBackoff = new();

        private Window? _window;
        private nint _hwnd;
        private nint _originalWndProc;
        private WndProcDelegate? _trayWndProc;
        private NOTIFYICONDATAW? _trayIconData;
        private nint _largeIcon;
        private nint _popupMenu;
        private TrayIconMouseWheelListener? _mouseWheelListener;
        private DispatcherQueueTimer? _registrationTimer;
        private MouseWheelControlMode _mouseWheelControlMode;
        private TrayIconBounds? _cachedBounds;
        private long _boundsCacheTimestamp;
        private long _hoverGeneration;
        private bool _desiredTrayIconVisible;
        private bool _isTrayIconRegistered;
        private bool _registrationFailureLogged;
        private bool _sampleDispatchFailureLogged;
        private bool _boundsFailureLogged;

        internal event Action<int>? MouseWheelScrolled;

        /// <summary>
        /// Gets or sets the UI-state gate checked before wheel deltas enter the accumulator.
        /// </summary>
        internal Func<bool>? CanProcessMouseWheel { get; set; }

        public TrayIconService(
            SettingsUtils settingsUtils,
            Action toggleWindowAction,
            Action exitAction,
            Action openSettingsAction)
        {
            _settingsUtils = settingsUtils;
            _toggleWindowAction = toggleWindowAction;
            _exitAction = exitAction;
            _openSettingsAction = openSettingsAction;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            // TaskbarCreated is the message that's broadcast when explorer.exe
            // restarts. We need to know when that happens to be able to bring our
            // notification area icon back
            _wmTaskbarRestart = RegisterWindowMessageNative("TaskbarCreated");
        }

        public void SetupTrayIcon(bool? showSystemTrayIcon = null)
        {
            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
            bool shouldShow = showSystemTrayIcon ?? settings.Properties.ShowSystemTrayIcon;
            var mouseWheelMode = settings.Properties.MouseWheelControlMode.Normalize();
            _desiredTrayIconVisible = shouldShow;
            UpdateMouseWheelMode(mouseWheelMode);

            if (!shouldShow)
            {
                Destroy();
                return;
            }

            EnsureTrayIconIdentity();
            EnsureTrayIconRegistration();
        }

        public void Destroy()
        {
            _desiredTrayIconVisible = false;
            StopRegistrationRecovery();
            DisposeMouseWheelListener();
            _mouseWheelControlMode = MouseWheelControlMode.Disabled;
            InvalidateMouseWheelHover(disarm: false);

            if (_isTrayIconRegistered && _trayIconData is not null)
            {
                var d = (NOTIFYICONDATAW)_trayIconData;
                unsafe
                {
                    Shell_NotifyIconNative((uint)NOTIFY_ICON_MESSAGE.NIM_DELETE, &d);
                }
            }

            _isTrayIconRegistered = false;
            _trayIconData = null;

            if (_popupMenu != 0)
            {
                DestroyMenu(_popupMenu);
                _popupMenu = 0;
            }

            if (_largeIcon != 0)
            {
                DestroyIcon(_largeIcon);
                _largeIcon = 0;
            }

            if (_window is not null)
            {
                _window.Close();
                _window = null;
                _hwnd = 0;
            }
        }

        private void EnsureTrayIconIdentity()
        {
            if (_window is null)
            {
                _window = new Window();
                _hwnd = WindowNative.GetWindowHandle(_window);

                // LOAD BEARING: If you don't stick the pointer to HotKeyPrc into a
                // member (and instead like, use a local), then the pointer we marshal
                // into the WindowLongPtr will be useless after we leave this function,
                // and our **WindProc will explode**.
                _trayWndProc = WindowProc;
                var hotKeyPrcPointer = Marshal.GetFunctionPointerForDelegate(_trayWndProc);
                _originalWndProc = SetWindowLongPtrNative(_hwnd, GwlWndproc, hotKeyPrcPointer);
            }

            if (_trayIconData is not null)
            {
                return;
            }

            // Keep the identity and its resources stable while Explorer registration is recovered.
            _largeIcon = GetAppIconHandle();
            unsafe
            {
                _trayIconData = new NOTIFYICONDATAW()
                {
                    cbSize = (uint)sizeof(NOTIFYICONDATAW),
                    hWnd = new HWND(_hwnd),
                    uID = MyNotifyId,
                    uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP,
                    uCallbackMessage = WmTrayIcon,
                    hIcon = new HICON(_largeIcon),
                    szTip = GetString("AppName"),
                };
            }
        }

        private void EnsureTrayIconRegistration()
        {
            if (!_desiredTrayIconVisible || _trayIconData is null || _hwnd == 0)
            {
                return;
            }

            if (IsTrayIconRegistrationHealthy())
            {
                CompleteTrayIconRegistration();
                return;
            }

            // Check again immediately before adding. Shell_NotifyIconGetRect can fail transiently
            // while the existing registration remains valid.
            if (IsTrayIconRegistrationHealthy())
            {
                CompleteTrayIconRegistration();
                return;
            }

            MarkTrayIconRegistrationStale(resetBackoff: _isTrayIconRegistered, scheduleRecovery: false);

            var data = (NOTIFYICONDATAW)_trayIconData;
            bool added;
            unsafe
            {
                added = Shell_NotifyIconNative((uint)NOTIFY_ICON_MESSAGE.NIM_ADD, &data);
            }

            if (added)
            {
                CompleteTrayIconRegistration();
                return;
            }

            DisposeMouseWheelListener();
            if (!_registrationFailureLogged)
            {
                Logger.LogWarning("[TrayIcon] Shell_NotifyIcon(NIM_ADD) failed; retrying registration");
                _registrationFailureLogged = true;
            }

            ScheduleRegistrationCheck(_registrationBackoff.NextDelay());
        }

        private void CompleteTrayIconRegistration()
        {
            _isTrayIconRegistered = true;
            _registrationBackoff.Reset();
            _registrationFailureLogged = false;
            EnsureMouseWheelListener();
            EnsureTrayIconMenu();
            ScheduleRegistrationCheck(RegistrationHealthInterval);
        }

        private void MarkTrayIconRegistrationStale(bool resetBackoff, bool scheduleRecovery)
        {
            if (_isTrayIconRegistered)
            {
                _isTrayIconRegistered = false;
                InvalidateMouseWheelHover(disarm: true);
                DisposeMouseWheelListener();
            }

            if (resetBackoff)
            {
                _registrationBackoff.Reset();
            }

            if (scheduleRecovery)
            {
                ScheduleRegistrationCheck(ImmediateRegistrationCheck);
            }
        }

        private unsafe bool IsTrayIconRegistrationHealthy()
        {
            if (_trayIconData is null || _hwnd == 0)
            {
                return false;
            }

            var identifier = new NotifyIconIdentifier
            {
                CbSize = (uint)sizeof(NotifyIconIdentifier),
                HWnd = _hwnd,
                Id = MyNotifyId,
                GuidItem = Guid.Empty,
            };

            // A successful rectangle lookup for an overflow icon also confirms a live registration.
            return ShellNotifyIconGetRectNative(ref identifier, out _) >= 0;
        }

        private void EnsureTrayIconMenu()
        {
            if (_popupMenu == 0)
            {
                _popupMenu = CreatePopupMenu();
                InsertMenuNative(_popupMenu, 0, (uint)(MENU_ITEM_FLAGS.MF_BYPOSITION | MENU_ITEM_FLAGS.MF_STRING), PInvoke.WM_USER + 1, GetString("TrayMenu_Settings"));
                InsertMenuNative(_popupMenu, 1, (uint)(MENU_ITEM_FLAGS.MF_BYPOSITION | MENU_ITEM_FLAGS.MF_STRING), PInvoke.WM_USER + 2, GetString("TrayMenu_Exit"));
            }
        }

        private void ScheduleRegistrationCheck(TimeSpan delay)
        {
            if (!_desiredTrayIconVisible)
            {
                return;
            }

            _registrationTimer ??= _dispatcherQueue.CreateTimer();
            _registrationTimer.IsRepeating = false;
            _registrationTimer.Tick -= OnRegistrationTimerTick;
            _registrationTimer.Tick += OnRegistrationTimerTick;
            _registrationTimer.Stop();
            _registrationTimer.Interval = delay;
            _registrationTimer.Start();
        }

        private void OnRegistrationTimerTick(DispatcherQueueTimer sender, object args)
        {
            sender.Stop();
            EnsureTrayIconRegistration();
        }

        private void StopRegistrationRecovery()
        {
            _registrationTimer?.Stop();
            _registrationBackoff.Reset();
            _registrationFailureLogged = false;
        }

        private void UpdateMouseWheelMode(MouseWheelControlMode mode)
        {
            mode = mode.Normalize();
            if (_mouseWheelControlMode == mode)
            {
                if (mode != MouseWheelControlMode.Disabled && _isTrayIconRegistered)
                {
                    EnsureMouseWheelListener();
                }

                return;
            }

            _mouseWheelControlMode = mode;
            InvalidateMouseWheelHover(disarm: true);

            if (mode == MouseWheelControlMode.Disabled)
            {
                DisposeMouseWheelListener();
            }
            else if (_isTrayIconRegistered)
            {
                EnsureMouseWheelListener();
            }
        }

        private void EnsureMouseWheelListener()
        {
            if (_mouseWheelControlMode == MouseWheelControlMode.Disabled)
            {
                return;
            }

            _mouseWheelListener ??= new TrayIconMouseWheelListener(
                OnWheelSampleBatch,
                OnMouseWheelListenerDisarmed);
            _mouseWheelListener.SetEnabled(true);
        }

        private void DisposeMouseWheelListener()
        {
            _mouseWheelListener?.Dispose();
            _mouseWheelListener = null;
            _cachedBounds = null;
            _wheelDeltaAccumulator.Reset();
        }

        private void InvalidateMouseWheelHover(bool disarm)
        {
            unchecked
            {
                _hoverGeneration++;
            }

            _cachedBounds = null;
            _boundsCacheTimestamp = 0;
            _wheelDeltaAccumulator.Reset();

            if (disarm)
            {
                _mouseWheelListener?.Disarm();
            }
        }

        private void HandleTrayMouseMove()
        {
            if (_mouseWheelControlMode == MouseWheelControlMode.Disabled)
            {
                return;
            }

            if (!GetCursorPos(out var cursor))
            {
                if (!_boundsFailureLogged)
                {
                    Logger.LogWarning("[TrayWheel] GetCursorPos failed while arming tray hover");
                    _boundsFailureLogged = true;
                }

                return;
            }

            var now = Environment.TickCount64;
            if (_cachedBounds is TrayIconBounds cached &&
                now - _boundsCacheTimestamp <= BoundsCacheLifetimeMs &&
                cached.Contains(cursor.X, cursor.Y))
            {
                EnsureMouseWheelListener();
                if (_mouseWheelListener?.IsArmed != true)
                {
                    unchecked
                    {
                        _hoverGeneration++;
                    }

                    _wheelDeltaAccumulator.Reset();
                }

                _mouseWheelListener?.Arm(cached, _hoverGeneration);
                return;
            }

            if (!TryGetCurrentIconBounds(out var bounds) ||
                !bounds.Contains(cursor.X, cursor.Y))
            {
                InvalidateMouseWheelHover(disarm: true);
                return;
            }

            var previousBounds = _cachedBounds;
            var startsNewHover =
                _mouseWheelListener?.IsArmed != true ||
                !previousBounds.HasValue ||
                previousBounds.Value != bounds;
            if (startsNewHover)
            {
                unchecked
                {
                    _hoverGeneration++;
                }

                _wheelDeltaAccumulator.Reset();
            }

            _cachedBounds = bounds;
            _boundsCacheTimestamp = now;
            EnsureMouseWheelListener();
            _mouseWheelListener?.Arm(bounds, _hoverGeneration);
        }

        private unsafe bool TryGetCurrentIconBounds(out TrayIconBounds bounds)
        {
            bounds = default;
            if (!_isTrayIconRegistered || _hwnd == 0 || _trayIconData is null)
            {
                return false;
            }

            var identifier = new NotifyIconIdentifier
            {
                CbSize = (uint)sizeof(NotifyIconIdentifier),
                HWnd = _hwnd,
                Id = MyNotifyId,
                GuidItem = Guid.Empty,
            };

            var result = ShellNotifyIconGetRectNative(ref identifier, out var rect);
            if (result < 0)
            {
                MarkTrayIconRegistrationStale(resetBackoff: true, scheduleRecovery: true);
                return false;
            }

            bounds = new TrayIconBounds(rect.Left, rect.Top, rect.Right, rect.Bottom);
            if (!bounds.IsValid)
            {
                if (!_boundsFailureLogged)
                {
                    Logger.LogWarning(
                        $"[TrayWheel] Shell_NotifyIconGetRect failed with HRESULT 0x{result:X8}");
                    _boundsFailureLogged = true;
                }

                return false;
            }

            _boundsFailureLogged = false;
            return true;
        }

        private void OnWheelSampleBatch(TrayWheelSample[] samples)
        {
            if (!_dispatcherQueue.TryEnqueue(() => ProcessWheelSampleBatch(samples)) &&
                !_sampleDispatchFailureLogged)
            {
                Logger.LogWarning("[TrayWheel] Failed to enqueue wheel samples to the UI thread");
                _sampleDispatchFailureLogged = true;
            }
        }

        private void ProcessWheelSampleBatch(TrayWheelSample[] samples)
        {
            _sampleDispatchFailureLogged = false;

            if (_mouseWheelControlMode == MouseWheelControlMode.Disabled)
            {
                InvalidateMouseWheelHover(disarm: true);
                return;
            }

            if (CanProcessMouseWheel?.Invoke() != true)
            {
                _wheelDeltaAccumulator.Reset();
                return;
            }

            if (!TryGetCurrentIconBounds(out var currentBounds))
            {
                InvalidateMouseWheelHover(disarm: true);
                return;
            }

            var now = unchecked((uint)Environment.TickCount);
            var hasStaleSample = false;
            var shouldDisarm = false;
            foreach (var sample in samples)
            {
                if (sample.HoverGeneration != _hoverGeneration ||
                    !currentBounds.Contains(sample.X, sample.Y))
                {
                    shouldDisarm = true;
                }

                if (unchecked(now - sample.Timestamp) > MaxSampleAgeMs)
                {
                    hasStaleSample = true;
                }
            }

            if (shouldDisarm)
            {
                InvalidateMouseWheelHover(disarm: true);
                return;
            }

            if (hasStaleSample)
            {
                _wheelDeltaAccumulator.Reset();
                return;
            }

            var totalNotches = 0;
            foreach (var sample in samples)
            {
                totalNotches += _wheelDeltaAccumulator.Add(sample.Delta);
            }

            _cachedBounds = currentBounds;
            _boundsCacheTimestamp = Environment.TickCount64;

            if (totalNotches != 0)
            {
                MouseWheelScrolled?.Invoke(totalNotches);
            }
        }

        private void OnMouseWheelListenerDisarmed(long generation)
        {
            if (!_dispatcherQueue.TryEnqueue(() =>
            {
                if (generation == _hoverGeneration)
                {
                    InvalidateMouseWheelHover(disarm: false);
                }
            }) &&
                !_sampleDispatchFailureLogged)
            {
                Logger.LogWarning("[TrayWheel] Failed to enqueue hover cleanup to the UI thread");
                _sampleDispatchFailureLogged = true;
            }
        }

        private static string GetString(string key)
        {
            try
            {
                return ResourceLoaderInstance.ResourceLoader.GetString(key);
            }
            catch
            {
                return "unknown";
            }
        }

        private nint GetAppIconHandle()
        {
            var exePath = Path.Combine(AppContext.BaseDirectory, "PowerToys.PowerDisplay.exe");
            ExtractIconExNative(exePath, 0, out var largeIcon, out var smallIcon, 1);
            if (smallIcon != 0)
            {
                DestroyIcon(smallIcon);
            }

            return largeIcon;
        }

        private nint WindowProc(
            nint hwnd,
            uint uMsg,
            nuint wParam,
            nint lParam)
        {
            switch (uMsg)
            {
                case PInvoke.WM_COMMAND:
                    {
                        if (wParam == PInvoke.WM_USER + 1)
                        {
                            // Settings menu item
                            _openSettingsAction?.Invoke();
                        }
                        else if (wParam == PInvoke.WM_USER + 2)
                        {
                            // Exit menu item
                            if (!_dispatcherQueue.TryEnqueue(() => _exitAction()))
                            {
                                Logger.LogWarning("[TrayIcon] Failed to enqueue the exit action");
                                _exitAction();
                            }
                        }
                    }

                    break;

                case PInvoke.WM_WINDOWPOSCHANGING:
                    {
                        if (_desiredTrayIconVisible && !_isTrayIconRegistered)
                        {
                            ScheduleRegistrationCheck(ImmediateRegistrationCheck);
                        }
                    }

                    break;
                default:
                    // _wmTaskbarRestart isn't a compile-time constant, so we can't
                    // use it in a case label
                    if (uMsg == _wmTaskbarRestart)
                    {
                        MarkTrayIconRegistrationStale(resetBackoff: true, scheduleRecovery: true);
                    }
                    else if (uMsg == WmTrayIcon && (uint)wParam == MyNotifyId)
                    {
                        switch ((uint)lParam)
                        {
                            case WmMouseMove:
                                HandleTrayMouseMove();
                                break;
                            case PInvoke.WM_RBUTTONUP:
                                {
                                    if (_popupMenu != 0)
                                    {
                                        GetCursorPos(out var cursorPos);
                                        SetForegroundWindow(_hwnd);
                                        TrackPopupMenuExNative(_popupMenu, (uint)TRACK_POPUP_MENU_FLAGS.TPM_LEFTALIGN | (uint)TRACK_POPUP_MENU_FLAGS.TPM_BOTTOMALIGN, cursorPos.X, cursorPos.Y, _hwnd, 0);
                                    }
                                }

                                break;
                            case PInvoke.WM_LBUTTONUP:
                                _toggleWindowAction?.Invoke();
                                break;
                        }
                    }

                    break;
            }

            return CallWindowProcIntPtr(_originalWndProc, hwnd, uMsg, wParam, lParam);
        }

        [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW")]
        private static partial nint CallWindowProcIntPtr(IntPtr lpPrevWndFunc, nint hWnd, uint msg, nuint wParam, nint lParam);

        [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
        private static partial uint RegisterWindowMessageNative(string lpString);

        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static partial nint SetWindowLongPtrNative(nint hWnd, int nIndex, nint dwNewLong);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetCursorPos(out POINT lpPoint);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetForegroundWindow(nint hWnd);

        // Shell APIs - use uint for enums and unsafe pointer for struct
        [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static unsafe partial bool Shell_NotifyIconNative(uint dwMessage, NOTIFYICONDATAW* lpData);

        [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconGetRect")]
        private static partial int ShellNotifyIconGetRectNative(
            ref NotifyIconIdentifier identifier,
            out NativeRect iconLocation);

        [LibraryImport("shell32.dll", EntryPoint = "ExtractIconExW", StringMarshalling = StringMarshalling.Utf16)]
        private static partial uint ExtractIconExNative(string lpszFile, int nIconIndex, out nint phiconLarge, out nint phiconSmall, uint nIcons);

        // Menu APIs
        [LibraryImport("user32.dll")]
        private static partial nint CreatePopupMenu();

        [LibraryImport("user32.dll", EntryPoint = "InsertMenuW", StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool InsertMenuNative(nint hMenu, uint uPosition, uint uFlags, nuint uIDNewItem, string? lpNewItem);

        [LibraryImport("user32.dll", EntryPoint = "TrackPopupMenuEx")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool TrackPopupMenuExNative(nint hMenu, uint uFlags, int x, int y, nint hwnd, nint lptpm);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DestroyMenu(nint hMenu);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DestroyIcon(nint hIcon);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NotifyIconIdentifier
        {
            public uint CbSize;
            public nint HWnd;
            public uint Id;
            public Guid GuidItem;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const int GwlWndproc = -4;
    }
}
