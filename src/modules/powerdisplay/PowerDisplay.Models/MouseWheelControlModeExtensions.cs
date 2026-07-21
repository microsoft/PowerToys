// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Models;

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
