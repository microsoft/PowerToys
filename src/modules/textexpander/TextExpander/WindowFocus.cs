// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Forces a window to the foreground.
///
/// Windows refuses SetForegroundWindow from a process that is not already foreground and did not
/// receive the last input event — which is exactly our situation, since the user is typing into
/// someone else's window. Temporarily attaching our input queue to the foreground thread's makes
/// the call succeed; this is the long-standing workaround every input utility relies on.
/// </summary>
internal static class WindowFocus
{
    public static bool Force(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        IntPtr foreground = Native.GetForegroundWindow();
        if (foreground == hwnd)
        {
            return true;
        }

        uint ourThread = Native.GetCurrentThreadId();
        uint foregroundThread = foreground == IntPtr.Zero
            ? 0
            : Native.GetWindowThreadProcessId(foreground, out _);

        bool attached = false;
        if (foregroundThread != 0 && foregroundThread != ourThread)
        {
            attached = Native.AttachThreadInput(ourThread, foregroundThread, true);
        }

        try
        {
            Native.BringWindowToTop(hwnd);
            Native.ShowWindow(hwnd, Native.SW_SHOW);
            bool ok = Native.SetForegroundWindow(hwnd);
            Native.SetActiveWindow(hwnd);
            return ok;
        }
        finally
        {
            if (attached)
            {
                Native.AttachThreadInput(ourThread, foregroundThread, false);
            }
        }
    }
}
