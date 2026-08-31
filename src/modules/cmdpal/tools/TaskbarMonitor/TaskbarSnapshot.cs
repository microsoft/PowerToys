// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace TaskbarMonitor;

/// <summary>
/// Immutable snapshot of taskbar metrics at a point in time.
/// </summary>
public sealed record TaskbarSnapshot
{
    /// <summary>Whether this is the primary taskbar (Shell_TrayWnd) or a secondary one.</summary>
    public bool IsPrimary { get; init; }

    public int TaskbarWidth { get; init; }

    public int TaskbarHeight { get; init; }

    public bool IsBottom { get; init; }

    /// <summary>
    /// Total width occupied by taskbar buttons (pixels). Only meaningful when <see cref="IsBottom"/>.
    /// </summary>
    public int ButtonsWidth { get; init; }

    /// <summary>
    /// Width of the notification/tray area (pixels). Only meaningful when <see cref="IsBottom"/>.
    /// </summary>
    public int TrayWidth { get; init; }

    /// <summary>
    /// DPI for the monitor this taskbar lives on.
    /// </summary>
    public uint Dpi { get; init; }

    /// <summary>
    /// DPI scale factor (Dpi / 96.0).
    /// </summary>
    public double ScaleFactor => Dpi / 96.0;

    /// <summary>
    /// Number of top-level child windows found in the button list.
    /// </summary>
    public int ButtonCount { get; init; }
}
