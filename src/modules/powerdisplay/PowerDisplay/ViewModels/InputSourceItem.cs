// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;

namespace PowerDisplay.ViewModels;

/// <summary>
/// Represents an input source option for display in UI
/// </summary>
public class InputSourceItem
{
    /// <summary>
    /// VCP value for this input source (e.g., 0x11 for HDMI-1)
    /// </summary>
    public int Value { get; set; }

    /// <summary>
    /// Human-readable name (e.g., "HDMI-1", "DisplayPort-1")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Visibility of selection indicator (Visible when selected)
    /// </summary>
    public Visibility SelectionVisibility { get; set; } = Visibility.Collapsed;

    /// <summary>
    /// Monitor ID for direct lookup (Flyout popup is not in visual tree)
    /// </summary>
    public string MonitorId { get; set; } = string.Empty;
}
