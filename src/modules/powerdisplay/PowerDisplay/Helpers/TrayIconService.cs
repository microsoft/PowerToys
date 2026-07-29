// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PowerDisplay.Common.Services;
using PowerDisplay.Models;
using PowerDisplay.PowerDisplayXAML;
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
        private const uint WmContextMenu = 0x007B;

        // NOTIFYICON_VERSION_4 notification events. They arrive in the low word of lParam, so they
        // do not collide with WmTrayIcon even where the numeric values overlap.
        private const uint NinSelect = PInvoke.WM_USER;
        private const uint NinKeySelect = PInvoke.WM_USER + 1;
        private const uint NinPopupOpen = PInvoke.WM_USER + 6;
        private const uint NinPopupClose = PInvoke.WM_USER + 7;
        private const uint NotifyIconVersion4 = 4;
        private const long BoundsCacheLifetimeMs = 1000;
        private const uint MaxSampleAgeMs = 500;
        private static readonly TimeSpan ImmediateRegistrationCheck = TimeSpan.FromMilliseconds(1);

        // Cadence for noticing that the pointer left the icon. NOTIFYICON_VERSION_4 also reports
        // the departure through NIN_POPUPCLOSE, but the Shell only sends that as the partner of a
        // NIN_POPUPOPEN it emits after its own hover dwell, which can outlast the delay before the
        // overlay appears; the legacy protocol reports nothing at all and has to poll faster.
        private static readonly TimeSpan HoverWatchdogInterval = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan LegacyHoverPollInterval = TimeSpan.FromMilliseconds(100);

        private readonly SettingsUtils _settingsUtils;
        private readonly Action _toggleWindowAction;
        private readonly Action _exitAction;
        private readonly Action _openSettingsAction;
        private readonly uint _wmTaskbarRestart;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly WheelDeltaAccumulator _wheelDeltaAccumulator = new();
        private readonly TrayIconRegistrationBackoff _registrationBackoff = new();
        private readonly TrayWheelFeedbackSession _feedbackSession = new();

        private Window? _window;
        private nint _hwnd;
        private nint _originalWndProc;
        private WndProcDelegate? _trayWndProc;
        private NOTIFYICONDATAW? _trayIconData;
        private nint _largeIcon;
        private nint _popupMenu;
        private TrayIconMouseWheelListener? _mouseWheelListener;
        private DispatcherQueueTimer? _registrationTimer;
        private DispatcherQueueTimer? _feedbackTimer;
        private TrayWheelFeedbackWindow? _feedbackWindow;
        private MouseWheelControlMode _mouseWheelControlMode;
        private string? _appName;
        private TrayWheelFeedbackTemplates? _feedbackTemplates;
        private TrayIconBounds? _cachedBounds;
        private long _boundsCacheTimestamp;
        private long _hoverGeneration;
        private bool _desiredTrayIconVisible;
        private bool _isTrayIconRegistered;
        private bool _trayIconUsesVersion4;
        private bool _registrationFailureLogged;
        private bool _mouseWheelListenerConstructionFailed;
        private bool _sampleDispatchFailureLogged;
        private bool _boundsFailureLogged;
        private bool _hoverOutsideBoundsLogged;
        private bool _feedbackWindowFailureLogged;
        private bool _feedbackWindowConstructionFailed;
        private bool _feedbackPresentationFailureLogged;

        internal event Action<int>? MouseWheelScrolled;

        /// <summary>
        /// Gets or sets the gate checked before wheel deltas enter the accumulator: the UI must be
        /// interactive and some monitor must be able to accept the resulting brightness write.
        /// </summary>
        internal Func<bool>? CanProcessMouseWheel { get; set; }

        /// <summary>
        /// Gets a value indicating whether a wheel notch delivered over the icon right now would be
        /// turned into a brightness change. The hook only arms while this holds, which is what lets
        /// the hook consume the notch instead of forwarding it to the focused window.
        /// </summary>
        private bool IsMouseWheelAdjustmentReady =>
            _mouseWheelControlMode != MouseWheelControlMode.Disabled &&
            CanProcessMouseWheel?.Invoke() == true;

        /// <summary>
        /// Gets the localized product name shown as the icon's name and as the ordinary hover text.
        /// Cached because the hover presentation is re-applied on every tray mouse-move message.
        /// </summary>
        private string AppName => _appName ??= GetString("AppName");

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
            DisposeFeedbackWindow();
            _mouseWheelControlMode = MouseWheelControlMode.Disabled;
            InvalidateMouseWheelHover(disarm: false);

            if (_trayIconData is not null)
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
                    uFlags = BuildTrayIconFlags(),
                    uCallbackMessage = WmTrayIcon,
                    hIcon = new HICON(_largeIcon),
                    szTip = AppName,
                };
            }
        }

        /// <summary>
        /// Builds the notification-icon flags for the current mouse-wheel mode.
        /// <para>NIF_TIP without NIF_SHOWTIP keeps szTip as the icon's name for the overflow
        /// flyout, the taskbar icon list and UI Automation, while NOTIFYICON_VERSION_4 suppresses
        /// the standard tooltip and sends NIN_POPUPOPEN/NIN_POPUPCLOSE instead so the hover overlay
        /// can replace it.</para>
        /// <para>That trade only pays for itself while the overlay actually runs. With mouse wheel
        /// control off there is no overlay and no wheel gesture to annotate, so ask for the standard
        /// tooltip back rather than leaving the icon with no hover text at all - a user who opted
        /// out of this feature should get the pre-existing tray behaviour, including for keyboard
        /// and touch, which never reach the cursor-gated overlay.</para>
        /// </summary>
        private NOTIFY_ICON_DATA_FLAGS BuildTrayIconFlags()
        {
            var flags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP;
            if (_mouseWheelControlMode == MouseWheelControlMode.Disabled)
            {
                flags |= NOTIFY_ICON_DATA_FLAGS.NIF_SHOWTIP;
            }

            return flags;
        }

        /// <summary>
        /// Re-applies <see cref="BuildTrayIconFlags"/> to a live registration after the mouse-wheel
        /// mode changed, so switching the setting hands the tooltip back and forth without needing
        /// a module restart. NIF_SHOWTIP is honoured under NOTIFYICON_VERSION_4, so the callback
        /// packing <see cref="DispatchTrayNotification"/> decodes is unaffected either way.
        /// </summary>
        private void ApplyTrayIconTooltipMode()
        {
            if (_trayIconData is null)
            {
                return;
            }

            var data = (NOTIFYICONDATAW)_trayIconData;
            var flags = BuildTrayIconFlags();
            if (data.uFlags == flags)
            {
                return;
            }

            data.uFlags = flags;
            _trayIconData = data;

            if (!_isTrayIconRegistered)
            {
                return;
            }

            bool modified;
            unsafe
            {
                modified = Shell_NotifyIconNative((uint)NOTIFY_ICON_MESSAGE.NIM_MODIFY, &data);
            }

            if (!modified)
            {
                Logger.LogWarning("[TrayIcon] Shell_NotifyIcon(NIM_MODIFY) failed while updating the hover presentation");
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

            MarkTrayIconRegistrationStale(resetBackoff: _isTrayIconRegistered, scheduleRecovery: false);

            var data = (NOTIFYICONDATAW)_trayIconData;
            bool added;
            unsafe
            {
                added = Shell_NotifyIconNative((uint)NOTIFY_ICON_MESSAGE.NIM_ADD, &data);
            }

            if (added)
            {
                ApplyNotifyIconVersion(data);
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

        /// <summary>
        /// Marks the icon as live and stands the recovery timer down. Nothing polls the healthy
        /// state: a lost registration is reported by the <c>TaskbarCreated</c> broadcast, by a
        /// failing <c>Shell_NotifyIconGetRect</c> on the next hover, or by the settings-update path,
        /// and each of those schedules its own recovery.
        /// </summary>
        private void CompleteTrayIconRegistration()
        {
            _isTrayIconRegistered = true;
            StopRegistrationRecovery();
            EnsureMouseWheelListener();
            EnsureTrayIconMenu();
        }

        /// <summary>
        /// Opts the freshly added icon into NOTIFYICON_VERSION_4. That is what suppresses the
        /// standard Shell tooltip while keeping szTip as the icon's name, and what makes the Shell
        /// send NIN_POPUPOPEN/NIN_POPUPCLOSE, NIN_SELECT/NIN_KEYSELECT and WM_CONTEXTMENU. The
        /// callback packing differs between versions, so <see cref="WindowProc"/> decodes according
        /// to whether this succeeded.
        /// </summary>
        private void ApplyNotifyIconVersion(NOTIFYICONDATAW data)
        {
            data.Anonymous.uVersion = NotifyIconVersion4;
            bool applied;
            unsafe
            {
                applied = Shell_NotifyIconNative((uint)NOTIFY_ICON_MESSAGE.NIM_SETVERSION, &data);
            }

            _trayIconUsesVersion4 = applied;
            if (!applied)
            {
                Logger.LogWarning("[TrayIcon] Shell_NotifyIcon(NIM_SETVERSION) failed; falling back to legacy callbacks");
            }
        }

        private void MarkTrayIconRegistrationStale(bool resetBackoff, bool scheduleRecovery)
        {
            StopHoverFeedback();

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

            if (_registrationTimer is null)
            {
                _registrationTimer = _dispatcherQueue.CreateTimer();
                _registrationTimer.IsRepeating = false;
                _registrationTimer.Tick += OnRegistrationTimerTick;
            }

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
            ApplyTrayIconTooltipMode();
            InvalidateMouseWheelHover(disarm: true);

            if (mode == MouseWheelControlMode.Disabled)
            {
                // The Shell tooltip is back, so take our own overlay down instead of leaving the
                // last one parked until the pointer happens to move.
                StopHoverFeedback();
                DisposeMouseWheelListener();
            }
            else if (_isTrayIconRegistered)
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
                _mouseWheelListener.SetEnabled(true);
            }
            catch (Exception ex)
            {
                // Deliberately broad: starting a thread can also fail with ThreadStateException or
                // OutOfMemoryException, and this runs on the window procedure where anything that
                // escapes ends the process. Wheel control is optional, so latch and carry on.
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
                // Off keeps NIF_SHOWTIP (see BuildTrayIconFlags), so the Shell draws the hover text
                // and there is nothing for us to present or arm.
                return;
            }

            if (!GetCursorPos(out var cursor))
            {
                StopHoverFeedback();
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
                ApplyFeedbackPresentation(_feedbackSession.StartHover(now), cached);
                ScheduleFeedbackTick();

                if (!IsMouseWheelAdjustmentReady)
                {
                    _mouseWheelListener?.Disarm();
                    return;
                }

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

            if (!TryGetCurrentIconBounds(out var bounds))
            {
                StopHoverFeedback();
                InvalidateMouseWheelHover(disarm: true);
                return;
            }

            if (!bounds.Contains(cursor.X, cursor.Y))
            {
                // The Shell only notifies while the pointer is over the icon, so a rectangle that
                // excludes the cursor means either the pointer already moved on or the Shell
                // reported a stand-in rectangle for an icon parked in the notification overflow.
                if (!_hoverOutsideBoundsLogged)
                {
                    Logger.LogInfo(
                        $"[TrayWheel] Tray hover at ({cursor.X}, {cursor.Y}) is outside the reported icon rectangle ({bounds.Left}, {bounds.Top}, {bounds.Right}, {bounds.Bottom})");
                    _hoverOutsideBoundsLogged = true;
                }

                StopHoverFeedback();
                InvalidateMouseWheelHover(disarm: true);
                return;
            }

            ApplyFeedbackPresentation(_feedbackSession.StartHover(now), bounds);
            ScheduleFeedbackTick();

            if (!IsMouseWheelAdjustmentReady)
            {
                _cachedBounds = bounds;
                _boundsCacheTimestamp = now;
                _mouseWheelListener?.Disarm();
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
            if (!TryQueryTrayIconBounds(out bounds, out var result))
            {
                if (result < 0)
                {
                    MarkTrayIconRegistrationStale(resetBackoff: true, scheduleRecovery: true);
                }
                else if (_isTrayIconRegistered && _hwnd != 0 && _trayIconData is not null)
                {
                    if (!_boundsFailureLogged)
                    {
                        Logger.LogWarning(
                            $"[TrayWheel] Shell_NotifyIconGetRect failed with HRESULT 0x{result:X8}");
                        _boundsFailureLogged = true;
                    }
                }

                return false;
            }

            _boundsFailureLogged = false;
            return true;
        }

        private unsafe bool TryQueryTrayIconBounds(out TrayIconBounds bounds, out int result)
        {
            bounds = default;
            result = 0;
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
            result = ShellNotifyIconGetRectNative(ref identifier, out var rect);
            bounds = new TrayIconBounds(rect.Left, rect.Top, rect.Right, rect.Bottom);
            return result >= 0 && bounds.IsValid;
        }

        /// <summary>
        /// Arms the next feedback tick. Two deadlines compete for it: the next presentation change
        /// that time alone will produce (the hover reveal, or the adjustment readout expiring back
        /// to the app name), and the watchdog that notices the pointer leaving. The earlier one
        /// wins, because neither can stand in for the other - NIN_POPUPCLOSE never arrives for a
        /// hover that ended before the Shell opened its own pop-up, and without
        /// NOTIFYICON_VERSION_4 it is not sent at all, so a two-second adjustment readout must not
        /// blind the watchdog for two seconds.
        /// </summary>
        private void ScheduleFeedbackTick()
        {
            if (!_feedbackSession.IsHovering)
            {
                _feedbackTimer?.Stop();
                return;
            }

            var interval = _trayIconUsesVersion4 ? HoverWatchdogInterval : LegacyHoverPollInterval;
            if (_feedbackSession.NextTransitionDelay(Environment.TickCount64) is long delay)
            {
                var pending = TimeSpan.FromMilliseconds(Math.Max(1L, delay));
                if (pending < interval)
                {
                    interval = pending;
                }
            }

            if (_feedbackTimer is null)
            {
                _feedbackTimer = _dispatcherQueue.CreateTimer();
                _feedbackTimer.IsRepeating = false;
                _feedbackTimer.Tick += OnFeedbackTimerTick;
            }

            _feedbackTimer.Stop();
            _feedbackTimer.Interval = interval;
            _feedbackTimer.Start();
        }

        private void OnFeedbackTimerTick(DispatcherQueueTimer sender, object args)
        {
            sender.Stop();
            if (!GetCursorPos(out var cursor) ||
                !TryQueryTrayIconBounds(out var bounds, out _) ||
                !bounds.Contains(cursor.X, cursor.Y))
            {
                // The rectangle can move out from under a pointer that never moved (an auto-hide
                // taskbar, or a neighbouring icon appearing), so this is a hover departure the
                // mouse-move path will not report. Drop the armed rectangle with it.
                StopHoverFeedback();
                InvalidateMouseWheelHover(disarm: true);
                return;
            }

            ApplyFeedbackPresentation(
                _feedbackSession.Tick(Environment.TickCount64, pointerInside: true),
                bounds);
            ScheduleFeedbackTick();
        }

        private void ApplyFeedbackPresentation(
            TrayWheelFeedbackSession.Presentation presentation,
            TrayIconBounds bounds)
        {
            switch (presentation.Kind)
            {
                case TrayWheelFeedbackSession.PresentationKind.AppName:
                    ShowFeedbackOverlay(AppName, bounds);
                    break;
                case TrayWheelFeedbackSession.PresentationKind.Adjustment:
                    if (!string.IsNullOrEmpty(presentation.Text))
                    {
                        ShowFeedbackOverlay(presentation.Text, bounds);
                    }
                    else
                    {
                        _feedbackWindow?.HideFeedback();
                    }

                    break;
                default:
                    _feedbackWindow?.HideFeedback();
                    break;
            }
        }

        private void ShowFeedbackOverlay(string text, TrayIconBounds bounds)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                // AppName resolves to an empty string when the resource is missing from the active
                // language's PRI, and ShowText rejects blank text. This runs on the tray window
                // procedure, where an escaping exception ends the process, so drop the overlay
                // instead of presenting nothing and throwing.
                _feedbackWindow?.HideFeedback();
                return;
            }

            // Do not thrash window creation inside one hover session, but do retry on the next one:
            // with the standard Shell tooltip suppressed this overlay is the only hover affordance.
            if (_feedbackWindowConstructionFailed && _feedbackWindow is null)
            {
                return;
            }

            try
            {
                _feedbackWindow ??= new TrayWheelFeedbackWindow();
                if (_feedbackWindow.ShowText(text, bounds))
                {
                    _feedbackWindowFailureLogged = false;
                }
                else
                {
                    _feedbackWindow.HideFeedback();
                }
            }
            catch (Exception ex) when (
                ex is System.ComponentModel.Win32Exception or
                System.Runtime.InteropServices.COMException or
                InvalidOperationException or
                ArgumentException)
            {
                _feedbackWindow?.HideFeedback();
                _feedbackWindowConstructionFailed = _feedbackWindow is null;
                if (!_feedbackWindowFailureLogged)
                {
                    Logger.LogWarning($"[TrayFeedback] Unable to show overlay: {ex.Message}");
                    _feedbackWindowFailureLogged = true;
                }
            }
        }

        internal void UpdateAdjustmentFeedback(TrayWheelAdjustmentFeedback? feedback)
        {
            var now = Environment.TickCount64;
            if (!GetCursorPos(out var cursor) ||
                !TryQueryTrayIconBounds(out var bounds, out _) ||
                !bounds.Contains(cursor.X, cursor.Y))
            {
                StopHoverFeedback();
                return;
            }

            if (feedback is null)
            {
                ApplyFeedbackPresentation(
                    _feedbackSession.ClearAdjustment(now, pointerInside: true),
                    bounds);
                ScheduleFeedbackTick();
                return;
            }

            try
            {
                // The resource strings cannot change without an app restart, and a fast wheel
                // produces several adjustments per second, so build the templates once. Kept inside
                // the try so a failed lookup still lands in the handler below.
                _feedbackTemplates ??= new TrayWheelFeedbackTemplates(
                    ResourceLoaderInstance.ResourceLoader.GetString("TrayWheelFeedbackPrimaryFormat"),
                    ResourceLoaderInstance.ResourceLoader.GetString("TrayWheelFeedbackPrimaryPluralFormat"),
                    ResourceLoaderInstance.ResourceLoader.GetString("TrayWheelFeedbackAllFormat"),
                    ResourceLoaderInstance.ResourceLoader.GetString("TrayWheelFeedbackLinkedFormat"),
                    ResourceLoaderInstance.ResourceLoader.GetString("TrayWheelFeedbackPercentageFormat"),
                    ResourceLoaderInstance.ResourceLoader.GetString("TrayWheelFeedbackRangeFormat"),
                    ResourceLoaderInstance.ResourceLoader.GetString("TrayWheelFeedbackListSeparator"));
                var text = TrayWheelFeedbackFormatter.Format(
                    feedback,
                    _feedbackTemplates,
                    CultureInfo.CurrentCulture);
                if (text is null)
                {
                    StopHoverFeedback();
                    return;
                }

                ApplyFeedbackPresentation(
                    _feedbackSession.ShowAdjustment(text, now),
                    bounds);
                _feedbackPresentationFailureLogged = false;
            }
            catch (Exception ex) when (
                ex is COMException or
                InvalidOperationException or
                FormatException or
                ArgumentException)
            {
                StopHoverFeedback();
                if (!_feedbackPresentationFailureLogged)
                {
                    Logger.LogWarning($"[TrayFeedback] Unable to prepare overlay feedback: {ex.Message}");
                    _feedbackPresentationFailureLogged = true;
                }

                return;
            }

            ScheduleFeedbackTick();
        }

        private void StopHoverFeedback()
        {
            _feedbackTimer?.Stop();
            _feedbackSession.Stop();
            _feedbackWindowConstructionFailed = false;
            _feedbackWindow?.HideFeedback();
        }

        private void DisposeFeedbackWindow()
        {
            _feedbackTimer?.Stop();
            _feedbackSession.Stop();
            _feedbackWindowConstructionFailed = false;

            var window = _feedbackWindow;
            _feedbackWindow = null;
            if (window is not null)
            {
                // Deliberately not routed through HideFeedback: TransparentWindow.Hide defers its
                // work to the dispatcher, and that queued callback would then run against a window
                // this method has already closed.
                window.Dispose();
                window.Close();
            }
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
                // The gate went false after the hook was armed - a monitor rescan, for instance.
                // Retire the hover rather than only dropping the partial notch: the pointer may be
                // parked, in which case no tray mouse-move would arrive to re-evaluate this, and
                // the hook would keep swallowing notches nobody acts on.
                InvalidateMouseWheelHover(disarm: true);
                return;
            }

            if (!TryGetCurrentIconBounds(out var currentBounds))
            {
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

                // Input that queued up behind a stalled UI thread should not move brightness late,
                // and the partial notch it belonged to is no longer meaningful either.
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
                        // Do not shorten a pending backoff retry: a window-position message is not
                        // evidence that Explorer became available again.
                        if (_desiredTrayIconVisible &&
                            !_isTrayIconRegistered &&
                            _registrationTimer?.IsRunning != true)
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
                    else if (uMsg == WmTrayIcon)
                    {
                        DispatchTrayNotification(wParam, lParam);
                    }

                    break;
            }

            return CallWindowProcIntPtr(_originalWndProc, hwnd, uMsg, wParam, lParam);
        }

        /// <summary>
        /// Unpacks a notification-icon callback. NOTIFYICON_VERSION_4 packs the event in the low
        /// word of lParam, the icon id in its high word and the Shell-chosen anchor point in wParam;
        /// the legacy packing puts the id in wParam and the event in lParam.
        /// </summary>
        private void DispatchTrayNotification(nuint wParam, nint lParam)
        {
            if (_trayIconUsesVersion4)
            {
                var packed = unchecked((uint)lParam);
                if ((packed >> 16) == MyNotifyId)
                {
                    HandleTrayNotification(packed & 0xFFFF, wParam);
                }

                return;
            }

            if ((uint)wParam == MyNotifyId)
            {
                HandleTrayNotification(unchecked((uint)lParam), 0);
            }
        }

        private void HandleTrayNotification(uint notification, nuint anchor)
        {
            switch (notification)
            {
                case WmMouseMove:
                case NinPopupOpen:
                    HandleTrayMouseMove();
                    break;

                case NinPopupClose:
                    DismissTrayHover();
                    break;

                case WmContextMenu:
                    ShowTrayContextMenu(AnchorX(anchor), AnchorY(anchor));
                    break;

                case PInvoke.WM_RBUTTONUP:
                    // Version 4 delivers WM_CONTEXTMENU instead, and still forwards the raw button
                    // message; handling both would open the menu twice.
                    if (!_trayIconUsesVersion4 && GetCursorPos(out var cursorPos))
                    {
                        ShowTrayContextMenu(cursorPos.X, cursorPos.Y);
                    }

                    break;

                case NinSelect:
                case NinKeySelect:
                    ActivateFromTrayIcon();
                    break;

                case PInvoke.WM_LBUTTONUP:
                    // Superseded by NIN_SELECT under version 4, same double-invoke reasoning.
                    if (!_trayIconUsesVersion4)
                    {
                        ActivateFromTrayIcon();
                    }

                    break;
            }
        }

        private void ShowTrayContextMenu(int x, int y)
        {
            if (_popupMenu == 0)
            {
                return;
            }

            DismissTrayHover();
            SetForegroundWindow(_hwnd);
            TrackPopupMenuExNative(_popupMenu, (uint)TRACK_POPUP_MENU_FLAGS.TPM_LEFTALIGN | (uint)TRACK_POPUP_MENU_FLAGS.TPM_BOTTOMALIGN, x, y, _hwnd, 0);

            // TrackPopupMenuEx runs its own modal loop, so control returns here once the menu is
            // gone. The pointer can be back on the icon without ever having moved, and a still
            // pointer produces no further tray mouse-move to re-arm from, so re-evaluate the hover
            // rather than leaving wheel control dead until the user nudges the mouse.
            HandleTrayMouseMove();
        }

        private void ActivateFromTrayIcon()
        {
            // Take the overlay down the way the Shell tooltip used to disappear on click, but keep
            // the wheel hover armed: the pointer is still over the icon, and arming only happens on
            // a tray mouse-move that a still pointer never produces. Leaving the icon still disarms
            // through the hook's own out-of-bounds check.
            StopHoverFeedback();
            _toggleWindowAction?.Invoke();
        }

        /// <summary>
        /// Tears the hover down completely: the presentation and the armed wheel rectangle. Used
        /// where the pointer is either gone (NIN_POPUPCLOSE) or about to be captured by something
        /// else (the context menu's modal loop), so a stale rectangle cannot outlive the gesture.
        /// Activation takes the lighter path instead - see <see cref="ActivateFromTrayIcon"/>.
        /// </summary>
        private void DismissTrayHover()
        {
            StopHoverFeedback();
            InvalidateMouseWheelHover(disarm: true);
        }

        private static int AnchorX(nuint anchor) => unchecked((short)(uint)anchor);

        private static int AnchorY(nuint anchor) => unchecked((short)((uint)anchor >> 16));

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
