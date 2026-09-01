// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Common.Services;

/// <summary>
/// Accumulates high-resolution wheel deltas into complete wheel notches.
/// </summary>
public sealed class WheelDeltaAccumulator
{
    /// <summary>
    /// The Win32 delta value for one complete wheel notch.
    /// </summary>
    public const int WheelDelta = 120;

    private int _remainder;

    /// <summary>
    /// Adds a signed wheel delta and returns the number of newly completed notches.
    /// </summary>
    /// <param name="delta">The signed Win32 wheel delta.</param>
    /// <returns>The number of newly completed notches.</returns>
    public int Add(int delta)
    {
        var total = (long)_remainder + delta;
        var notches = (int)(total / WheelDelta);
        _remainder = (int)(total % WheelDelta);
        return notches;
    }

    /// <summary>
    /// Clears any incomplete wheel-notch remainder.
    /// </summary>
    public void Reset()
    {
        _remainder = 0;
    }
}
