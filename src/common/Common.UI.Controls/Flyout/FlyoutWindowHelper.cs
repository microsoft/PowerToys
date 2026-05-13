// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinUIEx;

namespace Microsoft.PowerToys.Common.UI.Controls.Flyout;

/// <summary>
/// Shared helper for positioning and sizing flyout-style WinUI 3 windows
/// (e.g. Quick Access, PowerDisplay) that are pinned to a corner of the work area.
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

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out POINT lpPoint);

    [LibraryImport("shcore.dll")]
    private static partial int GetDpiForMonitor(nint hMonitor, uint dpiType, out uint dpiX, out uint dpiY);

    /// <summary>
    /// Get the DPI scale factor (1.0 = 100%, 1.25 = 125%, 1.5 = 150%, 2.0 = 200%) for a window.
    /// </summary>
    public static double GetDpiScale(WindowEx window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return (double)window.GetDpiForWindow() / DefaultDpi;
    }

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
    /// Two-step move that avoids WM_DPICHANGED double-scaling. First teleports a 1×1
    /// window into the target display (which may trigger an auto-rescale, but on a 1×1
    /// rect the effect is invisible). Then sets the real position+size while the window
    /// is already on the target monitor — no DPI boundary crossing, so WinUI's auto
    /// handler doesn't fire and overwrite our computed rect.
    ///
    /// Skips the teleport when the window is already on the target display, since there
    /// is no boundary to cross.
    /// </summary>
    private static void MoveAndResizeOnDisplay(WindowEx window, DisplayArea targetDisplay, RectInt32 finalRect)
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
