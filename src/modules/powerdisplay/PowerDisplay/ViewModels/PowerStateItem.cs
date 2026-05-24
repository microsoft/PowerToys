// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;

namespace PowerDisplay.ViewModels;

/// <summary>
/// Represents a power state option for display in UI.
/// VCP 0xD6 values: 0x01=On, 0x02=Standby, 0x03=Suspend, 0x04=Off(DPM), 0x05=Off(Hard)
/// </summary>
public class PowerStateItem
{
    /// <summary>
    /// VCP power mode value representing On state
    /// </summary>
    public const int PowerStateOn = 0x01;

    /// <summary>
    /// VCP value for this power state
    /// </summary>
    public int Value { get; set; }

    /// <summary>
    /// Human-readable name (e.g., "On", "Standby", "Off (DPM)")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this power state is currently selected.
    /// Set based on monitor's actual power state during list creation.
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Visibility of selection indicator (Visible when IsSelected is true)
    /// </summary>
    public Visibility SelectionVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Monitor ID for direct lookup (Flyout popup is not in visual tree)
    /// </summary>
    public string MonitorId { get; set; } = string.Empty;
}
