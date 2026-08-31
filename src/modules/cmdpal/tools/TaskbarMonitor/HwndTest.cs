// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace TaskbarMonitor;

/// <summary>
/// Creates a small test window, parents it to the taskbar, and prints
/// diagnostic information about the parenting/positioning/clipping.
/// Used with --hwnd flag to debug the parenting approach.
/// </summary>
internal static unsafe class HwndTest
{
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == PInvoke.WM_DESTROY)
        {
            PInvoke.PostQuitMessage(0);
            return (LRESULT)0;
        }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public static void Run()
    {
        var log = Console.Out;

        // Step 1: Find the taskbar hierarchy
        var shellTray = PInvoke.FindWindow("Shell_TrayWnd", null);
        log.WriteLine($"Shell_TrayWnd        = 0x{(nint)shellTray.Value:X}");

        if (shellTray.IsNull)
        {
            log.WriteLine("FATAL: Shell_TrayWnd not found");
            return;
        }

        var rebar = PInvoke.FindWindowEx(shellTray, HWND.Null, "ReBarWindow32", null);
        log.WriteLine($"  ReBarWindow32      = 0x{(nint)rebar.Value:X}");

        PInvoke.GetWindowRect(shellTray, out var taskbarRect);
        log.WriteLine($"Taskbar rect         = ({taskbarRect.left},{taskbarRect.top},{taskbarRect.right},{taskbarRect.bottom}) {taskbarRect.Width}x{taskbarRect.Height}");

        PInvoke.GetWindowRect(rebar, out var rebarRect);
        log.WriteLine($"ReBar rect           = ({rebarRect.left},{rebarRect.top},{rebarRect.right},{rebarRect.bottom}) {rebarRect.Width}x{rebarRect.Height}");

        var tray = PInvoke.FindWindowEx(shellTray, HWND.Null, "TrayNotifyWnd", null);
        PInvoke.GetWindowRect(tray, out var trayRect);
        log.WriteLine($"TrayNotifyWnd rect   = ({trayRect.left},{trayRect.top},{trayRect.right},{trayRect.bottom}) {trayRect.Width}x{trayRect.Height}");

        var dpi = PInvoke.GetDpiForWindow(shellTray);
        var scale = dpi / 96.0;
        log.WriteLine($"DPI={dpi} scale={scale:F2}x");

        // Get metrics
        using var poller = new TaskbarPoller();
        var snapshots = poller.PollAll();
        var primary = snapshots.FirstOrDefault(s => s.IsPrimary);
        if (primary == null)
        {
            log.WriteLine("FATAL: no primary taskbar found");
            return;
        }

        log.WriteLine($"Metrics: buttons={primary.ButtonsWidth}px tray={primary.TrayWidth}px count={primary.ButtonCount}");

        // Step 2: Create a test window — WS_CHILD from the start
        var hInstance = PInvoke.GetModuleHandle((PCWSTR)null);

        fixed (char* className = "TaskbarMonitor_TestWnd")
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &WndProc,
                hInstance = (HINSTANCE)hInstance.Value,
                lpszClassName = className,
                hbrBackground = PInvoke.CreateSolidBrush(new COLORREF(0x000000FF)), // Red (BGR)
            };

            PInvoke.RegisterClassEx(in wc);

            // Create as WS_CHILD of Shell_TrayWnd from the start
            var hwnd = PInvoke.CreateWindowEx(
                WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_TOPMOST,
                className,
                (PCWSTR)null,
                WINDOW_STYLE.WS_CHILD | WINDOW_STYLE.WS_VISIBLE,
                0, 0, taskbarRect.Width, taskbarRect.Height,
                shellTray,
                HMENU.Null,
                wc.hInstance,
                null);

            log.WriteLine($"\nCreated HWND = 0x{(nint)hwnd.Value:X}");

            // Position exactly like CmdPal does
            var x = taskbarRect.left;
            var y = rebarRect.top - taskbarRect.top;
            var w = taskbarRect.Width;
            var h = rebarRect.bottom - rebarRect.top;

            PInvoke.SetWindowRgn(hwnd, HRGN.Null, true);

            // HWND_TOPMOST = (HWND)-1
            var hwndTopmost = new HWND((void*)-1);

            PInvoke.SetWindowPos(
                hwnd,
                hwndTopmost,
                x, y, w, h,
                SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

            log.WriteLine($"Positioned: x={x} y={y} w={w} h={h}");
            DumpState(log, hwnd, "After initial position");

            // Step 3: Clip to the content area (between buttons and tray)
            var clipLeft = primary.ButtonsWidth;
            var clipRight = w - primary.TrayWidth;
            log.WriteLine($"\nClip: left={clipLeft} right={clipRight} (content area = {clipRight - clipLeft}px)");

            var hrgn = PInvoke.CreateRectRgn(clipLeft, 0, clipRight, h);
            PInvoke.SetWindowRgn(hwnd, hrgn, true);
            DumpState(log, hwnd, "After initial clip");

            // Step 4: Monitor window state over time. Skip re-measurement
            // of buttons (that uses Shell_TrayWnd scope which breaks once
            // we're parented). Just monitor visibility/rect/style.
            log.WriteLine("\n=== MONITORING (10 seconds) ===");
            log.WriteLine("Watching window state every 500ms...\n");

            for (var tick = 0; tick < 20; tick++)
            {
                Thread.Sleep(500);

                var curParent = PInvoke.GetParent(hwnd);
                var visible = PInvoke.IsWindowVisible(hwnd);
                PInvoke.GetWindowRect(hwnd, out var curRect);
                var style = (WINDOW_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);

                log.Write($"  t={(tick + 1) * 500}ms: ");
                log.Write($"vis={visible} ");
                log.Write($"parent=0x{(nint)curParent.Value:X} ");
                log.Write($"rect=({curRect.left},{curRect.top},{curRect.right},{curRect.bottom}) ");
                log.Write($"CHILD={((style & WINDOW_STYLE.WS_CHILD) != 0)} ");
                log.Write($"VIS={((style & WINDOW_STYLE.WS_VISIBLE) != 0)}");
                log.WriteLine();
            }

            log.WriteLine("\nPress Enter to exit...");
            Console.ReadLine();

            PInvoke.DestroyWindow(hwnd);
        }
    }

    private static void DumpState(TextWriter log, HWND hwnd, string label)
    {
        PInvoke.GetWindowRect(hwnd, out var rect);
        var parent = PInvoke.GetParent(hwnd);
        var visible = PInvoke.IsWindowVisible(hwnd);
        var style = (WINDOW_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        var exStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

        log.WriteLine($"  [{label}]");
        log.WriteLine($"    rect=({rect.left},{rect.top},{rect.right},{rect.bottom}) {rect.Width}x{rect.Height}");
        log.WriteLine($"    parent=0x{(nint)parent.Value:X} visible={visible}");
        log.WriteLine($"    WS_CHILD={((style & WINDOW_STYLE.WS_CHILD) != 0)} WS_POPUP={((style & WINDOW_STYLE.WS_POPUP) != 0)} WS_VISIBLE={((style & WINDOW_STYLE.WS_VISIBLE) != 0)}");
        log.WriteLine($"    exStyle=0x{(uint)exStyle:X8}");
    }
}
