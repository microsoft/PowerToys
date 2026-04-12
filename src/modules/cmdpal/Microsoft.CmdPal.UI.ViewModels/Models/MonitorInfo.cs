// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Represents a physical display monitor connected to the system.
/// </summary>
public sealed record MonitorInfo
{
    /// <summary>
    /// Gets the device identifier (e.g. <c>\\.\DISPLAY1</c>).
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Gets the human-readable display name (e.g. <c>DELL U2723QE</c>).
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the full monitor rectangle in virtual-screen coordinates.
    /// </summary>
    public required ScreenRect Bounds { get; init; }

    /// <summary>
    /// Gets the work area (excludes the taskbar) in virtual-screen coordinates.
    /// </summary>
    public required ScreenRect WorkArea { get; init; }

    /// <summary>
    /// Gets the DPI value for this monitor (e.g. 96, 120, 144, 192).
    /// </summary>
    public required uint Dpi { get; init; }

    /// <summary>
    /// Gets a value indicating whether this is the primary monitor.
    /// </summary>
    public required bool IsPrimary { get; init; }

    /// <summary>
    /// Gets the scale factor for this monitor (e.g. 1.0 = 100%, 1.5 = 150%).
    /// </summary>
    [JsonIgnore]
    public double ScaleFactor => Dpi / 96.0;
}
