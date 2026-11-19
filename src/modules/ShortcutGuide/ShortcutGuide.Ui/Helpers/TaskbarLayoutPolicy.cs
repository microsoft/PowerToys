// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.Graphics;

namespace ShortcutGuide.Helpers;

internal static class TaskbarLayoutPolicy
{
    // Ignore the small overlap exposed by a retracted auto-hide taskbar; only
    // treat it as revealed once its overlap exceeds this scaled boundary.
    private const int TaskbarVisibilityThresholdDip = 4;

    // Keep the taskbar activation edge outside the overlay so the pointer can
    // still reach it while the taskbar is retracted. This is independent of
    // the visibility threshold above: one controls classification, the other
    // reserves physical screen space.
    private const int TaskbarSensorMarginDip = 4;

    /// <summary>
    /// Calculates taskbar visibility and the usable monitor area for an auto-hidden
    /// taskbar.
    /// </summary>
    /// <param name="monitor">The monitor bounds in physical pixels.</param>
    /// <param name="edge">The monitor edge occupied by the taskbar.</param>
    /// <param name="dpiScale">The monitor's scale factor, used to convert policy
    /// distances from DIPs to pixels.</param>
    /// <param name="overlapWidth">The taskbar's horizontal overlap with the monitor in
    /// physical pixels.</param>
    /// <param name="overlapHeight">The taskbar's vertical overlap with the monitor in
    /// physical pixels.</param>
    /// <returns>The visibility classification and usable area for the overlay.</returns>
    internal static AutoHideTaskbarLayout CalculateAutoHideLayout(
        RectInt32 monitor,
        TaskbarEdge edge,
        float dpiScale,
        int overlapWidth,
        int overlapHeight)
    {
        int overlap = edge is TaskbarEdge.Top or TaskbarEdge.Bottom ? overlapHeight : overlapWidth;
        bool isTaskbarVisible = overlap > (int)Math.Ceiling(TaskbarVisibilityThresholdDip * dpiScale);
        var usableArea = isTaskbarVisible
            ? GetVisibleTaskbarUsableArea(monitor, edge, overlapWidth, overlapHeight)
            : GetAutoHideSensorUsableArea(monitor, edge, dpiScale);

        return new AutoHideTaskbarLayout(isTaskbarVisible, usableArea);
    }

    /// <summary>
    /// Selects the monitor from the layout, falling back to the monitor containing the
    /// overlay when needed.
    /// </summary>
    /// <param name="layoutMonitor">The monitor handle stored in the current layout, or
    /// zero if unavailable.</param>
    /// <param name="overlayMonitor">The monitor handle containing the overlay.</param>
    /// <returns>The layout monitor when available; otherwise, the overlay monitor.
    /// </returns>
    internal static nint ResolveMonitor(nint layoutMonitor, nint overlayMonitor) =>
        layoutMonitor != IntPtr.Zero ? layoutMonitor : overlayMonitor;

    private static RectInt32 GetVisibleTaskbarUsableArea(
        RectInt32 monitor, TaskbarEdge edge, int overlapWidth, int overlapHeight) => edge switch
        {
            TaskbarEdge.Bottom => new RectInt32(monitor.X, monitor.Y, monitor.Width, Math.Max(0, monitor.Height - overlapHeight)),
            TaskbarEdge.Top => new RectInt32(monitor.X, monitor.Y + overlapHeight, monitor.Width, Math.Max(0, monitor.Height - overlapHeight)),
            TaskbarEdge.Left => new RectInt32(monitor.X + overlapWidth, monitor.Y, Math.Max(0, monitor.Width - overlapWidth), monitor.Height),
            TaskbarEdge.Right => new RectInt32(monitor.X, monitor.Y, Math.Max(0, monitor.Width - overlapWidth), monitor.Height),
            _ => monitor,
        };

    private static RectInt32 GetAutoHideSensorUsableArea(RectInt32 monitor, TaskbarEdge edge, float dpiScale)
    {
        int margin = (int)Math.Ceiling(TaskbarSensorMarginDip * dpiScale);
        return edge switch
        {
            TaskbarEdge.Bottom => new RectInt32(monitor.X, monitor.Y, monitor.Width, Math.Max(0, monitor.Height - margin)),
            TaskbarEdge.Top => new RectInt32(monitor.X, monitor.Y + margin, monitor.Width, Math.Max(0, monitor.Height - margin)),
            TaskbarEdge.Left => new RectInt32(monitor.X + margin, monitor.Y, Math.Max(0, monitor.Width - margin), monitor.Height),
            TaskbarEdge.Right => new RectInt32(monitor.X, monitor.Y, Math.Max(0, monitor.Width - margin), monitor.Height),
            _ => monitor,
        };
    }
}

internal readonly record struct AutoHideTaskbarLayout(bool IsTaskbarVisible, RectInt32 UsableArea);
