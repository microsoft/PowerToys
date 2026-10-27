// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

using ManagedCommon;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MouseJump.WinUI3.Helpers;

internal static class ForegroundHelper
{
    /// <summary>
    /// Shows the window and makes a best-effort attempt to bring it to the foreground
    /// and give it keyboard focus.
    /// </summary>
    /// <remarks>
    /// Uses raw Win32 calls instead of WinUI3's AppWindow.Show()/Activate()/Focus()
    /// because WinUI3's activation sequence intermittently fails to transfer keyboard
    /// focus, which causes the Window.Activated "Deactivated" event to never fire
    /// when the user clicks away, leaving the preview window stuck open.
    ///
    /// Windows can still refuse to bring the window to the foreground (the foreground
    /// lock), so failures are logged rather than thrown. The preview window is topmost,
    /// so it stays visible and clickable without focus, whereas throwing here terminated
    /// the Mouse Jump process. See https://github.com/microsoft/PowerToys/issues/50424.
    /// </remarks>
    public static void ShowAndActivate(HWND hWnd)
    {
        _ = PInvoke.ShowWindow(hWnd, SHOW_WINDOW_CMD.SW_SHOW);

        if (!ForegroundHelper.TrySetForegroundWindow(hWnd))
        {
            Logger.LogWarning($"{nameof(PInvoke.SetForegroundWindow)} failed after retry. Continuing without focus.");
        }

        PInvoke.SetLastError(0);
        var setFocusResult = PInvoke.SetFocus(hWnd);
        if (setFocusResult.IsNull)
        {
            var lastError = Marshal.GetLastPInvokeError();
            if (lastError != 0)
            {
                Logger.LogWarning($"{nameof(PInvoke.SetFocus)} failed with error {lastError}. Continuing.");
            }
        }

        PInvoke.SetLastError(0);
        var setActiveResult = PInvoke.SetActiveWindow(hWnd);
        if (setActiveResult.IsNull)
        {
            var lastError = Marshal.GetLastPInvokeError();
            if (lastError != 0)
            {
                Logger.LogWarning($"{nameof(PInvoke.SetActiveWindow)} failed with error {lastError}. Continuing.");
            }
        }
    }

    /// <summary>
    /// Brings the window to the foreground. If Windows refuses the first attempt, retries
    /// once with this thread's input temporarily attached to the current foreground thread.
    /// </summary>
    /// <returns>True if the window became the foreground window, otherwise false.</returns>
    private static bool TrySetForegroundWindow(HWND hWnd)
    {
        if ((bool)PInvoke.SetForegroundWindow(hWnd))
        {
            return true;
        }

        var foregroundThreadId = PInvoke.GetWindowThreadProcessId(PInvoke.GetForegroundWindow(), out _);
        var currentThreadId = PInvoke.GetCurrentThreadId();
        var attached = foregroundThreadId != 0
            && foregroundThreadId != currentThreadId
            && (bool)PInvoke.AttachThreadInput(currentThreadId, foregroundThreadId, true);
        try
        {
            _ = PInvoke.BringWindowToTop(hWnd);
            var result = (bool)PInvoke.SetForegroundWindow(hWnd);
            Logger.LogWarning($"{nameof(PInvoke.SetForegroundWindow)} failed. Retried with input attached to foreground thread {foregroundThreadId} (attached = {attached}). Retry succeeded: {result}");
            return result;
        }
        finally
        {
            if (attached)
            {
                _ = PInvoke.AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }
    }
}
