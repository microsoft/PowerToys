using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

static class OSInterop
{
    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int smIndex);
    public const int SM_CMONITORS = 80;

    [DllImport("user32.dll")]
    public static extern bool SystemParametersInfo(int nAction, int nParam, ref RECT rc, int nUpdate);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool GetMonitorInfo(HandleRef hmonitor, [In, Out] MONITORINFOEX info);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(HandleRef handle, int flags);

    [DllImport("user32.dll")]
    public static extern bool ClipCursor(ref RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool ClipCursor([In()] IntPtr lpRect);

    public struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
        public int width { get { return right - left; } }
        public int height { get { return bottom - top; } }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Auto)]
    public class MONITORINFOEX
    {
        public int cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
        public RECT rcMonitor;
        public RECT rcWork;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public char[] szDevice = new char[32];
        public int dwFlags;
    }
}

static class WPFExtensionMethods
{
    public static Point GetAbsolutePosition(this Window w)
    {
        if (w.WindowState != WindowState.Maximized)
            return new Point(w.Left, w.Top);

        Int32Rect r;
        bool MultiMonitorSupported = OSInterop.GetSystemMetrics(OSInterop.SM_CMONITORS) != 0;
        if (!MultiMonitorSupported)
        {
            OSInterop.RECT rc = new OSInterop.RECT();
            OSInterop.SystemParametersInfo(48, 0, ref rc, 0);
            r = new Int32Rect(rc.left, rc.top, rc.width, rc.height);
        }
        else
        {
            WindowInteropHelper helper = new WindowInteropHelper(w);
            IntPtr hmonitor = OSInterop.MonitorFromWindow(new HandleRef(null, helper.EnsureHandle()), 2);
            OSInterop.MONITORINFOEX info = new OSInterop.MONITORINFOEX();
            OSInterop.GetMonitorInfo(new HandleRef(null, hmonitor), info);
            r = new Int32Rect(info.rcMonitor.left, info.rcMonitor.top, info.rcMonitor.width, info.rcMonitor.height);
        }
        return new Point(r.X, r.Y);
    }
}

internal static class NativeMethods
{
    // See http://msdn.microsoft.com/en-us/library/ms649021%28v=vs.85%29.aspx
    public const int WM_CLIPBOARDUPDATE = 0x031D;
    public static IntPtr HWND_MESSAGE = new IntPtr(-3);

    // See http://msdn.microsoft.com/en-us/library/ms632599%28VS.85%29.aspx#message_only
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AddClipboardFormatListener(IntPtr hwnd);
}