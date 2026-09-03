// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>Preset window sizes for <see cref="WindowHelper.SetWindowSize(IntPtr, WindowSize)"/>.</summary>
public enum WindowSize
{
    /// <summary>No size change.</summary>
    UnSpecified,

    /// <summary>640 x 480.</summary>
    Small,

    /// <summary>480 x 640.</summary>
    Small_Vertical,

    /// <summary>1024 x 768.</summary>
    Medium,

    /// <summary>768 x 1024.</summary>
    Medium_Vertical,

    /// <summary>1920 x 1080.</summary>
    Large,

    /// <summary>1080 x 1920.</summary>
    Large_Vertical,
}

/// <summary>
/// Win32 window + screen helpers for scenarios winappcli can't express: resizing/positioning a
/// window, reading a screen pixel color, and querying display geometry. Window discovery itself
/// stays CLI-first (<see cref="WindowsFinder"/>; <see cref="IsWindowOpen"/>).
/// </summary>
public static class WindowHelper
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOPMOST = 0x00000008L;
    private const long WS_EX_LAYERED = 0x00080000L;
    private const uint LWA_ALPHA = 0x00000002;
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const int SW_MAXIMIZE = 3;
    private const int SW_RESTORE = 9;
    private const int SW_MINIMIZE = 6;
    private const int DwmCloakedAttribute = 14;
    private const int DwmExtendedFrameBoundsAttribute = 9;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern IntPtr GetPropW(IntPtr hWnd, string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLayeredWindowAttributes(IntPtr hWnd, out uint crKey, out byte bAlpha, out uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern uint GetPixel(IntPtr hdc, int x, int y);

    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    private static extern int DwmGetWindowIntAttribute(IntPtr hWnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    private static extern int DwmGetWindowRectAttribute(IntPtr hWnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();

    /// <summary>True when any UIA-visible window's title contains <paramref name="titleContains"/> (CLI-based).</summary>
    public static bool IsWindowOpen(string titleContains) =>
        WindowsFinder.ListAll().Any(w => w.Title.Contains(titleContains, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Resize a window to a preset <see cref="WindowSize"/> and CENTER it on the primary display.
    /// The preset is first clamped to ~90% of the display, so a fixed size (e.g. Large = 1920x1080)
    /// can't spill off the edges of an equally-sized (1920x1080) display once positioned at a
    /// non-origin top-left — the cause of the "shifted right and bottom, partially off-screen"
    /// Settings window. On a larger display the preset size is used as-is, just centered.
    /// </summary>
    public static void SetWindowSize(IntPtr hWnd, WindowSize size)
    {
        var (w, h) = Dimensions(size);
        if (w <= 0 || h <= 0)
        {
            return;
        }

        var (screenW, screenH) = GetDisplaySize();

        // Clamp to ~90% of the screen so there's always a visible margin on every edge.
        int cw = screenW > 0 ? Math.Min(w, (int)(screenW * 0.9)) : w;
        int ch = screenH > 0 ? Math.Min(h, (int)(screenH * 0.9)) : h;

        // Center on the primary display (never negative, so the title bar stays reachable).
        int x = Math.Max(0, (screenW - cw) / 2);
        int y = Math.Max(0, (screenH - ch) / 2);

        SetWindowPos(hWnd, IntPtr.Zero, x, y, cw, ch, SWP_NOZORDER | SWP_NOACTIVATE);
    }

    /// <summary>Resize a window to explicit width/height, keeping its current position (no move).</summary>
    public static void SetMainWindowSize(IntPtr hWnd, int width, int height) =>
        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, width, height, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);

    /// <summary>Move a window to explicit screen coordinates while preserving its current size.</summary>
    public static void MoveWindow(IntPtr hWnd, int x, int y) =>
        SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

    /// <summary>
    /// Maximize a window so it fills the monitor work area and is fully on-screen. Used as the default
    /// window state for tests so a module's restored (possibly small or off-screen) last window rect
    /// can't hide controls such as the Settings NavigationView pane.
    /// </summary>
    public static void MaximizeWindow(IntPtr hWnd) => ShowWindow(hWnd, SW_MAXIMIZE);

    /// <summary>
    /// Restore a window from maximized/minimized. Needed before positioning a window that a test will
    /// then drag: <c>SetWindowPos</c> can move and size a maximized window without clearing its
    /// maximized state, and dragging such a window makes Windows restore it mid-gesture instead of
    /// performing a plain move.
    /// </summary>
    public static void RestoreWindow(IntPtr hWnd) => ShowWindow(hWnd, SW_RESTORE);

    /// <summary>Minimize a window, e.g. to get it out of the way of an on-screen pixel measurement.</summary>
    public static void MinimizeWindow(IntPtr hWnd) => ShowWindow(hWnd, SW_MINIMIZE);

    /// <summary>(Left, Top, Right, Bottom) of the window in screen pixels.</summary>
    public static (int Left, int Top, int Right, int Bottom) GetWindowBounds(IntPtr hWnd)
    {
        if (GetWindowRect(hWnd, out var r))
        {
            return (r.Left, r.Top, r.Right, r.Bottom);
        }

        return (0, 0, 0, 0);
    }

    /// <summary>
    /// Whether DWM currently cloaks a window. Cloaking is independent of Win32 visibility and is
    /// commonly used to keep a WinUI window rendered while hiding its composed output.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The DWM query failed, including when <paramref name="hWnd"/> was destroyed during the query.
    /// Callers polling a replaceable window should treat this exception as transient and reacquire
    /// the current HWND before retrying.
    /// </exception>
    public static bool IsWindowCloaked(IntPtr hWnd) =>
        IsWindowCloaked(hWnd, QueryCloakedState);

    internal static bool IsWindowCloaked(
        IntPtr hWnd,
        Func<IntPtr, (int HResult, int CloakedState)> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (hResult, cloakedState) = query(hWnd);
        ThrowIfDwmQueryFailed(hWnd, "DWMWA_CLOAKED", hResult);
        return cloakedState != 0;
    }

    /// <summary>
    /// DWM extended-frame bounds (Left, Top, Right, Bottom) in physical screen pixels. Unlike
    /// <see cref="GetWindowBounds"/>, these bounds exclude invisible resize borders and are not
    /// DPI-virtualized.
    /// </summary>
    /// <remarks>
    /// Compare these bounds with monitor geometry only from a per-monitor-DPI-aware test host.
    /// Every UITestAutomation.Next test executable should embed the framework's PerMonitorV2
    /// application manifest.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The DWM query failed or returned an empty frame, including when <paramref name="hWnd"/> was
    /// destroyed during the query. Callers polling a replaceable window should treat this exception
    /// as transient and reacquire the current HWND before retrying.
    /// </exception>
    public static (int Left, int Top, int Right, int Bottom) GetVisibleBounds(IntPtr hWnd) =>
        GetVisibleBounds(hWnd, QueryVisibleBounds);

    internal static (int Left, int Top, int Right, int Bottom) GetVisibleBounds(
        IntPtr hWnd,
        Func<IntPtr, (int HResult, (int Left, int Top, int Right, int Bottom) Bounds)> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (hResult, bounds) = query(hWnd);
        ThrowIfDwmQueryFailed(hWnd, "DWMWA_EXTENDED_FRAME_BOUNDS", hResult);
        if (bounds.Right <= bounds.Left || bounds.Bottom <= bounds.Top)
        {
            throw new InvalidOperationException(
                $"DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS) returned invalid bounds " +
                $"({bounds.Left},{bounds.Top})-({bounds.Right},{bounds.Bottom}) for HWND {FormatHandle(hWnd)}.");
        }

        return bounds;
    }

    /// <summary>Read a named Win32 property stamped on a window, or zero when it is absent.</summary>
    public static long GetWindowPropertyValue(IntPtr hWnd, string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        return GetPropW(hWnd, propertyName).ToInt64();
    }

    /// <summary>
    /// Capture the visible DWM frame from the screen. Unlike PrintWindow, this includes composed
    /// WinUI/WebView content; unlike a raw GetWindowRect capture, it excludes invisible resize borders.
    /// </summary>
    public static void CaptureVisibleWindow(IntPtr hWnd, string outputPath)
    {
        var bounds = GetVisibleBounds(hWnd);

        var wasTopmost = (GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64() & WS_EX_TOPMOST) != 0;
        var noMoveOrResize = SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE;
        var topmost = new IntPtr(-1);
        var notTopmost = new IntPtr(-2);

        try
        {
            SetWindowPos(hWnd, topmost, 0, 0, 0, 0, noMoveOrResize);
            DwmFlush();

            using var bitmap = new Bitmap(bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(
                bounds.Left,
                bounds.Top,
                0,
                0,
                bitmap.Size,
                CopyPixelOperation.SourceCopy);
            bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        }
        finally
        {
            if (!wasTopmost)
            {
                SetWindowPos(hWnd, notTopmost, 0, 0, 0, 0, noMoveOrResize);
                DwmFlush();
            }
        }
    }

    private static (int HResult, int CloakedState) QueryCloakedState(IntPtr hWnd)
    {
        var hResult = DwmGetWindowIntAttribute(
            hWnd,
            DwmCloakedAttribute,
            out var cloakedState,
            sizeof(int));
        return (hResult, cloakedState);
    }

    private static (int HResult, (int Left, int Top, int Right, int Bottom) Bounds) QueryVisibleBounds(IntPtr hWnd)
    {
        var hResult = DwmGetWindowRectAttribute(
            hWnd,
            DwmExtendedFrameBoundsAttribute,
            out var bounds,
            Marshal.SizeOf<RECT>());
        return (hResult, (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
    }

    private static void ThrowIfDwmQueryFailed(IntPtr hWnd, string attributeName, int hResult)
    {
        if (hResult != 0)
        {
            throw new InvalidOperationException(
                $"DwmGetWindowAttribute({attributeName}) failed for HWND {FormatHandle(hWnd)} " +
                $"with HRESULT 0x{hResult:X8}.");
        }
    }

    private static string FormatHandle(IntPtr hWnd) => $"0x{hWnd.ToInt64():X}";

    /// <summary>Center point of the window in screen pixels.</summary>
    public static (int CenterX, int CenterY) GetWindowCenter(IntPtr hWnd)
    {
        var (l, t, rgt, b) = GetWindowBounds(hWnd);
        return (l + ((rgt - l) / 2), t + ((b - t) / 2));
    }

    /// <summary>Primary display size in pixels.</summary>
    public static (int Width, int Height) GetDisplaySize() =>
        (GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));

    /// <summary>Center of the primary display in pixels.</summary>
    public static (int CenterX, int CenterY) GetScreenCenter()
    {
        var (w, h) = GetDisplaySize();
        return (w / 2, h / 2);
    }

    /// <summary>Color of the on-screen pixel at (<paramref name="x"/>, <paramref name="y"/>) via GDI.</summary>
    public static Color GetPixelColor(int x, int y)
    {
        var hdc = GetDC(IntPtr.Zero);
        try
        {
            var pixel = GetPixel(hdc, x, y);
            int r = (int)(pixel & 0x000000FF);
            int g = (int)((pixel & 0x0000FF00) >> 8);
            int b = (int)((pixel & 0x00FF0000) >> 16);
            return Color.FromArgb(r, g, b);
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, hdc);
        }
    }

    /// <summary>On-screen pixel color at (<paramref name="x"/>, <paramref name="y"/>) as <c>#RRGGBB</c>.</summary>
    public static string GetPixelColorHex(int x, int y)
    {
        var c = GetPixelColor(x, y);
        return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    /// <summary>
    /// Alpha of a layered window, or 255 when it is not alpha-blended. Lets a test observe a module
    /// that fades a window (FancyZones' "make the dragged window transparent") without sampling
    /// pixels.
    /// </summary>
    public static byte GetWindowAlpha(IntPtr hWnd)
    {
        if ((GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64() & WS_EX_LAYERED) == 0)
        {
            return 255;
        }

        return GetLayeredWindowAttributes(hWnd, out _, out var alpha, out var flags) && (flags & LWA_ALPHA) != 0
            ? alpha
            : (byte)255;
    }

    private static (int Width, int Height) Dimensions(WindowSize size) => size switch
    {
        WindowSize.Small => (640, 480),
        WindowSize.Small_Vertical => (480, 640),
        WindowSize.Medium => (1024, 768),
        WindowSize.Medium_Vertical => (768, 1024),
        WindowSize.Large => (1920, 1080),
        WindowSize.Large_Vertical => (1080, 1920),
        _ => (0, 0),
    };
}
