// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class MonitorHelper
{
    /// <summary>
    /// Gets the display area based on the specified monitor behavior.
    /// </summary>
    private static DisplayArea GetScreen(HWND hwnd, MonitorBehavior target)
    {
        // Leaving a note here, in case we ever need it:
        // https://github.com/microsoft/microsoft-ui-xaml/issues/6454
        // If we need to ever FindAll, we'll need to iterate manually
        // var displayAreas = Microsoft.UI.Windowing.DisplayArea.FindAll();
        switch (target)
        {
            case MonitorBehavior.InPlace:
                if (PInvoke.GetWindowRect(hwnd, out var bounds))
                {
                    RectInt32 converted = new(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                    return DisplayArea.GetFromRect(converted, DisplayAreaFallback.Nearest);
                }

                break;

            case MonitorBehavior.ToFocusedWindow:
                var foregroundWindowHandle = PInvoke.GetForegroundWindow();
                if (foregroundWindowHandle != IntPtr.Zero)
                {
                    if (PInvoke.GetWindowRect(foregroundWindowHandle, out var fgBounds))
                    {
                        RectInt32 converted = new(fgBounds.X, fgBounds.Y, fgBounds.Width, fgBounds.Height);
                        return DisplayArea.GetFromRect(converted, DisplayAreaFallback.Nearest);
                    }
                }

                break;

            case MonitorBehavior.ToPrimary:
                return DisplayArea.Primary;

            case MonitorBehavior.ToMouse:
            default:
                if (PInvoke.GetCursorPos(out var cursorPos))
                {
                    return DisplayArea.GetFromPoint(new PointInt32(cursorPos.X, cursorPos.Y), DisplayAreaFallback.Nearest);
                }

                break;
        }

        return DisplayArea.Primary;
    }

    internal static void PositionCentered(HWND hwnd, AppWindow appWindow, MonitorBehavior monitorBehavior)
    {
        var displayArea = GetScreen(hwnd, monitorBehavior);
        PositionCentered(appWindow, displayArea);
    }

    internal static void PositionCentered(AppWindow appWindow, DisplayAreaFallback displayAreaFallback = DisplayAreaFallback.Nearest)
    {
        var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, displayAreaFallback);
        PositionCentered(appWindow, displayArea);
    }

    private static void PositionCentered(AppWindow appWindow, DisplayArea displayArea)
    {
        if (displayArea is not null)
        {
            var centeredPosition = appWindow.Position;
            centeredPosition.X = (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;
            centeredPosition.Y = (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;

            centeredPosition.X += displayArea.WorkArea.X;
            centeredPosition.Y += displayArea.WorkArea.Y;
            appWindow.Move(centeredPosition);
        }
    }

    /// <summary>
    /// Ensures that the window rectangle is visible on-screen.
    /// </summary>
    /// <param name="windowRect">The window rectangle in physical pixels.</param>
    /// <param name="originalScreen">The desktop area the window was positioned on.</param>
    /// <param name="originalDpi">The window's original DPI.</param>
    /// <param name="defaultWidth">Default window width.</param>
    /// <param name="defaultHeight">Default window height.</param>
    /// <returns>
    /// A window rectangle in physical pixels, moved to the nearest display and resized
    /// if the DPI has changed.
    /// </returns>
    public static RectInt32 EnsureWindowIsVisible(RectInt32 windowRect, SizeInt32 originalScreen, int originalDpi, int defaultWidth, int defaultHeight)
    {
        var displayArea = DisplayArea.GetFromRect(windowRect, DisplayAreaFallback.Nearest);
        if (displayArea is null)
        {
            return windowRect;
        }

        var workArea = displayArea.WorkArea;
        if (workArea.Width <= 0 || workArea.Height <= 0)
        {
            // Fallback, nothing reasonable to do
            return windowRect;
        }

        var effectiveDpi = GetEffectiveDpiFromDisplayId(displayArea);
        if (originalDpi <= 0)
        {
            originalDpi = effectiveDpi; // use current DPI as baseline (no scaling adjustment needed)
        }

        var hasInvalidSize = windowRect.Width <= 0 || windowRect.Height <= 0;
        if (hasInvalidSize)
        {
            windowRect = new RectInt32(windowRect.X, windowRect.Y, defaultWidth, defaultHeight);
        }

        // If we have a DPI change, scale the window rectangle accordingly
        if (effectiveDpi != originalDpi)
        {
            var scalingFactor = effectiveDpi / (double)originalDpi;
            windowRect = new RectInt32(
                (int)Math.Round(windowRect.X * scalingFactor),
                (int)Math.Round(windowRect.Y * scalingFactor),
                (int)Math.Round(windowRect.Width * scalingFactor),
                (int)Math.Round(windowRect.Height * scalingFactor));
        }

        var targetWidth = Math.Min(windowRect.Width, workArea.Width);
        var targetHeight = Math.Min(windowRect.Height, workArea.Height);

        // Ensure at least some minimum visible area (e.g., 100 pixels)
        // This helps prevent the window from being entirely offscreen, regardless of display scaling.
        const int minimumVisibleSize = 100;
        var isOffscreen =
            windowRect.X + minimumVisibleSize > workArea.X + workArea.Width ||
            windowRect.X + windowRect.Width - minimumVisibleSize < workArea.X ||
            windowRect.Y + minimumVisibleSize > workArea.Y + workArea.Height ||
            windowRect.Y + windowRect.Height - minimumVisibleSize < workArea.Y;

        // if the work area size has changed, re-center the window
        var workAreaSizeChanged =
            originalScreen.Width != workArea.Width ||
            originalScreen.Height != workArea.Height;

        int targetX;
        int targetY;
        var recenter = isOffscreen || workAreaSizeChanged || hasInvalidSize;
        if (recenter)
        {
            targetX = workArea.X + ((workArea.Width - targetWidth) / 2);
            targetY = workArea.Y + ((workArea.Height - targetHeight) / 2);
        }
        else
        {
            targetX = windowRect.X;
            targetY = windowRect.Y;
        }

        return new RectInt32(targetX, targetY, targetWidth, targetHeight);
    }

    private static int GetEffectiveDpiFromDisplayId(DisplayArea displayArea)
    {
        var effectiveDpi = 96;

        var hMonitor = (HMONITOR)Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);
        if (!hMonitor.IsNull)
        {
            var hr = PInvoke.GetDpiForMonitor(hMonitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out _);
            if (hr == 0)
            {
                effectiveDpi = (int)dpiX;
            }
            else
            {
                Logger.LogWarning($"GetDpiForMonitor failed with HRESULT: 0x{hr.Value:X8} on display {displayArea.DisplayId}");
            }
        }

        if (effectiveDpi <= 0)
        {
            effectiveDpi = 96;
        }

        return effectiveDpi;
    }
}
