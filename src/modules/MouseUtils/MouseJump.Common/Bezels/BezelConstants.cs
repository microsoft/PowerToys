// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseJump.Common.Bezels;

/// <summary>
/// Rendering constants for corner and edge lighting effects.
/// </summary>
internal static class BezelConstants
{
    // ── global shading caps ──────────────────────────

    /// <summary>
    /// Peak opacity of the highlight (white) overlay, as a fraction of 255.
    /// 0.0 = no highlight; 1.0 = fully white at peak.
    /// </summary>
    internal const double HighlightMax = 0x44 / 255.0;

    /// <summary>
    /// Peak opacity of the shadow (black) overlay, as a fraction of 255.
    /// 0.0 = no shadow; 1.0 = fully black at peak.
    /// </summary>
    internal const double ShadowMax = 0x44 / 255.0;

    // ── corner shading parameters ──────────────────────────

    /// <summary>
    /// Degrees from the nearest straight edge at which a corner's highlight/shadow
    /// effect begins to fade. Full intensity between 0° and this value.
    /// </summary>
    /// <seealso cref="CornerTemplates.GetCornerTemplates"/>
    internal const double CornerGradientStart = 30.0;

    /// <summary>
    /// Degrees from the nearest straight edge at which a corner's highlight/shadow
    /// effect reaches zero intensity, fading via a cosine curve from
    /// <see cref="CornerGradientStart"/> to this value.
    /// </summary>
    /// <seealso cref="CornerTemplates.GetCornerTemplates"/>
    internal const double CornerGradientEnd = 90.0 - BezelConstants.CornerGradientStart; // 60.0

    // ── edge shading parameters ──────────────────────────

    /// <summary>
    /// Fraction along an edge line, from the corner-adjacent end, before its
    /// highlight/shadow effect begins to fade from <c>strongColor</c>.
    /// </summary>
    /// <seealso cref="BezelGraphics.DrawBezelEdgeLine"/>
    internal const float EdgeGradientStart = 0.05f;

    /// <summary>
    /// Fraction along an edge line, from the corner-adjacent end, at which its
    /// highlight/shadow effect finishes fading to <c>fadeColor</c>.
    /// </summary>
    /// <seealso cref="BezelGraphics.DrawBezelEdgeLine"/>
    internal const float EdgeGradientEnd = 0.75f;

    // ── Common corner / edge seam effect multipliers ──────────────────────────
    //
    // Effect multipliers used at the points where corner and edges join.
    // Used by corner and edge rendering methods to ensure a smooth visual
    // transition between edges and corner sections around borders and bezels
    //
    // "Forward" parameters apply to the perimeter sections that point
    // toward the light source, "Reverse" parameters apply to perimeter
    // sections that point away from the light source.

    /// <summary>
    /// Forward (top/left) edges: highlight strength at the corner-adjacent end of the gradient line.
    /// </summary>
    internal const double EdgeForwardHighlightStrong = 1.75;

    /// <summary>
    /// Forward (top/left) edges: highlight strength at the faded-out end of the gradient line.
    /// </summary>
    internal const double EdgeForwardHighlightFade = 1.0;

    /// <summary>
    /// Forward (top/left) edges: shadow strength at the corner-adjacent end of the gradient line.
    /// </summary>
    internal const double EdgeForwardShadowStrong = 1.5;

    /// <summary>
    /// Forward (top/left) edges: shadow strength at the faded-out end of the gradient line.
    /// </summary>
    internal const double EdgeForwardShadowFade = 1.0;

    /// <summary>
    /// Reverse (bottom/right) edges: highlight strength at the corner-adjacent end of the gradient line.
    /// </summary>
    internal const double EdgeReverseHighlightStrong = 0.75;

    /// <summary>
    /// Reverse (bottom/right) edges: highlight strength at the faded-out end of the gradient line.
    /// </summary>
    internal const double EdgeReverseHighlightFade = 0.5;

    /// <summary>
    /// Reverse (bottom/right) edges: shadow strength at the corner-adjacent end of the gradient line.
    /// </summary>
    internal const double EdgeReverseShadowStrong = 1.5;

    /// <summary>
    /// Reverse (bottom/right) edges: shadow strength at the faded-out end of the gradient line.
    /// </summary>
    internal const double EdgeReverseShadowFade = 1.0;
}
