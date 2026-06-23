// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.ViewModels;

/// <summary>
/// Represents a color temperature preset option for display in UI
/// </summary>
public class ColorTemperatureItem
{
    /// <summary>
    /// VCP value for this color temperature preset (e.g., 0x05 for 6500K)
    /// </summary>
    public int VcpValue { get; set; }

    /// <summary>
    /// Human-readable name (e.g., "6500K", "sRGB", "User 1")
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this preset is currently selected
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Monitor ID for direct lookup (Flyout popup is not in visual tree)
    /// </summary>
    public string MonitorId { get; set; } = string.Empty;
}
