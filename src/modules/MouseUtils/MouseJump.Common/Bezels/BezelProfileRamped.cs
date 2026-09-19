// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseJump.Common.Bezels;

/// <summary>
/// Models the cross-sectional surface geometry of a bezel ring as a flat
/// inclined plane (chamfer / bevel), mapping a position along the ring to a
/// CONSTANT surface normal angle across each effect ring.
///
/// Cross-section layout (pixel 0 = outer arc edge, n = content boundary -
/// pixel terms from the constructor's own n/d. GetProfileNormal itself takes
/// the position row below, not the pixel row):
///
///   pixel:      0      d           n-d      n
///   position:   0     d/n         1-d/n     1
///               │      │            │       │
///   θ (normal): α ──── α ──────── π-α ──── π-α
///               │outer ring│  flat  │inner ring│
///               │(highlight)│ (none) │(shadow) │
///
///   where α = π/2 − rampAngle  (rampAngle is the inclination from horizontal)
/// </summary>
internal sealed class BezelProfileRamped : IBezelProfile
{
    private readonly int _n;            // bezel ring pixel width (outer arc → content boundary)
    private readonly int _d;            // 3D effect ring depth in pixels on each side
    private readonly double _rampAngle; // inclination from horizontal, in radians

    internal BezelProfileRamped(int n, int d, double rampAngleDegrees)
    {
        _n = n;
        _d = d;
        _rampAngle = rampAngleDegrees * Math.PI / 180.0;
    }

    /// <summary>
    /// Returns the surface normal angle in radians at <paramref name="position"/>
    /// of the way from the outer arc boundary to the content boundary
    /// (0 = outer arc edge, 1 = content boundary).
    ///
    /// The normal is constant within each effect ring (a flat inclined surface):
    ///   π/2 − rampAngle — outer ring (faces partly toward the light source).
    ///   π/2             — flat zone  (faces sideways; no effect).
    ///   π/2 + rampAngle — inner ring (faces partly away from the light source).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="position"/> is less than 0 or greater than 1.
    /// </exception>
    public double GetProfileNormal(double position)
    {
        if (position < 0.0 || position > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, "position must be between 0 and 1 inclusive.");
        }

        // depthProportion is the effect ring's own depth (_d) expressed as a
        // proportion of the full profile width (_n).
        var depthProportion = _d / (double)_n;

        if (position < depthProportion)
        {
            return (Math.PI / 2.0) - _rampAngle; // outer ring: constant ramp angle (highlight)
        }

        if (position < 1.0 - depthProportion)
        {
            return Math.PI / 2.0; // flat zone — surface faces sideways
        }

        return (Math.PI / 2.0) + _rampAngle; // inner ring: constant ramp angle (shadow)
    }
}
