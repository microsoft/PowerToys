// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI.WindowEventHook
{
    using System;
    using System.Reflection.Metadata;
    using System.Runtime.ConstrainedExecution;
    using System.Runtime.InteropServices;
    using Microsoft.Win32.SafeHandles;
    using Peek.UI.Native;
    using Windows.Win32;

    public class WindowEventHook : IDisposable
    {
        public event EventHandler<WindowEventHookEventArgs>? WindowEventReceived;

        public WindowEventHook()
        {
            var moveOrResizeEvent = WindowEvent.EVENT_SYSTEM_MOVESIZEEND;

            var windowHookEventHandler = new WindowEventProc(OnWindowEventProc);

            var hook = PInvoke.SetWinEventHook(
                (uint)moveOrResizeEvent,
                (uint)moveOrResizeEvent,
                new SafeHandle(),
                windowHookEventHandler,
                0,
                0,
                WinEventHookFlags.WINEVENT_OUTOFCONTEXT | WinEventHookFlags.WINEVENT_SKIPOWNPROCESS);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        private void OnWindowEventProc(nint hWinEventHook, WindowEvent eventType, nint hwnd, AccessibleObjectID idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            throw new NotImplementedException();
        }
    }

    public record WindowEventHookEventArgs(WindowEvent eventType, IntPtr windowHandle);

    public delegate void WindowEventProc(
        IntPtr hWinEventHook,
        WindowEvent eventType,
        IntPtr hwnd,
        AccessibleObjectID idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime);
}
