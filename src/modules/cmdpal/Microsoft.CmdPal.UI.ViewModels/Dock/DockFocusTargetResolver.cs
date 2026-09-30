// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

/// <summary>
/// Picks which monitor's dock should take focus when the dock hotkey fires.
/// </summary>
public static class DockFocusTargetResolver
{
    /// <summary>
    /// Resolves the stable monitor ID of the dock to focus.
    /// </summary>
    /// <param name="monitors">All currently connected monitors.</param>
    /// <param name="liveDockMonitorIds">Stable IDs of the monitors that actually have a dock window.</param>
    /// <param name="cursorX">Cursor X in virtual-screen coordinates.</param>
    /// <param name="cursorY">Cursor Y in virtual-screen coordinates.</param>
    /// <returns>The stable ID to focus, or <c>null</c> when no dock is running.</returns>
    public static string? Resolve(
        IReadOnlyList<MonitorInfo> monitors,
        IReadOnlyCollection<string> liveDockMonitorIds,
        int cursorX,
        int cursorY)
    {
        if (liveDockMonitorIds.Count == 0)
        {
            return null;
        }

        var cursorMonitor = FindMonitorContaining(monitors, cursorX, cursorY);
        if (cursorMonitor is not null && TryMatch(liveDockMonitorIds, cursorMonitor.StableId, out var onCursor))
        {
            return onCursor;
        }

        for (var i = 0; i < monitors.Count; i++)
        {
            if (monitors[i].IsPrimary && TryMatch(liveDockMonitorIds, monitors[i].StableId, out var onPrimary))
            {
                return onPrimary;
            }
        }

        foreach (var id in liveDockMonitorIds)
        {
            return id;
        }

        return null;
    }

    private static MonitorInfo? FindMonitorContaining(IReadOnlyList<MonitorInfo> monitors, int x, int y)
    {
        for (var i = 0; i < monitors.Count; i++)
        {
            var bounds = monitors[i].Bounds;
            if (x >= bounds.Left && x < bounds.Right && y >= bounds.Top && y < bounds.Bottom)
            {
                return monitors[i];
            }
        }

        return null;
    }

    private static bool TryMatch(IReadOnlyCollection<string> liveDockMonitorIds, string stableId, out string match)
    {
        foreach (var id in liveDockMonitorIds)
        {
            if (string.Equals(id, stableId, StringComparison.OrdinalIgnoreCase))
            {
                match = id;
                return true;
            }
        }

        match = string.Empty;
        return false;
    }
}
