// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinUIEx;

namespace Microsoft.PowerToys.Common.UI.Controls.Window;

/// <summary>
/// Shared helper for positioning and sizing flyout-style WinUI 3 windows
/// (e.g. Quick Access, PowerDisplay) near an anchor or at a corner of the work area.
///
/// The public API takes sizes in device-independent pixels (DIP). The helper resolves the
/// target monitor's effective DPI and converts to physical pixels. All window positioning
/// uses absolute screen physical-pixel coordinates via
/// <see cref="AppWindow.MoveAndResize(RectInt32)"/> — the same pattern used by the original
/// Settings.UI flyout, which proved reliable across multi-monitor and mixed-DPI setups.
/// </summary>
public static partial class FlyoutWindowHelper
{
    private const uint MdtEffectiveDpi = 0;
    private const int DefaultDpi = 96;
    private const uint AbmGetTaskbarPos = 0x00000005;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public uint CbSize;
        public nint HWnd;
        public uint CallbackMessage;
        public uint Edge;
        public NativeRect Rect;
        public nint LParam;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out POINT lpPoint);

    [LibraryImport("shcore.dll")]
    private static partial int GetDpiForMonitor(nint hMonitor, uint dpiType, out uint dpiX, out uint dpiY);

    [LibraryImport("shell32.dll", EntryPoint = "SHAppBarMessage")]
    private static partial nuint SHAppBarMessage(uint message, ref AppBarData data);

    /// <summary>
    /// Get the DPI scale factor for a given <see cref="DisplayArea"/>.
    /// Resolves DPI from the underlying monitor handle so the value reflects the
    /// target display, regardless of which monitor the window is currently on.
    /// </summary>
    public static double GetDpiScale(DisplayArea displayArea)
    {
        ArgumentNullException.ThrowIfNull(displayArea);
        return (double)GetEffectiveDpi(global::Microsoft.UI.Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId)) / DefaultDpi;
    }

    /// <summary>
    /// Convert device-independent pixels (DIP) to physical pixels (rounding up).
    /// </summary>
    public static int ScaleToPhysicalPixels(int dip, double dpiScale)
    {
        return (int)Math.Ceiling(dip * dpiScale);
    }

    /// <summary>
    /// Convert physical pixels to device-independent pixels (DIP) (rounding down).
    /// </summary>
    public static int ScaleToDip(int physicalPixels, double dpiScale)
    {
        return (int)Math.Floor(physicalPixels / dpiScale);
    }

    /// <summary>
    /// Look up the <see cref="DisplayArea"/> currently containing the mouse cursor.
    /// </summary>
    public static bool TryGetDisplayAreaAtCursor(out DisplayArea? displayArea)
    {
        displayArea = null;

        if (!GetCursorPos(out var cursorPos))
        {
            return false;
        }

        displayArea = DisplayArea.GetFromPoint(new PointInt32(cursorPos.X, cursorPos.Y), DisplayAreaFallback.Nearest);
        return displayArea is not null;
    }

    /// <summary>
    /// Capture placement at a notification icon or point anchor without sampling the cursor.
    /// </summary>
    public static bool TryCreatePlacementAtAnchor(RectInt32 anchor, out FlyoutPlacementContext? context)
    {
        context = null;
        var displayArea = GetDisplayAreaForAnchor(anchor);
        return displayArea is not null && TryCreatePlacement(displayArea, anchor, out context);
    }

    /// <summary>
    /// Capture the cursor's display once, using the notification icon only if it is on that display.
    /// Otherwise use a point at the tray end of that display's taskbar edge. Falls back to the primary
    /// display if the cursor cannot be queried.
    /// </summary>
    public static bool TryCreatePlacementAtCursor(RectInt32? trayAnchor, out FlyoutPlacementContext? context)
    {
        context = null;
        var displayArea = GetCursorPos(out var cursor)
            ? DisplayArea.GetFromPoint(new PointInt32(cursor.X, cursor.Y), DisplayAreaFallback.Nearest)
            : DisplayArea.Primary;
        if (displayArea is null)
        {
            return false;
        }

        var work = displayArea.WorkArea;
        var edge = GetTaskbarEdge(displayArea);
        RectInt32 anchor = GetWorkAreaEdgeAnchor(work, edge);
        if (trayAnchor is RectInt32 candidate &&
            GetDisplayAreaForAnchor(candidate)?.DisplayId.Value == displayArea.DisplayId.Value)
        {
            anchor = candidate;
        }

        return TryCreatePlacement(displayArea, anchor, edge, out context);
    }

    /// <summary>
    /// Check that the captured display geometry is still current without sampling the cursor
    /// or choosing a different anchor.
    /// </summary>
    public static bool IsPlacementCurrent(FlyoutPlacementContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var displayArea = DisplayArea.GetFromDisplayId(new global::Microsoft.UI.DisplayId { Value = context.DisplayIdValue });
        return displayArea is not null &&
            displayArea.OuterBounds == context.OuterBounds &&
            displayArea.WorkArea == context.WorkArea &&
            GetDpiScale(displayArea) == context.DpiScale &&
            GetTaskbarEdge(displayArea) == context.TaskbarEdge;
    }

    /// <summary>
    /// Position a flyout using its captured anchor and display geometry. The caller can use
    /// <see cref="IsPlacementCurrent"/> to invalidate a presentation after display geometry changes.
    /// </summary>
    /// <returns>False if the target display is gone or no visible window size is available.</returns>
    public static bool PositionWindowNearAnchor(
        WindowEx window,
        FlyoutPlacementContext context,
        int widthDip,
        int heightDip,
        int gapDip = 12)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(context);

        var displayArea = DisplayArea.GetFromDisplayId(new global::Microsoft.UI.DisplayId { Value = context.DisplayIdValue });
        if (displayArea is null)
        {
            return false;
        }

        var rect = FlyoutPlacementCalculator.Calculate(
            context,
            ScaleToPhysicalPixels(widthDip, context.DpiScale),
            ScaleToPhysicalPixels(heightDip, context.DpiScale),
            ScaleToPhysicalPixels(gapDip, context.DpiScale));
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return false;
        }

        MoveAndResizeOnDisplay(window, displayArea, rect);
        return true;
    }

    /// <summary>
    /// Position a flyout-style window at the bottom-right corner of the work area on the
    /// monitor under the mouse cursor.
    /// </summary>
    public static void PositionWindowBottomRight(
        WindowEx window,
        int widthDip,
        int heightDip,
        int rightMarginDip = 0,
        int bottomMarginDip = 0)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (!TryGetDisplayAreaAtCursor(out var displayArea) || displayArea is null)
        {
            Logger.LogWarning("FlyoutWindowHelper.PositionWindowBottomRight: unable to determine display from cursor; skipping positioning");
            return;
        }

        PositionWindowBottomRight(window, displayArea, widthDip, heightDip, rightMarginDip, bottomMarginDip);
    }

    /// <summary>
    /// Position a flyout-style window at the bottom-right corner of the specified display
    /// area's work area. Use this overload when the caller has already resolved the target
    /// <see cref="DisplayArea"/> (e.g. the cursor monitor) so size and placement are computed
    /// from the same source.
    ///
    /// Internally moves the window in two steps to avoid <c>WM_DPICHANGED</c> double-scaling
    /// when the target monitor has a different DPI than the one the window was previously on:
    /// first a 1×1 teleport into the target display, then the real position+size while the
    /// window is already on that monitor (no DPI boundary crossing).
    /// </summary>
    public static void PositionWindowBottomRight(
        WindowEx window,
        DisplayArea displayArea,
        int widthDip,
        int heightDip,
        int rightMarginDip = 0,
        int bottomMarginDip = 0)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(displayArea);

        double dpiScale = GetDpiScale(displayArea);
        var work = displayArea.WorkArea;

        int w = ScaleToPhysicalPixels(widthDip, dpiScale);
        int h = ScaleToPhysicalPixels(heightDip, dpiScale);
        int marginRight = ScaleToPhysicalPixels(rightMarginDip, dpiScale);
        int marginBottom = ScaleToPhysicalPixels(bottomMarginDip, dpiScale);

        // Clamp size so the window never extends past the work area minus margins.
        // Guards against the bottom/right edge spilling into the taskbar when rounding
        // (Math.Ceiling above) would push it just past the boundary.
        int maxW = Math.Max(0, work.Width - marginRight);
        int maxH = Math.Max(0, work.Height - marginBottom);
        w = Math.Min(w, maxW);
        h = Math.Min(h, maxH);

        // Absolute screen physical-pixel coordinates. WorkArea is in screen coordinates,
        // so for non-primary monitors WorkArea.X/Y will be non-zero (and may be negative).
        int x = work.X + work.Width - w - marginRight;
        int y = work.Y + work.Height - h - marginBottom;

        MoveAndResizeOnDisplay(window, displayArea, new RectInt32(x, y, w, h));
    }

    /// <summary>
    /// Center a window within the specified display area's work area.
    /// Uses a 1×1 teleport into the target display first to avoid WM_DPICHANGED
    /// double-scaling when crossing monitors with different DPI.
    /// </summary>
    public static void CenterWindowOnDisplay(
        WindowEx window,
        DisplayArea displayArea,
        int widthDip,
        int heightDip)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(displayArea);

        double dpiScale = GetDpiScale(displayArea);
        var work = displayArea.WorkArea;

        int w = Math.Min(ScaleToPhysicalPixels(widthDip, dpiScale), work.Width);
        int h = Math.Min(ScaleToPhysicalPixels(heightDip, dpiScale), work.Height);

        int x = work.X + ((work.Width - w) / 2);
        int y = work.Y + ((work.Height - h) / 2);

        MoveAndResizeOnDisplay(window, displayArea, new RectInt32(x, y, w, h));
    }

    /// <summary>
    /// Move and resize <paramref name="window"/> to <paramref name="finalRect"/> (absolute
    /// screen physical-pixel coordinates) on <paramref name="targetDisplay"/>. Performs a
    /// two-step move that avoids WM_DPICHANGED double-scaling: first a 1×1 teleport into the
    /// target display (invisible at that size), then the real position+size while the window
    /// is already on that monitor. Skips the teleport when already on the target display.
    /// </summary>
    public static void MoveAndResizeOnDisplay(WindowEx window, DisplayArea targetDisplay, RectInt32 finalRect)
    {
        var currentDisplay = DisplayArea.GetFromWindowId(window.AppWindow.Id, DisplayAreaFallback.Nearest);
        bool needsTeleport = currentDisplay is null || currentDisplay.DisplayId.Value != targetDisplay.DisplayId.Value;

        if (needsTeleport)
        {
            var work = targetDisplay.WorkArea;
            window.AppWindow.MoveAndResize(new RectInt32(work.X, work.Y, 1, 1));
        }

        window.AppWindow.MoveAndResize(finalRect);
    }

    private static DisplayArea? GetDisplayAreaForAnchor(RectInt32 anchor)
    {
        if (anchor.Width < 0 || anchor.Height < 0)
        {
            return null;
        }

        return anchor.Width == 0 || anchor.Height == 0
            ? DisplayArea.GetFromPoint(new PointInt32(anchor.X, anchor.Y), DisplayAreaFallback.Nearest)
            : DisplayArea.GetFromRect(anchor, DisplayAreaFallback.Nearest);
    }

    private static bool TryCreatePlacement(DisplayArea displayArea, RectInt32 anchor, out FlyoutPlacementContext? context)
    {
        return TryCreatePlacement(displayArea, anchor, GetTaskbarEdge(displayArea), out context);
    }

    private static bool TryCreatePlacement(
        DisplayArea displayArea,
        RectInt32 anchor,
        FlyoutPlacementEdge edge,
        out FlyoutPlacementContext? context)
    {
        context = null;
        var outer = displayArea.OuterBounds;
        var work = displayArea.WorkArea;
        double dpiScale = GetDpiScale(displayArea);
        if (outer.Width <= 0 || outer.Height <= 0 || work.Width <= 0 || work.Height <= 0 ||
            !double.IsFinite(dpiScale) || dpiScale <= 0)
        {
            return false;
        }

        context = new FlyoutPlacementContext(displayArea.DisplayId.Value, anchor, outer, work, dpiScale, edge);
        return true;
    }

    private static RectInt32 GetWorkAreaEdgeAnchor(RectInt32 work, FlyoutPlacementEdge edge)
    {
        return edge switch
        {
            FlyoutPlacementEdge.Top => new RectInt32(work.X + work.Width, work.Y, 0, 0),
            FlyoutPlacementEdge.Left => new RectInt32(work.X, work.Y + work.Height, 0, 0),
            _ => new RectInt32(work.X + work.Width, work.Y + work.Height, 0, 0),
        };
    }

    private static FlyoutPlacementEdge GetTaskbarEdge(DisplayArea displayArea)
    {
        var data = new AppBarData
        {
            CbSize = (uint)Marshal.SizeOf<AppBarData>(),
        };

        // ABM_GETTASKBARPOS returns the system taskbar rectangle. Do not apply a primary-display
        // rectangle to another display; use that display's work area instead.
        if (SHAppBarMessage(AbmGetTaskbarPos, ref data) != 0)
        {
            long width = (long)data.Rect.Right - data.Rect.Left;
            long height = (long)data.Rect.Bottom - data.Rect.Top;
            if (width > 0 && width <= int.MaxValue && height > 0 && height <= int.MaxValue)
            {
                var taskbar = new RectInt32(data.Rect.Left, data.Rect.Top, (int)width, (int)height);
                var taskbarDisplay = DisplayArea.GetFromRect(taskbar, DisplayAreaFallback.Nearest);
                if (taskbarDisplay?.DisplayId.Value == displayArea.DisplayId.Value)
                {
                    return FlyoutPlacementCalculator.GetTaskbarEdge(displayArea.OuterBounds, displayArea.WorkArea, taskbar);
                }
            }
        }

        return FlyoutPlacementCalculator.GetTaskbarEdge(displayArea.OuterBounds, displayArea.WorkArea, null);
    }

    private static int GetEffectiveDpi(nint hMonitor)
    {
        if (hMonitor == 0)
        {
            return DefaultDpi;
        }

        var hr = GetDpiForMonitor(hMonitor, MdtEffectiveDpi, out var dpiX, out _);
        return hr >= 0 && dpiX > 0 ? (int)dpiX : DefaultDpi;
    }
}
