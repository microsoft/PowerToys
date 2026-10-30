// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseJump.Common.Bezels;

/// <summary>
/// Defines the cross-sectional surface geometry of a bezel ring.
/// Implementations map a position along the profile to a surface normal angle
/// that the shared lighting helpers in <see cref="BezelProfile"/> convert
/// to highlight / shadow intensities.
/// </summary>
internal interface IBezelProfile
{
    /// <summary>
    /// Returns the surface normal angle in radians at <paramref name="position"/>
    /// of the way from the outer arc boundary to the content boundary
    /// (0 = outer arc edge, 1 = content boundary).
    ///
    /// The angle is measured relative to the light source direction, not the
    /// viewer: implementations must return 0 where the local surface normal
    /// faces the light directly (full highlight), π/2 where it faces sideways
    /// (no effect), and π where it faces directly away from the light (full
    /// shadow). <see cref="BezelProfile.GetLightingEffectIntensity"/> applies Lambert's
    /// cosine law to this angle and relies on every implementation honoring
    /// this convention.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="position"/> is less than 0 or greater than 1.
    /// </exception>
    double GetProfileNormal(double position);
}
