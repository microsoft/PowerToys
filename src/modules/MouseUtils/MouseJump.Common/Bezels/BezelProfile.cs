// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

namespace MouseJump.Common.Bezels;

/// <summary>
/// Lighting effect helpers shared by all <see cref="IBezelProfile"/>
/// implementations. These methods are profile-agnostic: they delegate
/// the geometry question ("what is the surface normal here?") to the
/// profile and handle the rest.
/// </summary>
internal static class BezelProfile
{
    /// <summary>
    /// Converts a surface normal angle to a signed lighting effect intensity in [-1, +1]
    /// using Lambert's cosine law: <c>intensity = cos(normalAngle)</c>.
    /// </summary>
    /// <remarks>
    /// Effect magnitudes
    /// -----------------
    /// +1.0 at normalAngle = 0   (normal directly facing the light source — full highlight).
    ///  0.0 at normalAngle = π/2 (normal at 90° to the light source — no effect).
    /// -1.0 at normalAngle = π   (normal facing 180° away from the light source — full shadow).
    ///
    /// In short, the intensity of reflected light (or shadow) at a point on a perfectly
    /// diffusing material is related to the angle of the surface to the light source,
    /// and is independent of the viewer's position.
    ///
    ///                           θ=π/2 radians
    ///                     (no lighting effect)
    ///                            ^
    ///                            |
    ///                         ▒▒▒▒▒░░░░░
    ///    light             ▒▒▒▒▒▒▒▒░░░░░░░
    ///    source          ▓▓▒▒▒▒▒▒▒▒░░░░░░░..
    ///    ----->         ▓▓▓▓▓▓▒▒▒▒▒░░░░░.....
    ///                  ▓▓▓▓▓▓▓▓▓▒▒▒░░.........
    ///             <-- ▓▓▓▓▓▓▓▓▓▓▓▓O............ -->
    ///            θ=0 radians                    θ=π radians
    ///            (+1, full highlight)           (-1, full shadow)
    /// </remarks>
    /// <param name="normalAngle">
    /// Angle in radians between the surface normal and the vector pointing
    /// toward the light source (see <see cref="IBezelProfile.GetProfileNormal"/>).
    /// 0 means directly facing the light source, π means facing away from the light source.
    /// </param>
    internal static double GetLightingEffectIntensity(double normalAngle)
    {
        // light effect intensity is the cosine of the light's angle of incidence
        // to the normal, ranging from +1 at 0 radians to -1 at π radians.
        // +1 means full highlight effect, -1 means full shadow effect.
        return Math.Cos(normalAngle);
    }

    // ── Normal-angle helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Returns the surface normal angle for a straight-edge pixel at depth
    /// <paramref name="d"/> pixels into an effect ring <paramref name="n"/> pixels wide.
    /// </summary>
    internal static double GetEdgeNormal(this IBezelProfile profile, int n, int d)
        => profile.GetProfileNormal(d / (double)n);

    /// <summary>
    /// Returns the surface normal angle for a corner pixel at
    /// <paramref name="originOffset"/> from the corner arc centre.
    /// </summary>
    /// <remarks>
    /// Assumes we're working within the cordinates of the combined 4-corner image
    /// we generate during border rendering:
    ///
    ///     -n      0     +n
    ///   -n +------+------+
    ///      |   ---|---   |
    ///      | /    |    \ |
    ///      ||  TL | TR  ||
    ///    0 +------O------+   ← origin point (0, 0) sits at "O"
    ///      ||  BL | BR  ||
    ///      | \    |    / |
    ///      |   ---|---   |
    ///   +n +------+------+
    ///
    /// originOffset is relative to (0, 0).
    ///
    /// </remarks>
    internal static double GetCornerNormal(this IBezelProfile profile, int cornerRadius, Point originOffset)
    {
        // convert the point's cartesian offset from the centre of the corner
        // (e.g. (3, 4)) into a radial distance from the centre of the corner
        var offsetRadius = Math.Sqrt(
            (double)(originOffset.X * originOffset.X) +
            (double)(originOffset.Y * originOffset.Y));

        // the distance across the border's profile is measured from the *outside*
        // of the curve, but we've got the radius from the centre of the corner's curve,
        // so we need to convert between the two measurements
        var offsetDistance = (double)cornerRadius - offsetRadius;

        // in the TR, BL and BR corners, the curve of the corner falls just outside
        // the bottom and / or right of the tile due to rasterization (the TR cell
        // ranges from *pixels* x=0 to x=cornerRadius-1, but the edge of the curve runs from
        // x=0 to x=cornerRadius on the continuous geometric number line).
        //
        // for a border of thickness cornerRadius the right-most TR offset point is (cornerRadius-1, 0),
        // and the offsetDistance is calculated as cornerRadius - (cornerRadius-1) = 1. This differs from
        // the calculation applied to the straight edges which gives 0, and renders
        // a noticeable lighting effect seam where the corner and edge join.
        if (originOffset.X > 0)
        {
            var lastColumnIndex = cornerRadius - 1;
            var offsetColumnIndex = originOffset.X;
            var columnDistance = lastColumnIndex - offsetColumnIndex;
            offsetDistance = Math.Min(offsetDistance, columnDistance);
        }

        if (originOffset.Y > 0)
        {
            var lastRowIndex = cornerRadius - 1;
            var offsetRowIndex = originOffset.Y;
            var rowDistance = lastRowIndex - offsetRowIndex;
            offsetDistance = Math.Min(offsetDistance, rowDistance);
        }

        // GetProfileNormal takes a proportion in [0, 1], not a pixel distance, so convert
        // here. offsetDistance can land slightly outside [0, n] for antialiased boundary
        // pixels whose partial coverage extends marginally past the arc's true radius -
        // GetProfileNormal validates its input and throws outside [0, 1], so clamp
        // explicitly before calling it.
        var proportion = Math.Clamp(offsetDistance / cornerRadius, 0.0, 1.0);
        return profile.GetProfileNormal(proportion);
    }
}
