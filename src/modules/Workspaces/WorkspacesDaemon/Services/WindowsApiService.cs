// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using PowerToys.WorkspacesMCP.Models;

namespace PowerToys.WorkspacesMCP.Services;

public class WindowsApiService
{
    // Win32 API Constants
    private const int SWHIDE = 0;
    private const int SWMAXIMIZE = 3;
    private const int SWMINIMIZE = 6;
    private const int SWRESTORE = 9;
    private const int SWSHOW = 5;

    // Win32 API Imports
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr extraData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, [Out] System.Text.StringBuilder text, int count);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, [Out] System.Text.StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("psapi.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, [Out] System.Text.StringBuilder fileName, [In][MarshalAs(UnmanagedType.U4)] int size);

    // Delegates
    private delegate bool EnumWindowsCallback(IntPtr hWnd, IntPtr lParam);

    // Structures
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public async Task<List<WindowInfo>> GetAllWindowsAsync()
    {
        var windows = new List<WindowInfo>();

        await Task.Run(() =>
        {
            EnumWindows(
                (hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd))
                {
                    var windowInfo = GetWindowInfo(hWnd);
                    if (windowInfo != null && !string.IsNullOrWhiteSpace(windowInfo.Title))
                    {
                        windows.Add(windowInfo);
                    }
                }

                return true;
            },
                IntPtr.Zero);
        });

        return windows;
    }

    public WindowInfo? GetWindowInfo(IntPtr hWnd)
    {
        try
        {
            // Get window title
            var titleBuilder = new System.Text.StringBuilder(256);
            int titleLength = GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
            var title = titleLength > 0 ? titleBuilder.ToString() : string.Empty;

            // Get class name
            var classNameBuilder = new System.Text.StringBuilder(256);
            int classNameLength = GetClassName(hWnd, classNameBuilder, classNameBuilder.Capacity);
            var className = classNameLength > 0 ? classNameBuilder.ToString() : string.Empty;

            // Get process ID
            uint threadId = GetWindowThreadProcessId(hWnd, out uint processId);
            if (threadId == 0)
            {
                return null; // Failed to get process ID
            }

            // Get window bounds
            if (!GetWindowRect(hWnd, out RECT rect))
            {
                return null; // Failed to get window bounds
            }

            var bounds = new WindowBounds
            {
                X = rect.Left,
                Y = rect.Top,
                Width = rect.Right - rect.Left,
                Height = rect.Bottom - rect.Top,
            };

            return new WindowInfo
            {
                Hwnd = hWnd.ToInt64(),
                ProcessId = processId,
                Title = title,
                ClassName = className,
                Bounds = bounds,
                IsVisible = IsWindowVisible(hWnd),
                IsMinimized = IsIconic(hWnd),
                IsMaximized = IsZoomed(hWnd),
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<AppInfo>> GetRunningApplicationsAsync()
    {
        var apps = new Dictionary<uint, AppInfo>();
        var windows = await GetAllWindowsAsync();

        foreach (var window in windows)
        {
            if (!apps.ContainsKey(window.ProcessId))
            {
                var executablePath = GetProcessExecutablePath(window.ProcessId);
                var appName = GetAppNameFromPath(executablePath);

                apps[window.ProcessId] = new AppInfo
                {
                    Name = appName,
                    ProcessId = window.ProcessId,
                    ExecutablePath = executablePath,
                    IsRunning = true,
                    Windows = Array.Empty<WindowInfo>(),
                };
            }
        }

        // Group windows by process
        foreach (var app in apps.Values)
        {
            var appWindows = windows.Where(w => w.ProcessId == app.ProcessId).ToArray();
            apps[app.ProcessId] = app with { Windows = appWindows };
        }

        return apps.Values.ToList();
    }

    public bool IsApplicationRunning(string appName)
    {
        try
        {
            var processes = System.Diagnostics.Process.GetProcesses();
            return processes.Any(p =>
                string.Equals(p.ProcessName, appName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    System.IO.Path.GetFileNameWithoutExtension(GetProcessExecutablePath((uint)p.Id)),
                    appName,
                    StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    public List<WindowInfo> FindWindowsByTitle(string titlePattern)
    {
        var result = new List<WindowInfo>();

        EnumWindows(
            (hWnd, lParam) =>
        {
            if (IsWindowVisible(hWnd))
            {
                var windowInfo = GetWindowInfo(hWnd);
                if (windowInfo != null &&
                    windowInfo.Title.Contains(titlePattern, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(windowInfo);
                }
            }

            return true;
        },
            IntPtr.Zero);

        return result;
    }

    public List<WindowInfo> FindWindowsByClassName(string className)
    {
        var result = new List<WindowInfo>();

        EnumWindows(
            (hWnd, lParam) =>
        {
            if (IsWindowVisible(hWnd))
            {
                var windowInfo = GetWindowInfo(hWnd);
                if (windowInfo != null &&
                    string.Equals(windowInfo.ClassName, className, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(windowInfo);
                }
            }

            return true;
        },
            IntPtr.Zero);
        return result;
    }

    private string GetProcessExecutablePath(uint processId)
    {
        try
        {
            const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
            var hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);

            if (hProcess != IntPtr.Zero)
            {
                var fileNameBuilder = new System.Text.StringBuilder(1024);
                if (GetModuleFileNameEx(hProcess, IntPtr.Zero, fileNameBuilder, fileNameBuilder.Capacity) > 0)
                {
                    CloseHandle(hProcess);
                    return fileNameBuilder.ToString();
                }

                CloseHandle(hProcess);
            }
        }
        catch
        {
            // Fallback to Process class
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById((int)processId);
                return process.MainModule?.FileName ?? string.Empty;
            }
            catch
            {
                // Ignore
            }
        }

        return string.Empty;
    }

    private string GetAppNameFromPath(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return "Unknown";
        }

        return System.IO.Path.GetFileNameWithoutExtension(executablePath);
    }
}
