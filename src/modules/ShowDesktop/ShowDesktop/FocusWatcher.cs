// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;

namespace ShowDesktop
{
    internal sealed class FocusWatcher : IDisposable
    {
        private IntPtr _hookId;
        private NativeMethods.WinEventDelegate? _proc;
        private bool _disposed;

        public event Action? ForegroundWindowChanged;

        public void Start()
        {
            _proc = WinEventCallback;
            _hookId = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _proc,
                0,
                0,
                NativeMethods.WINEVENT_OUTOFCONTEXT);

            if (_hookId == IntPtr.Zero)
            {
                Logger.LogError("Failed to install WinEvent hook for foreground tracking.");
            }
            else
            {
                Logger.LogInfo("FocusWatcher started.");
            }
        }

        public void Stop()
        {
            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_hookId);
                _hookId = IntPtr.Zero;
                Logger.LogInfo("FocusWatcher stopped.");
            }
        }

        private void WinEventCallback(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime)
        {
            ForegroundWindowChanged?.Invoke();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
        }
    }
}
