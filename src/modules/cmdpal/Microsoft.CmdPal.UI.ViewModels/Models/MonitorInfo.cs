// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Represents a physical display monitor with its bounds and DPI.
/// Coordinates are in virtual screen pixels (absolute, not relative to primary).
/// </summary>
public sealed record MonitorInfo
{
    /// <summary>
    /// Gets the device identifier string (e.g. "\\.\DISPLAY1"). Survives reboots
    /// and uniquely identifies a physical display output.
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Gets a human-readable display name (e.g. "DELL U2723QE" or "Display 1").
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the full monitor rectangle in virtual-screen pixels.
    /// </summary>
    public required ScreenRect Bounds { get; init; }

    /// <summary>
    /// Gets the work area (excludes taskbar/app bars) in virtual-screen pixels.
    /// </summary>
    public required ScreenRect WorkArea { get; init; }

    /// <summary>
    /// Gets the DPI for this monitor (e.g. 96, 120, 144, 192).
    /// </summary>
    public required uint Dpi { get; init; }

    /// <summary>
    /// Gets a value indicating whether this is the primary monitor.
    /// </summary>
    public required bool IsPrimary { get; init; }

    /// <summary>
    /// Gets the scale factor relative to 96 DPI (1.0 = 100%, 1.5 = 150%, 2.0 = 200%).
    /// </summary>
    public double ScaleFactor => Dpi / 96.0;
}

/// <summary>
/// A simple rectangle in virtual-screen pixel coordinates.
/// Avoids dependency on platform-specific RECT types in the ViewModel layer.
/// </summary>
public readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;
}
