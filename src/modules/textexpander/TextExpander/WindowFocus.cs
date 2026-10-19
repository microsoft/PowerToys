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
    /// <summary>How long a window that has just come to the front gets to settle before it's typed into.</summary>
    internal const int SettleMs = 25;

    private const int PollMs = 5;

    /// <summary>
    /// Waits until <paramref name="hwnd"/> is the foreground window, checking every few
    /// milliseconds for at most <paramref name="timeoutMs"/>, then lets it settle briefly. A
    /// window that is already in front costs only the settle. Returns whether it came to the front.
    /// </summary>
    public static bool WaitUntilInFront(IntPtr hwnd, int timeoutMs)
    {
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        return WaitUntilInFront(hwnd, timeoutMs, Native.GetForegroundWindow, Thread.Sleep, () => (long)System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    }

    /// <summary>The wait behind <see cref="WaitUntilInFront(IntPtr, int)"/>, with the clock and the window check passed in for tests.</summary>
    internal static bool WaitUntilInFront(IntPtr hwnd, int timeoutMs, Func<IntPtr> foreground, Action<int> sleep, Func<long> elapsedMs)
    {
        while (foreground() != hwnd)
        {
            if (elapsedMs() >= timeoutMs)
            {
                return false;
            }

            sleep(PollMs);
        }

        sleep(SettleMs);
        return true;
    }

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
