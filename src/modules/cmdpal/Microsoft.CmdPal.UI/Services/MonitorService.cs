// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

using MonitorInfo = Microsoft.CmdPal.UI.ViewModels.Models.MonitorInfo;

namespace Microsoft.CmdPal.UI.Services;

/// <summary>
/// Enumerates connected display monitors using Win32 APIs.
/// Register as a singleton in DI; call <see cref="RefreshMonitors"/> when
/// WM_DISPLAYCHANGE is received.
/// </summary>
public sealed class MonitorService : IMonitorService
{
    private readonly object _lock = new();
    private List<MonitorInfo> _monitors = [];

    public event Action? MonitorsChanged;

    public MonitorService()
    {
        RefreshMonitors();
    }

    /// <summary>
    /// Re-enumerates all monitors. Call this from the WM_DISPLAYCHANGE handler.
    /// Fires <see cref="MonitorsChanged"/> if the set of monitors changed.
    /// </summary>
    public void RefreshMonitors()
    {
        var newMonitors = EnumerateMonitors();

        lock (_lock)
        {
            _monitors = newMonitors;
        }

        MonitorsChanged?.Invoke();
    }

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        lock (_lock)
        {
            return _monitors.AsReadOnly();
        }
    }

    public MonitorInfo? GetMonitorByDeviceId(string deviceId)
    {
        lock (_lock)
        {
            return _monitors.Find(m => string.Equals(m.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public MonitorInfo GetPrimaryMonitor()
    {
        lock (_lock)
        {
            return _monitors.Find(m => m.IsPrimary)
                ?? _monitors[0]; // Fallback: first monitor
        }
    }

    private static unsafe List<MonitorInfo> EnumerateMonitors()
    {
        var results = new List<MonitorInfo>();

        PInvoke.EnumDisplayMonitors(HDC.Null, (RECT*)null, EnumProc, 0);

        return results;

        BOOL EnumProc(HMONITOR hMonitor, HDC hdcMonitor, RECT* lprcMonitor, LPARAM dwData)
        {
            var info = default(MONITORINFOEXW);
            info.monitorInfo.cbSize = (uint)Marshal.SizeOf<MONITORINFOEXW>();

            if (PInvoke.GetMonitorInfo(hMonitor, ref info.monitorInfo))
            {
                var deviceName = info.szDevice.ToString();
                var isPrimary = (info.monitorInfo.dwFlags & PInvoke.MONITORINFOF_PRIMARY) != 0;

                // Get per-monitor DPI via the existing GetDpiForMonitor P/Invoke
                uint dpiX = 96;
                try
                {
                    PInvoke.GetDpiForMonitor(
                        hMonitor,
                        MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI,
                        out dpiX,
                        out _);
                }
                catch
                {
                    // Fallback to 96 DPI if the API isn't available
                }

                var bounds = info.monitorInfo.rcMonitor;
                var workArea = info.monitorInfo.rcWork;

                results.Add(new MonitorInfo
                {
                    DeviceId = deviceName,
                    DisplayName = FormatDisplayName(deviceName, isPrimary),
                    Bounds = new ScreenRect(bounds.left, bounds.top, bounds.right, bounds.bottom),
                    WorkArea = new ScreenRect(workArea.left, workArea.top, workArea.right, workArea.bottom),
                    Dpi = dpiX,
                    IsPrimary = isPrimary,
                });
            }

            return true;
        }
    }

    private static string FormatDisplayName(string deviceName, bool isPrimary)
    {
        // Extract display number from "\\.\DISPLAY1" → "Display 1"
        var name = deviceName.Replace(@"\\.\DISPLAY", "Display ", StringComparison.OrdinalIgnoreCase).Trim();
        if (isPrimary)
        {
            name += " (Primary)";
        }

        return name;
    }
}
