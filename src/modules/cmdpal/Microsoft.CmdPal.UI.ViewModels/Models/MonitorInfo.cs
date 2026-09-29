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
    /// Gets the GDI device name (e.g. <c>\\.\DISPLAY1</c>).
    /// This is volatile and may change across reboots or plug/unplug events.
    /// Use <see cref="StableId"/> for persistent identification.
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Gets a stable hardware identifier derived from the Display Configuration API
    /// device path (e.g. <c>\\?\DISPLAY#GSM1388#4&amp;125707d6&amp;0&amp;UID8388688#{guid}</c>).
    /// Unlike <see cref="DeviceId"/>, this value survives reboots, driver updates,
    /// and plug/unplug events on the same GPU port. Falls back to <see cref="DeviceId"/>
    /// when the Display Configuration API is unavailable.
    /// </summary>
    public required string StableId { get; init; }

    /// <summary>
    /// Gets an EDID-derived identifier for the physical panel (e.g. <c>DEL-41A3-ABC123</c>),
    /// built by <see cref="EdidIdentity"/>. Unlike <see cref="StableId"/>, it follows the
    /// monitor across ports, docks, and GPUs. <c>null</c> when the EDID is unavailable or
    /// has no usable serial number.
    /// </summary>
    public string? HardwareId { get; init; }

    /// <summary>
    /// Gets the friendly hardware name without a primary-monitor suffix.
    /// Can be empty or <c>null</c> when the display does not provide one.
    /// </summary>
    public string? FriendlyName { get; init; }

    /// <summary>
    /// Gets the runtime display name (e.g. <c>DELL U2723QE</c>).
    /// Its fallback includes the current GDI display number, which may change.
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
