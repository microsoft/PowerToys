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

        // The Shell repeats WM_MOUSEMOVE for every pixel of travel across the icon. Serving a
        // still-fresh rectangle from this cache keeps the hover path off Shell_NotifyIconGetRect.
        private const long BoundsCacheLifetimeMs = 1000;

        // Input that queued up behind a stalled UI thread should not move brightness late.
        private const uint MaxSampleAgeMs = 500;

        private readonly SettingsUtils _settingsUtils;
        private readonly Action _toggleWindowAction;
        private readonly Action _exitAction;
        private readonly Action _openSettingsAction;
        private readonly uint _wmTaskbarRestart;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly WheelDeltaAccumulator _wheelDeltaAccumulator = new();

        private Window? _window;
        private nint _hwnd;
        private nint _originalWndProc;
        private WndProcDelegate? _trayWndProc;
        private NOTIFYICONDATAW? _trayIconData;
        private nint _largeIcon;
        private nint _popupMenu;
        private TrayIconMouseWheelListener? _mouseWheelListener;
        private MouseWheelControlMode _mouseWheelControlMode;
        private TrayIconBounds? _cachedBounds;
        private long _boundsCacheTimestamp;
        private long _hoverGeneration;
        private bool _mouseWheelListenerConstructionFailed;
        private bool _sampleDispatchFailureLogged;
        private bool _boundsFailureLogged;

        /// <summary>
        /// Raised on the UI thread with the signed number of complete wheel notches delivered over
        /// the icon.
        /// </summary>
        internal event Action<int>? MouseWheelScrolled;

        /// <summary>
        /// Gets or sets the gate checked before wheel deltas enter the accumulator: the UI must be
        /// interactive and some monitor must be able to accept the resulting brightness write.
        /// </summary>
        internal Func<bool>? CanProcessMouseWheel { get; set; }

        /// <summary>
        /// Gets a value indicating whether a wheel notch delivered over the icon right now would be
        /// turned into a brightness change. The hook only arms while this holds, which is what lets
        /// the hook consume the notch instead of forwarding it to the window under the pointer.
        /// </summary>
        private bool IsMouseWheelAdjustmentReady =>
            _mouseWheelControlMode != MouseWheelControlMode.Disabled &&
            CanProcessMouseWheel?.Invoke() == true;

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
            UpdateMouseWheelMode(settings.Properties.MouseWheelControlMode.Normalize());

            if (shouldShow)
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

                if (_trayIconData is null)
                {
                    // We need to stash this handle, so it doesn't clean itself up. If
                    // explorer restarts, we'll come back through here, and we don't
                    // really need to re-load the icon in that case. We can just use
                    // the handle from the first time.
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

                var d = (NOTIFYICONDATAW)_trayIconData;

                // Add the notification icon
                unsafe
                {
                    bool success = Shell_NotifyIconNative((uint)NOTIFY_ICON_MESSAGE.NIM_ADD, &d);
                    if (!success)
                    {
                        // Shell_NotifyIcon can fail if explorer.exe isn't ready yet (e.g., during system startup)
                        // Reset _trayIconData to allow retry via WM_WINDOWPOSCHANGING or WM_TASKBAR_RESTART
                        Logger.LogWarning("[TrayIcon] Shell_NotifyIcon(NIM_ADD) failed, will retry later");
                        _trayIconData = null;
                        return;
                    }
                }

                if (_popupMenu == 0)
                {
                    _popupMenu = CreatePopupMenu();
                    InsertMenuNative(_popupMenu, 0, (uint)(MENU_ITEM_FLAGS.MF_BYPOSITION | MENU_ITEM_FLAGS.MF_STRING), PInvoke.WM_USER + 1, GetString("TrayMenu_Settings"));
                    InsertMenuNative(_popupMenu, 1, (uint)(MENU_ITEM_FLAGS.MF_BYPOSITION | MENU_ITEM_FLAGS.MF_STRING), PInvoke.WM_USER + 2, GetString("TrayMenu_Exit"));
                }

                EnsureMouseWheelListener();
            }
            else
            {
                Destroy();
            }
        }

        public void Destroy()
        {
            DisposeMouseWheelListener();
            InvalidateMouseWheelHover(disarm: false);

            if (_trayIconData is not null)
            {
                var d = (NOTIFYICONDATAW)_trayIconData;
                unsafe
                {
                    if (Shell_NotifyIconNative((uint)NOTIFY_ICON_MESSAGE.NIM_DELETE, &d))
                    {
                        _trayIconData = null;
                    }
                }
            }

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

        /// <summary>
        /// Applies the persisted mouse-wheel mode, starting or stopping the hook thread with it.
        /// </summary>
        private void UpdateMouseWheelMode(MouseWheelControlMode mode)
        {
            mode = mode.Normalize();
            if (_mouseWheelControlMode == mode)
            {
                return;
            }

            _mouseWheelControlMode = mode;
            InvalidateMouseWheelHover(disarm: true);

            if (mode == MouseWheelControlMode.Disabled)
            {
                DisposeMouseWheelListener();
            }
            else if (_trayIconData is not null)
            {
                EnsureMouseWheelListener();
            }
        }

        private void EnsureMouseWheelListener()
        {
            // Reached from the window procedure, where an escaping exception takes the process down.
            // The hook thread is optional, so latch a failed start instead of retrying it per hover.
            if (_mouseWheelControlMode == MouseWheelControlMode.Disabled ||
                _mouseWheelListenerConstructionFailed)
            {
                return;
            }

            try
            {
                _mouseWheelListener ??= new TrayIconMouseWheelListener(
                    OnWheelSampleBatch,
                    OnMouseWheelListenerDisarmed);
            }
            catch (Exception ex)
            {
                // Deliberately broad: starting a thread can also fail with ThreadStateException or
                // OutOfMemoryException, and anything that escapes here ends the process. Wheel
                // control is optional, so latch and carry on with a plain tray icon.
                _mouseWheelListenerConstructionFailed = true;
                Logger.LogWarning($"[TrayWheel] Unable to start the hook thread: {ex.Message}");
            }
        }

        private void DisposeMouseWheelListener()
        {
            _mouseWheelListener?.Dispose();
            _mouseWheelListener = null;
            _mouseWheelListenerConstructionFailed = false;
            _cachedBounds = null;
            _boundsCacheTimestamp = 0;
            _wheelDeltaAccumulator.Reset();
        }

        /// <summary>
        /// Retires the current hover: any sample still in flight is stamped with the old generation
        /// and will be discarded, and the partial notch it belonged to is no longer meaningful.
        /// </summary>
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

        /// <summary>
        /// Arms the hook while the pointer is confirmed to be over the icon and a notch would
        /// actually produce a brightness change. Both conditions are what lets the hook consume the
        /// notch rather than forwarding it on.
        /// </summary>
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
            var previousBounds = _cachedBounds;

            TrayIconBounds bounds;
            if (previousBounds is TrayIconBounds cached &&
                now - _boundsCacheTimestamp <= BoundsCacheLifetimeMs &&
                cached.Contains(cursor.X, cursor.Y))
            {
                bounds = cached;
            }
            else if (!TryQueryTrayIconBounds(out bounds) || !bounds.Contains(cursor.X, cursor.Y))
            {
                // The Shell only notifies while the pointer is over the icon, so a rectangle that
                // excludes the cursor means either the pointer already moved on or the Shell
                // reported a stand-in rectangle for an icon in the notification overflow.
                InvalidateMouseWheelHover(disarm: true);
                return;
            }

            _cachedBounds = bounds;
            _boundsCacheTimestamp = now;

            if (!IsMouseWheelAdjustmentReady)
            {
                _mouseWheelListener?.Disarm();
                return;
            }

            // The Shell repeats this message for every pixel of travel, so only touch the hook
            // thread when something it cares about actually changed. Re-arming on an unchanged
            // rectangle would post a thread message per pixel for no effect.
            if (_mouseWheelListener?.IsArmed == true &&
                previousBounds.HasValue &&
                previousBounds.Value == bounds)
            {
                return;
            }

            unchecked
            {
                _hoverGeneration++;
            }

            _wheelDeltaAccumulator.Reset();
            EnsureMouseWheelListener();
            _mouseWheelListener?.Arm(bounds, _hoverGeneration);
        }

        private unsafe bool TryQueryTrayIconBounds(out TrayIconBounds bounds)
        {
            bounds = default;
            if (_hwnd == 0 || _trayIconData is null)
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
                if (!_boundsFailureLogged)
                {
                    Logger.LogWarning(
                        $"[TrayWheel] Shell_NotifyIconGetRect failed with HRESULT 0x{result:X8}");
                    _boundsFailureLogged = true;
                }

                return false;
            }

            _boundsFailureLogged = false;
            bounds = new TrayIconBounds(rect.Left, rect.Top, rect.Right, rect.Bottom);
            return bounds.IsValid;
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

            if (_mouseWheelControlMode == MouseWheelControlMode.Disabled ||
                CanProcessMouseWheel?.Invoke() != true ||
                !TryQueryTrayIconBounds(out var currentBounds))
            {
                // The gate can go false after the hook was armed - a monitor rescan, for instance.
                // Retire the hover rather than only dropping the partial notch: the pointer may be
                // parked, in which case no further tray mouse-move would arrive to re-evaluate this
                // and the hook would keep swallowing notches nobody acts on.
                InvalidateMouseWheelHover(disarm: true);
                return;
            }

            var now = unchecked((uint)Environment.TickCount);
            var totalNotches = 0;
            var retireHover = false;
            foreach (var sample in samples)
            {
                // The hook only swallows notches that landed inside the rectangle it was armed
                // with, so a sample stamped with a retired hover - or one the Shell has since moved
                // the icon out from under - was never ours. Retire the hover for those, but keep
                // applying the samples in the same batch that were swallowed on our behalf:
                // dropping those would consume a notch without adjusting anything.
                if (sample.HoverGeneration != _hoverGeneration ||
                    !currentBounds.Contains(sample.X, sample.Y))
                {
                    retireHover = true;
                    continue;
                }

                if (unchecked(now - sample.Timestamp) > MaxSampleAgeMs)
                {
                    _wheelDeltaAccumulator.Reset();
                    continue;
                }

                totalNotches += _wheelDeltaAccumulator.Add(sample.Delta);
            }

            if (!retireHover)
            {
                _cachedBounds = currentBounds;
                _boundsCacheTimestamp = Environment.TickCount64;
            }

            if (totalNotches != 0)
            {
                MouseWheelScrolled?.Invoke(totalNotches);
            }

            if (retireHover)
            {
                InvalidateMouseWheelHover(disarm: true);
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
                            _exitAction?.Invoke();
                        }
                    }

                    break;

                // Shell_NotifyIcon can fail when we invoke it during the time explorer.exe isn't present/ready to handle it.
                // We'll also never receive _wmTaskbarRestart message if the first call to Shell_NotifyIcon failed, so we use
                // WM_WINDOWPOSCHANGING which is always received on explorer startup sequence.
                case PInvoke.WM_WINDOWPOSCHANGING:
                    {
                        if (_trayIconData is null)
                        {
                            SetupTrayIcon();
                        }
                    }

                    break;
                default:
                    // _wmTaskbarRestart isn't a compile-time constant, so we can't
                    // use it in a case label
                    if (uMsg == _wmTaskbarRestart)
                    {
                        // Handle the case where explorer.exe restarts.
                        // Even if we created it before, do it again
                        SetupTrayIcon();
                    }
                    else if (uMsg == WmTrayIcon)
                    {
                        switch ((uint)lParam)
                        {
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
                            case WmMouseMove:
                                HandleTrayMouseMove();
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

        [LibraryImport("shell32.dll", EntryPoint = "ExtractIconExW", StringMarshalling = StringMarshalling.Utf16)]
        private static partial uint ExtractIconExNative(string lpszFile, int nIconIndex, out nint phiconLarge, out nint phiconSmall, uint nIcons);

        [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconGetRect")]
        private static partial int ShellNotifyIconGetRectNative(
            ref NotifyIconIdentifier identifier,
            out NativeRect iconLocation);

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
