// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed partial class WindowHelper
{
    /// <summary>
    /// Known applications whose visible windows can trigger a QUNS_BUSY state
    /// even when the user is not actually in a fullscreen/presentation scenario.
    /// </summary>
    internal sealed record KnownTriggerApp(string ProcessName, string WindowClassName, string DisplayName);

    internal static readonly KnownTriggerApp[] KnownTriggerWindowClasses =
    [
        new("NVIDIA overlay", "CEF-OSC-WIDGET", "NVIDIA Overlay"),
    ];

    public static QUERY_USER_NOTIFICATION_STATE? GetUserNotificationState()
    {
        // https://learn.microsoft.com/en-us/windows/win32/api/shellapi/ne-shellapi-query_user_notification_state
        return PInvoke.SHQueryUserNotificationState(out var state).Succeeded ? state : null;
    }

    internal static UserNotificationFlags GetUserNotificationFlags()
    {
        return GetUserNotificationFlags(GetUserNotificationState());
    }

    internal static UserNotificationFlags GetUserNotificationFlags(QUERY_USER_NOTIFICATION_STATE? state)
    {
        return new UserNotificationFlags(
            IsRunningD3DFullScreen: state is QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN,
            IsPresentationMode: state is QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE,
            IsBusy: state is QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY);
    }

    /// <summary>
    /// Returns the display names of known trigger apps that currently have visible
    /// windows matching their expected window class and process name.
    /// </summary>
    public static unsafe List<string> FindVisibleTriggerApps()
    {
        var detected = new HashSet<string>(StringComparer.Ordinal);

        PInvoke.EnumWindows(
            (HWND hWnd, LPARAM lParam) =>
            {
                if (!PInvoke.IsWindowVisible(hWnd))
                {
                    return true; // continue
                }

                var className = GetWindowClassName(hWnd);
                if (className is null)
                {
                    return true;
                }

                foreach (var app in KnownTriggerWindowClasses)
                {
                    if (!string.Equals(className, app.WindowClassName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Window class matches — verify the process name.
                    uint pid;
                    if (PInvoke.GetWindowThreadProcessId(hWnd, &pid) == 0)
                    {
                        continue;
                    }

                    try
                    {
                        using var process = Process.GetProcessById((int)pid);
                        if (string.Equals(process.ProcessName, app.ProcessName, StringComparison.OrdinalIgnoreCase))
                        {
                            detected.Add(app.DisplayName);
                        }
                    }
                    catch
                    {
                        // Process may have exited between enumeration and lookup.
                    }
                }

                return true;
            },
            0);

        return [.. detected];
    }

    private static unsafe string? GetWindowClassName(HWND hWnd)
    {
        const int maxLength = 256;
        fixed (char* buffer = new char[maxLength])
        {
            var length = PInvoke.GetClassName(hWnd, buffer, maxLength);
            return length > 0 ? new string(buffer, 0, length) : null;
        }
    }

    internal readonly record struct UserNotificationFlags(bool IsRunningD3DFullScreen, bool IsPresentationMode, bool IsBusy)
    {
        public bool IsFullscreenState => IsRunningD3DFullScreen || IsPresentationMode;
    }
}
