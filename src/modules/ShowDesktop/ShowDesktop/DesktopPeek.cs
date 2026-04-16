// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;

namespace ShowDesktop
{
    internal sealed class DesktopPeek : IDisposable
    {
        private enum State
        {
            Idle,
            Peeking,
        }

        private readonly MouseHook _mouseHook;
        private readonly FocusWatcher _focusWatcher;
        private readonly WindowTracker _windowTracker;
        private State _state = State.Idle;
        private ShowDesktopSettings _settings;
        private bool _disposed;

        // Cached settings values
        private PeekMode _peekMode;
        private bool _requireDoubleClick;
        private bool _enableTaskbarPeek;
        private bool _enableGamingDetection;

        public DesktopPeek(ShowDesktopSettings settings)
        {
            _settings = settings;
            ApplySettings();
            _mouseHook = new MouseHook();
            _focusWatcher = new FocusWatcher();
            _windowTracker = new WindowTracker();
        }

        public void Start()
        {
            _mouseHook.DesktopClicked += OnDesktopClicked;
            _mouseHook.Install();
            _focusWatcher.ForegroundWindowChanged += OnForegroundChanged;
            _focusWatcher.Start();
            Logger.LogInfo("DesktopPeek started.");
        }

        public void Stop()
        {
            _mouseHook.DesktopClicked -= OnDesktopClicked;
            _mouseHook.Uninstall();
            _focusWatcher.ForegroundWindowChanged -= OnForegroundChanged;
            _focusWatcher.Stop();

            // Restore windows if we're currently peeking
            if (_state == State.Peeking && _peekMode != PeekMode.Native)
            {
                _windowTracker.Restore(_peekMode);
            }

            _state = State.Idle;
            Logger.LogInfo("DesktopPeek stopped.");
        }

        public void UpdateSettings(ShowDesktopSettings settings)
        {
            _settings = settings;
            ApplySettings();
            Logger.LogInfo("DesktopPeek settings updated.");
        }

        private void ApplySettings()
        {
            _peekMode = (PeekMode)_settings.Properties.PeekMode.Value;
            _requireDoubleClick = _settings.Properties.RequireDoubleClick.Value;
            _enableTaskbarPeek = _settings.Properties.EnableTaskbarPeek.Value;
            _enableGamingDetection = _settings.Properties.EnableGamingDetection.Value;
        }

        private void OnDesktopClicked(MouseHookEventArgs args)
        {
            try
            {
                // Ignore taskbar clicks unless explicitly enabled
                if (args.IsTaskbar && !_enableTaskbarPeek)
                {
                    return;
                }

                // Gaming / fullscreen detection
                if (_enableGamingDetection && IsFullscreenAppRunning())
                {
                    return;
                }

                // Double-click requirement
                if (_requireDoubleClick && !args.IsDoubleClick)
                {
                    return;
                }

                if (_state == State.Idle)
                {
                    Logger.LogInfo($"Desktop clicked — peeking (mode={_peekMode}).");

                    if (_peekMode == PeekMode.Native)
                    {
                        WindowTracker.SendShowDesktop();
                    }
                    else
                    {
                        _windowTracker.CaptureAndMinimize(_peekMode);
                    }

                    _state = State.Peeking;
                }
                else
                {
                    Logger.LogInfo("Desktop clicked — restoring.");

                    if (_peekMode == PeekMode.Native)
                    {
                        WindowTracker.SendShowDesktop(); // toggles back
                    }
                    else
                    {
                        _windowTracker.Restore(_peekMode);
                    }

                    _state = State.Idle;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error handling desktop click: {ex.Message}");
            }
        }

        private void OnForegroundChanged()
        {
            try
            {
                if (_state != State.Peeking)
                {
                    return;
                }

                // If the user activated a regular window (not the desktop), cancel peek
                IntPtr fg = NativeMethods.GetForegroundWindow();
                if (fg == IntPtr.Zero || DesktopDetector.IsDesktopWindow(fg))
                {
                    return;
                }

                Logger.LogInfo("Foreground changed to non-desktop window — cancelling peek.");

                if (_peekMode != PeekMode.Native)
                {
                    _windowTracker.Restore(_peekMode);
                }

                _state = State.Idle;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error handling foreground change: {ex.Message}");
            }
        }

        private static bool IsFullscreenAppRunning()
        {
            int hr = NativeMethods.SHQueryUserNotificationState(out var state);
            if (hr != 0)
            {
                return false;
            }

            return state == NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN
                || state == NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _mouseHook.Dispose();
                _focusWatcher.Dispose();
                _disposed = true;
            }
        }
    }
}
