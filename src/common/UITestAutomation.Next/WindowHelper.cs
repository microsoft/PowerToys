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
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern uint GetPixel(IntPtr hdc, int x, int y);

    /// <summary>True when any UIA-visible window's title contains <paramref name="titleContains"/> (CLI-based).</summary>
    public static bool IsWindowOpen(string titleContains) =>
        WindowsFinder.ListAll().Any(w => w.Title.Contains(titleContains, StringComparison.OrdinalIgnoreCase));

    /// <summary>Resize a window to a preset <see cref="WindowSize"/> (keeps its current position).</summary>
    public static void SetWindowSize(IntPtr hWnd, WindowSize size)
    {
        var (w, h) = Dimensions(size);
        if (w > 0 && h > 0)
        {
            SetMainWindowSize(hWnd, w, h);
        }
    }

    /// <summary>Resize a window to explicit width/height (keeps its current position).</summary>
    public static void SetMainWindowSize(IntPtr hWnd, int width, int height) =>
        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, width, height, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);

    /// <summary>(Left, Top, Right, Bottom) of the window in screen pixels.</summary>
    public static (int Left, int Top, int Right, int Bottom) GetWindowBounds(IntPtr hWnd)
    {
        if (GetWindowRect(hWnd, out var r))
        {
            return (r.Left, r.Top, r.Right, r.Bottom);
        }

        return (0, 0, 0, 0);
    }

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
