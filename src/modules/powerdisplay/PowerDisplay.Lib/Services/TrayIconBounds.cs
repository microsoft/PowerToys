// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Common.Services;

/// <summary>
/// Represents a notification icon rectangle in virtual-screen coordinates.
/// </summary>
public readonly record struct TrayIconBounds(int Left, int Top, int Right, int Bottom)
{
    /// <summary>
    /// Gets a value indicating whether the rectangle has positive width and height.
    /// </summary>
    public bool IsValid => Right > Left && Bottom > Top;

    /// <summary>
    /// Determines whether a screen point is inside the rectangle.
    /// </summary>
    /// <param name="x">The virtual-screen X coordinate.</param>
    /// <param name="y">The virtual-screen Y coordinate.</param>
    /// <returns><see langword="true"/> when the point is inside the rectangle.</returns>
    public bool Contains(int x, int y)
        => IsValid && x >= Left && x < Right && y >= Top && y < Bottom;
}
