// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable SA1649 // File name should match first type name

namespace PowerDisplay.Models
{
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

    /// <summary>
    /// Provides validation helpers for <see cref="MouseWheelControlMode"/>.
    /// </summary>
    public static class MouseWheelControlModeExtensions
    {
        /// <summary>
        /// Returns a supported mode, or <see cref="MouseWheelControlMode.Disabled"/> for an
        /// unsupported persisted numeric value.
        /// </summary>
        /// <param name="mode">The persisted mode value.</param>
        /// <returns>A supported mode value.</returns>
        public static MouseWheelControlMode Normalize(this MouseWheelControlMode mode)
            => mode is MouseWheelControlMode.Disabled
                or MouseWheelControlMode.PrimaryDisplay
                or MouseWheelControlMode.AllDisplays
                ? mode
                : MouseWheelControlMode.Disabled;
    }
}

#pragma warning restore SA1649 // File name should match first type name
