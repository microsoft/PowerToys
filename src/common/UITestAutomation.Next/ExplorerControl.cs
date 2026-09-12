// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>Explorer window and Shell lifetimes, independent of a module's registration or selection policy.</summary>
public static class ExplorerControl
{
    public const string ProcessName = "explorer";
    public const string FileWindowClassName = "CabinetWClass";
    public const string TaskbarWindowClassName = "Shell_TrayWnd";

    public static bool IsFileWindow(WindowsFinder.WindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return window.ClassName.Equals(FileWindowClassName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Close Explorer file windows without terminating the Shell or anything launched from it.</summary>
    public static bool CloseFileWindows(int timeoutMS = 10_000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMS);
        return WindowControl.TryCloseByApp(ProcessName, IsFileWindow, timeoutMS);
    }

    /// <summary>
    /// Open a filesystem folder or Shell parsing name in a fresh Explorer window. The caller owns
    /// closing old windows, registration, selection, foreground, and sizing.
    /// </summary>
    public static Session? OpenFolder(string folderPath, int timeoutMS = 30_000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMS);
        var startInfo = CreateStartInfo(folderPath);
        var previousHandles = WindowsFinder.ListByApp(ProcessName)
            .Where(IsFileWindow)
            .Select(window => window.Hwnd)
            .ToHashSet();
        using var process = Process.Start(startInfo);
        return WindowsFinder.WaitForWindowByApp(
            ProcessName,
            window => IsFileWindow(window) && !previousHandles.Contains(window.Hwnd),
            timeoutMS: timeoutMS);
    }

    /// <summary>Find a replacement file window, optionally restricted to a filesystem folder's leaf name.</summary>
    public static Session? FindReplacementWindow(Session previous, string? folderPath = null)
    {
        ArgumentNullException.ThrowIfNull(previous);
        var replacement = SelectReplacementWindow(
            WindowsFinder.ListByApp(ProcessName),
            previous.WindowHandle,
            WindowControl.GetForegroundWindowHandle().ToInt64(),
            folderPath);
        return replacement is null ? null : CreateSession(replacement, previous.InitScope);
    }

    /// <summary>
    /// Restart only Explorer processes in the current interactive session and require a fresh taskbar
    /// PID. There is no global once flag: callers retain their module/class registration lifetime.
    /// </summary>
    public static bool RestartShell(int timeoutMS = 30_000, Action<string>? log = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMS);
        log ??= message => Console.WriteLine($"[Explorer] {message}");
        using var currentProcess = Process.GetCurrentProcess();
        var sessionId = currentProcess.SessionId;
        if (sessionId == 0)
        {
            log("Shell restart requires an interactive session, not session 0.");
            return false;
        }

        var deadline = Stopwatch.StartNew();
        var previousIds = new HashSet<int>();
        foreach (var process in Process.GetProcessesByName(ProcessName))
        {
            using (process)
            {
                try
                {
                    if (process.SessionId == sessionId)
                    {
                        previousIds.Add(process.Id);
                    }
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
                {
                    log($"Could not inspect Explorer PID {process.Id}: {ex.Message}");
                }
            }
        }

        foreach (var id in previousIds)
        {
            try
            {
                using var process = Process.GetProcessById(id);
                if (process.SessionId != sessionId || !process.ProcessName.Equals(ProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    log($"PID {id} no longer belongs to this session's Explorer; leaving it untouched.");
                    continue;
                }

                if (!process.HasExited)
                {
                    // Explorer may have launched the test host, debugger, or other user processes.
                    process.Kill(entireProcessTree: false);
                }

                var remaining = Math.Min(10_000, timeoutMS - (int)deadline.ElapsedMilliseconds);
                if (remaining <= 0 || !process.WaitForExit(remaining))
                {
                    log($"Explorer PID {id} did not exit within the restart budget.");
                    return false;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
            {
                log($"Explorer PID {id} exited or became unavailable during restart: {ex.Message}");
            }
        }

        var remainingTimeout = timeoutMS - (int)deadline.ElapsedMilliseconds;
        if (remainingTimeout <= 0)
        {
            log("The Explorer restart budget expired before taskbar readiness.");
            return false;
        }

        var ready = WaitHelper.WaitForStable(
            observe: () => WindowsFinder.ListByApp(ProcessName).FirstOrDefault(window => IsFreshTaskbar(window, previousIds)),
            isMatch: window => window is not null,
            timeoutMS: remainingTimeout,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100);
        log(ready.Succeeded
            ? $"Explorer taskbar is ready in fresh PID {ready.LastObservation!.ProcessId}."
            : "Explorer did not expose a taskbar in a fresh process within the restart budget.");
        return ready.Succeeded;
    }

    internal static ProcessStartInfo CreateStartInfo(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        if (folderPath.Contains('"'))
        {
            throw new ArgumentException("An Explorer folder or Shell parsing name must not contain quotation marks.", nameof(folderPath));
        }

        // Preserve Explorer's /n,"path" grammar, including spaces and Shell namespace targets.
        return new ProcessStartInfo("explorer.exe", $"/n,\"{folderPath}\"") { UseShellExecute = true };
    }

    internal static WindowsFinder.WindowInfo? SelectReplacementWindow(
        IReadOnlyList<WindowsFinder.WindowInfo> windows,
        long previousHandle,
        long foregroundHandle,
        string? folderPath)
    {
        var folderName = folderPath is null ? null : Path.GetFileName(Path.TrimEndingDirectorySeparator(folderPath));
        return windows
            .Where(IsFileWindow)
            .Where(window => window.Hwnd != previousHandle)
            .Where(window => folderName is null || window.Title.Contains(folderName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(window => window.Hwnd == foregroundHandle)
            .FirstOrDefault();
    }

    internal static bool IsFreshTaskbar(WindowsFinder.WindowInfo window, IReadOnlySet<int> previousIds) =>
        window.Hwnd != 0 &&
        window.Width > 0 &&
        window.Height > 0 &&
        window.ProcessName.Equals(ProcessName, StringComparison.OrdinalIgnoreCase) &&
        window.ClassName.Equals(TaskbarWindowClassName, StringComparison.OrdinalIgnoreCase) &&
        !previousIds.Contains(window.ProcessId);

    internal static Session CreateSession(WindowsFinder.WindowInfo window, PowerToysModule scope = PowerToysModule.Runner) =>
        new(scope, window.Hwnd, window.Title, window.ProcessId, window.ProcessName);
}
