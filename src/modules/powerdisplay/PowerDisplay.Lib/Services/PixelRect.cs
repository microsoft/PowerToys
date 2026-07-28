// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Common.Services;

/// <summary>
/// Represents a rectangle in physical screen pixels, positioned by its top-left corner.
/// </summary>
/// <remarks>
/// Mirrors the shape of the display geometry the UI layer works in, so this library can compute
/// placement without taking a WinRT projection on a dependency-free, AOT-compatible assembly. The
/// UI layer converts at the boundary.
/// </remarks>
/// <param name="X">The left edge, in physical screen pixels.</param>
/// <param name="Y">The top edge, in physical screen pixels.</param>
/// <param name="Width">The width, in physical screen pixels.</param>
/// <param name="Height">The height, in physical screen pixels.</param>
public readonly record struct PixelRect(int X, int Y, int Width, int Height);
