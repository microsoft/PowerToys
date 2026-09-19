// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseJump.Common.Bezels;

/// <summary>
/// Models the cross-sectional surface geometry of a bezel ring, mapping a
/// position along the ring to a surface normal angle.
/// </summary>
/// <remarks>
/// Cross-section layout (pixel 0 = outer arc edge, n = content boundary -
/// pixel terms from the <see cref="BezelProfileCurved(int, int)"/> overload's
/// own n/d. GetProfileNormal itself takes the position row below, not the
/// pixel row):
///
///   r = curve radius
///   n = bezel width
///   d = bezel depth
///
///   position:   0           r           1-r          1
///   pixel:      0           d           n-d          n
///               │           │            │           │
///   θ (normal): 0 ───────→ π/2 ──────── π/2 ───────→  π
///   cos(θ):    +1 ───────→  0  ────────  0  ───────→ -1
///               │outer ring │    flat    │inner ring │
///               │(highlight)│   (none)   │(shadow)   │
///
/// GetProfileNormal maps a position to a normal angle; the shared helpers in
/// BezelProfile convert that angle to highlight / shadow intensities.
/// </remarks>
internal sealed class BezelProfileCurved : IBezelProfile
{
    /// <remarks>
    /// Convenience overload for callers working in pixels rather than <see cref="CurveRadius"/>
    /// directly - equivalent to <c>new BezelProfileCurved(bezelDepth / (double)bezelWidth)</c>.
    /// </remarks>
    /// <param name="bezelWidth">The full bezel ring width in pixels (outer arc → content boundary).</param>
    /// <param name="bezelDepth">The 3D effect ring depth in pixels, on each side.</param>
    internal BezelProfileCurved(int bezelWidth, int bezelDepth)
        : this(bezelDepth / (double)bezelWidth)
    {
    }

    internal BezelProfileCurved(double curveRadius)
    {
        if (curveRadius < 0.0 || curveRadius > 0.5)
        {
            throw new ArgumentOutOfRangeException(nameof(curveRadius), curveRadius, $"{nameof(curveRadius)} must be between 0 and 0.5 inclusive.");
        }

        this.CurveRadius = curveRadius;
    }

    /// <summary>
    /// Gets the radius of the curves at the start and end of the profile,
    /// as a proportion of the full profile width. e.g. 0.25 means 25% of
    /// the profile width.
    /// </summary>
    /// <remarks>
    /// Notable values:
    ///
    ///     * 0.00 - a flat bezel with no raised profile, and no lighting effect
    ///              as a result
    ///
    ///     * 0.25 - each curve occupies a quarter of the profile width (or half the
    ///              profile width in total) with the flat portion in the middle also
    ///              occupying half the profile width
    ///
    ///     * 0.50 - the curves meet in the middle and form a perfect semi-circular
    ///              profile, which is the largest allowed value (any larger and the
    ///              curves would overlap in the middle of the profile)
    /// </remarks>
    private double CurveRadius
    {
        get;
    }

    /// <summary>
    /// Returns the surface normal angle in radians at <paramref name="position"/>
    /// of the way from the outer arc boundary to the content boundary
    /// (0 = outer arc edge, 1 = content boundary).
    ///
    /// 0   — outer arc edge: surface faces the light source directly (full highlight).
    /// π/2 — flat zone junction: surface faces sideways (no effect).
    /// π   — content boundary: surface faces away from the light source (full shadow).
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

        if (this.CurveRadius <= 0.0)
        {
            // zero-width effect ring - there's no bevel surface to have any normal
            // other than flat, so the "0 = facing the light" contract below doesn't
            // apply: with no ring to curve through, the outer arc edge is flat too.
            return Math.PI / 2.0;
        }

        if (position <= 0.0)
        {
            return 0.0; // at or beyond outer arc edge
        }

        if (position < this.CurveRadius)
        {
            // true quarter-circle: for a circular arc transitioning from a vertical
            // tangent (facing the light) to a horizontal one (flat), horizontal
            // displacement x relates to swept angle φ as x = radius * (1 - cos(φ)),
            // so inverting for φ given a position/radius ratio t gives φ = acos(1 - t).
            return Math.Acos(1.0 - (position / this.CurveRadius)); // outer effect ring: 0 → π/2
        }

        if (position < 1.0 - this.CurveRadius)
        {
            return Math.PI / 2.0; // flat zone — surface faces sideways
        }

        if (position < 1.0)
        {
            return Math.PI - Math.Acos(1.0 - ((1.0 - position) / this.CurveRadius)); // inner effect ring: π/2 → π
        }

        return Math.PI; // at or beyond content boundary
    }
}
