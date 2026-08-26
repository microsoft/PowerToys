// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Models;

/// <summary>
/// Defines how PowerDisplay handles mouse-wheel input.
/// </summary>
public enum MouseWheelControlMode
{
    /// <summary>
    /// Disables tray-icon and flyout-slider mouse-wheel adjustment.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// Enables flyout-slider adjustment and targets the primary display from the tray icon.
    /// </summary>
    PrimaryDisplay = 1,

    /// <summary>
    /// Enables flyout-slider adjustment and targets all visible displays from the tray icon.
    /// </summary>
    AllDisplays = 2,
}
