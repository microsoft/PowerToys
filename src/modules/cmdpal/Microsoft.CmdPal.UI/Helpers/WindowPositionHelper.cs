// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class WindowPositionHelper
{
    private const int DefaultWidth = 800;
    private const int DefaultHeight = 480;
    private const int MinimumVisibleSize = 100;
    private const int DefaultDpi = 96;

    public static PointInt32? CalculateCenteredPosition(DisplayArea? displayArea, SizeInt32 windowSize, int windowDpi)
    {
        if (displayArea is null)
        {
            return null;
        }

        var workArea = displayArea.WorkArea;
        if (workArea.Width <= 0 || workArea.Height <= 0)
        {
            return null;
        }

        var targetDpi = GetDpiForDisplay(displayArea);
        var predictedSize = ScaleSize(windowSize, windowDpi, targetDpi);

        // Clamp to work area
        var width = Math.Min(predictedSize.Width, workArea.Width);
        var height = Math.Min(predictedSize.Height, workArea.Height);

        return new PointInt32(
            workArea.X + ((workArea.Width - width) / 2),
            workArea.Y + ((workArea.Height - height) / 2));
    }

    /// <summary>
    /// Adjusts a saved window rect to ensure it's visible on the nearest display,
    /// accounting for DPI changes and work area differences.
    /// </summary>
    ///
    public static RectInt32 AdjustRectForVisibility(RectInt32 savedRect, SizeInt32 savedScreenSize, int savedDpi)
    {
        var displayArea = DisplayArea.GetFromRect(savedRect, DisplayAreaFallback.Nearest);
        if (displayArea is null)
        {
            return savedRect;
        }

        var workArea = displayArea.WorkArea;
        if (workArea.Width <= 0 || workArea.Height <= 0)
        {
            return savedRect;
        }

        var targetDpi = GetDpiForDisplay(displayArea);
        if (savedDpi <= 0)
        {
            savedDpi = targetDpi;
        }

        var hasInvalidSize = savedRect.Width <= 0 || savedRect.Height <= 0;
        if (hasInvalidSize)
        {
            savedRect = savedRect with { Width = DefaultWidth, Height = DefaultHeight };
        }

        if (targetDpi != savedDpi)
        {
            savedRect = ScaleRect(savedRect, savedDpi, targetDpi);
        }

        var clampedSize = ClampSize(savedRect.Width, savedRect.Height, workArea);

        var shouldRecenter = hasInvalidSize ||
                             IsOffscreen(savedRect, workArea) ||
                             savedScreenSize.Width != workArea.Width ||
                             savedScreenSize.Height != workArea.Height;

        if (shouldRecenter)
        {
            return CenterRectInWorkArea(clampedSize, workArea);
        }

        return new RectInt32(savedRect.X, savedRect.Y, clampedSize.Width, clampedSize.Height);
    }

    private static int GetDpiForDisplay(DisplayArea displayArea)
    {
        var hMonitor = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);
        if (hMonitor == IntPtr.Zero)
        {
            return DefaultDpi;
        }

        var hr = PInvoke.GetDpiForMonitor(
            new HMONITOR(hMonitor),
            MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI,
            out var dpiX,
            out _);

        return hr.Succeeded && dpiX > 0 ? (int)dpiX : DefaultDpi;
    }

    private static SizeInt32 ScaleSize(SizeInt32 size, int fromDpi, int toDpi)
    {
        if (fromDpi <= 0 || toDpi <= 0 || fromDpi == toDpi)
        {
            return size;
        }

        var scale = (double)toDpi / fromDpi;
        return new SizeInt32(
            (int)Math.Round(size.Width * scale),
            (int)Math.Round(size.Height * scale));
    }

    private static RectInt32 ScaleRect(RectInt32 rect, int fromDpi, int toDpi)
    {
        var scale = (double)toDpi / fromDpi;
        return new RectInt32(
            (int)Math.Round(rect.X * scale),
            (int)Math.Round(rect.Y * scale),
            (int)Math.Round(rect.Width * scale),
            (int)Math.Round(rect.Height * scale));
    }

    private static SizeInt32 ClampSize(int width, int height, RectInt32 workArea) =>
        new(Math.Min(width, workArea.Width), Math.Min(height, workArea.Height));

    private static RectInt32 CenterRectInWorkArea(SizeInt32 size, RectInt32 workArea) =>
        new(
            workArea.X + ((workArea.Width - size.Width) / 2),
            workArea.Y + ((workArea.Height - size.Height) / 2),
            size.Width,
            size.Height);

    private static bool IsOffscreen(RectInt32 rect, RectInt32 workArea) =>
        rect.X + MinimumVisibleSize > workArea.X + workArea.Width ||
        rect.X + rect.Width - MinimumVisibleSize < workArea.X ||
        rect.Y + MinimumVisibleSize > workArea.Y + workArea.Height ||
        rect.Y + rect.Height - MinimumVisibleSize < workArea.Y;
}
