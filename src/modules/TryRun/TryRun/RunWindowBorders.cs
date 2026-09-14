// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun;

internal sealed class RunWindowBorders : IDisposable
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly Dictionary<nint, BorderWindow> borders = [];
    private readonly Action<string> reportFailure;
    private WindowsProcessTree? processes;
    private long nextProcessScan;
    private bool disposed;

    internal RunWindowBorders(Action<string> reportFailure)
    {
        this.reportFailure = reportFailure;
        timer.Tick += OnTick;
    }

    internal IReadOnlyDictionary<nint, BorderWindow> Borders => borders;

    internal void Start(WindowsProcessIdentity root)
    {
        if (disposed || processes is not null)
        {
            return;
        }

        processes = new WindowsProcessTree(root);
        Refresh();
        if (!disposed)
        {
            timer.Start();
        }
    }

    internal void Refresh()
    {
        if (disposed || processes is null)
        {
            return;
        }

        if (!processes.IsRunning)
        {
            Dispose();
            return;
        }

        try
        {
            if (Environment.TickCount64 >= nextProcessScan)
            {
                processes.Refresh();
                nextProcessScan = Environment.TickCount64 + 500;
            }

            var visible = new HashSet<nint>();
            WindowBorderNative.EnumWindows(
                (window, _) =>
                {
                    WindowBorderNative.GetWindowThreadProcessId(window, out var processId);
                    if (visible.Count < 32 && processes.Contains(processId) && WindowBorderNative.IsWindowVisible(window) && !WindowBorderNative.IsIconic(window) && WindowBorderNative.GetCloaked(window, 14, out var cloaked, sizeof(uint)) == 0 && cloaked == 0)
                    {
                        visible.Add(window);
                    }

                    return true;
                },
                0);

            foreach (var window in borders.Keys.Except(visible).ToArray())
            {
                borders[window].Close();
                borders.Remove(window);
            }

            foreach (var window in visible)
            {
                // Recheck ownership after enumeration. No title, executable name,
                // or workload-supplied text is used to decide which window to mark.
                WindowBorderNative.GetWindowThreadProcessId(window, out var processId);
                if (!processes.Contains(processId))
                {
                    continue;
                }

                if (!borders.TryGetValue(window, out var border))
                {
                    border = new BorderWindow();
                    borders.Add(window, border);
                }

                border.Follow(window);
            }
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or ExternalException)
        {
            Dispose();
            reportFailure("Window border unavailable: " + exception.Message);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        timer.Stop();
        timer.Tick -= OnTick;
        foreach (var border in borders.Values)
        {
            border.Close();
        }

        borders.Clear();
        processes?.Dispose();
        processes = null;
    }

    private void OnTick(object? sender, EventArgs e) => Refresh();

    internal sealed class BorderWindow : Window
    {
        private WindowBorderNative.Rectangle previousBounds;

        internal BorderWindow()
        {
            Title = "Try Run window border";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowActivated = false;
            ShowInTaskbar = false;
            Focusable = false;
            IsHitTestVisible = false;
            Width = 1;
            Height = 1;
            Content = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 183, 195)),
                BorderThickness = new Thickness(4),
                IsHitTestVisible = false,
                SnapsToDevicePixels = true,
            };

            Handle = new WindowInteropHelper(this).EnsureHandle();
            var style = WindowBorderNative.GetWindowLongPtrW(Handle, WindowBorderNative.ExtendedStyle).ToInt64();
            Marshal.SetLastPInvokeError(0);
            if (WindowBorderNative.SetWindowLongPtrW(Handle, WindowBorderNative.ExtendedStyle, (nint)(style | WindowBorderNative.Transparent | WindowBorderNative.ToolWindow | WindowBorderNative.NoActivate)) == 0 && Marshal.GetLastPInvokeError() != 0)
            {
                var error = Marshal.GetLastPInvokeError();
                Close();
                throw new Win32Exception(error);
            }

            HwndSource.FromHwnd(Handle)!.AddHook(HandleMessage);
        }

        internal nint Handle { get; }

        internal void Follow(nint target)
        {
            if (WindowBorderNative.GetFrameBounds(target, 9, out var bounds, 16) != 0 && !WindowBorderNative.GetWindowRect(target, out bounds))
            {
                Hide();
                return;
            }

            var width = bounds.Right - bounds.Left;
            var height = bounds.Bottom - bounds.Top;
            if (width <= 0 || height <= 0)
            {
                Hide();
                return;
            }

            // Place inside the visible frame so maximized windows keep all four
            // edges on screen. Native coordinates are physical pixels; WPF scales
            // the stroke for the monitor's DPI.
            var changed = !IsVisible || !bounds.Equals(previousBounds);
            if (!IsVisible)
            {
                Show();
            }

            var targetIsTopmost = IsTopmost(target);
            if (IsTopmost(Handle) != targetIsTopmost)
            {
                // Match the target's z-order band, without activating either
                // window. HWND_TOP alone cannot promote an ordinary overlay.
                if (!WindowBorderNative.SetWindowPos(Handle, targetIsTopmost ? -1 : -2, 0, 0, 0, 0, WindowBorderNative.NoActivation | 0x0003))
                {
                    Hide();
                    return;
                }
            }

            // Immediately above the target, underneath whatever covers it.
            // A globally topmost overlay would incorrectly frame unrelated apps.
            var aboveTarget = WindowBorderNative.GetWindow(target, 3); // GW_HWNDPREV
            if (changed || aboveTarget != Handle)
            {
                var flags = WindowBorderNative.NoActivation | (aboveTarget == Handle ? 0x0004u : 0); // SWP_NOZORDER
                if (!WindowBorderNative.SetWindowPos(Handle, aboveTarget == Handle ? 0 : aboveTarget, bounds.Left, bounds.Top, width, height, flags))
                {
                    Hide();
                    return;
                }

                previousBounds = bounds;
            }
        }

        private static bool IsTopmost(nint window) => (WindowBorderNative.GetWindowLongPtrW(window, WindowBorderNative.ExtendedStyle).ToInt64() & 8) != 0;

        private static nint HandleMessage(nint window, int message, nint wParam, nint lParam, ref bool handled)
        {
            // WM_NCHITTEST / WM_MOUSEACTIVATE
            if (message is 0x0084 or 0x0021)
            {
                handled = true;
                return message == 0x0084 ? -1 : 3; // HTTRANSPARENT / MA_NOACTIVATE
            }

            return 0;
        }
    }
}
