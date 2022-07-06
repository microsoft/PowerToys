using System;
using System.Runtime.InteropServices;

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
        public int width => right - left;
        public int height => bottom - top;
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
