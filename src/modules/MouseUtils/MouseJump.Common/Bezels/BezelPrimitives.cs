// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

namespace MouseJump.Common.Bezels;

internal static class BezelPrimitives
{
    /// <summary>
    /// Implements a cosine easing function that can be used to create a
    /// smooth transition between two values on a continuous axis within
    /// an interval.
    /// <code>
    ///  +-----+-----+-----+
    ///   -----._    .       - start value
    ///        . \   .
    ///        .  \  .
    ///        .   \ .
    ///        .    -._____  _ end value
    ///  +-----+-----+-----+
    ///        ^   ^
    ///        |   |
    ///        |   easing interval end
    ///        easing interval start
    /// </code>
    ///
    /// </summary>
    /// <returns>
    /// if x <= easingIntervalStart, returns startValue.
    /// if x >= easingIntervalEnd, returns endValue.
    /// otherwise returns an eased value between startValue and endValue
    /// </returns>
    internal static double CosineEase(
        double x,
        double intervalStart,
        double intervalEnd,
        double startValue,
        double endValue)
    {
        if (x <= intervalStart)
        {
            return startValue;
        }

        if (x >= intervalEnd)
        {
            return endValue;
        }

        var easingIntervalWidth = intervalEnd - intervalStart;

        var t = (x - intervalStart) / easingIntervalWidth;
        var weight = 0.5 * (1.0 - Math.Cos(Math.PI * t));

        var valueDelta = endValue - startValue;
        return startValue + (valueDelta * weight);
    }

    /// <summary>
    /// Calculates the intensity of a 3-stage gradient fill at a given angle around
    /// a bezel corner, taking into account the transition points (start / end degrees)
    /// of the stages.
    /// </summary>
    /// <param name="theta">Angle in degrees, measured from the nearest straight edge.</param>
    internal static double CornerEffectWeight(double theta, double fadeStartDegrees, double fadeEndDegrees)
    {
        // overall, the shape of the resulting weight is 3 stages, transitioning
        // at <fadeStartDegrees>° and <fadeEndDegrees>°:
        //
        //     * stage 1 - full intensity: 1.0
        //     * stage 2 - cosine easing:  1.0 -> 0.0
        //     * stage 3 - zero intensity: 0.0
        //
        //     |▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▒▒▒▒▒▒▒▒▒▒▒▒░░░░░░░░░░░░---------|
        //     t=0° ^                                  ^        t=90°
        //          |                                  |
        //          fadeStartDegrees                   fadeEndDegrees
        //
        //     * the first stage is held flat at full intensity (1.0), for
        //       angles close to the straight edge
        //
        //     * the effect then rolls off from 1.0 to 0.0 via a cosine ease,
        //       for a smooth, continuous transition
        //
        //     * the final stage is held flat at zero intensity for the rest
        //       of the corner, up to the diagonal at 90°
        //
        // this is used when drawing the lighting effects on a corner - the
        // linear gradient is "wrapped" round the curve of a corner like this
        // (diagram shows the rounded top right corner of a rectangle; "." marks
        // a pixel inside the corner radius with zero effect, blank is outside
        // the radius entirely - not part of the drawn shape):
        //
        //     t=0°  / fadeStartDegrees
        //     +----/-------+
        //     |▓▓▓/.       |/ fadeEndDegrees
        //     |▓▓▓▓▓▒▒.    /
        //     |▓▓▓▓▒▒▒▒▒. /|
        //     |▓▓▓▒▒▒▒░░░/ |
        //     |▓▒▒▒░░░░░░░.|
        //     |▒░░░░░░░░░░░|
        //     +------------+ t=90°
        //   (0,0)
        return CosineEase(theta, fadeStartDegrees, fadeEndDegrees, 1.0, 0.0);
    }

    /// <summary>
    /// Returns the GDI screen angle in degrees for a pixel offset (dx, dy) from an arc centre.
    /// 0° = rightward, increasing clockwise. Result is always in [0, 360).
    /// </summary>
    internal static double GetGdiAngle(int dx, int dy)
    {
        // .    270
        //       |
        // 180 --+-- 0
        //       |
        //      90
        var angle = Math.Atan2(dy, dx) * (180.0 / Math.PI);
        return angle < 0
            ? angle + 360.0
            : angle;
    }

    /// <summary>
    /// Calculates the intensity of a single-peak gradient at a given angle around a
    /// bezel corner. Used on TL / BR corners as a multiplier to add a secondary-effect
    /// peak halfway round the double-highlight / double-shadow arc.
    /// </summary>
    /// <param name="theta">Angle in degrees, measured from the nearest straight edge.</param>
    internal static double MidpointPeak(double theta)
    {
        // overall, the shape of the resulting weight is a single peak, rising to
        // full intensity at the 45° midpoint and back down again:
        //
        //     * stage 1 - rising:  0.0 -> 1.0
        //     * stage 2 - falling: 1.0 -> 0.0
        //
        //     |░░░░░░░░░░░░▒▒▒▒▓▓▓▓▓▓▓▒▒▒▒░░░░░░░░░░░░|
        //     t=0°                ^                  t=90°
        //                         |
        //                        45°
        //
        //     * the effect rises from zero at the straight edge, through a
        //       half-sine ease (sin(θ × π/90)), up to full intensity at the
        //       45° midpoint
        //
        //     * it then falls back to zero via the same half-sine curve,
        //       reaching zero again at the other straight edge (90°)
        //
        // this is used when drawing the lighting effects on a corner - the
        // linear gradient is "wrapped" round the curve of a corner like this
        // (diagram shows the rounded top right corner of a rectangle):
        //
        //     t=0°
        //     +------------+
        //     |░░░▒▒       |
        //     |░░▒▒▒▓▓▓    |
        //     |░░▒▒▓▓▓▓▓▓  |
        //     |░▒▒▓▓▓▓▓▓▒▒ |
        //     |░▒▓▓▓▓▒▒▒▒▒░|
        //     |▒▓▒▒░░░░░░░░|
        //     +------------+ t=90°
        //   (0,0)
        return Math.Sin(theta * Math.PI / 90.0);
    }

    /// <summary>
    /// Fades 1.0 → 0.0 from a straight edge (θ = 0°) to the 45° corner midpoint.
    /// Used on TR / BL corners where highlight meets shadow; both contributions fade
    /// to zero at 45° so the bezel colour is clean at the diagonal.
    /// </summary>
    /// <param name="theta">Angle in degrees, measured from the nearest straight edge.</param>
    internal static double MidpointFade(double theta)
    {
        // overall, the shape of the resulting weight is 2 stages, transitioning
        // at 45°:
        //
        //     * stage 1 - cosine easing:  1.0 -> 0.0
        //     * stage 2 - zero intensity: 0.0
        //
        //     |▓▓▓▓▓▓▓▓▒▒▒▒▒▒▒░░░░░░░------------------------|
        //     t=0°                   ^                       t=90°
        //                            |
        //                           45°
        //
        //     * the effect starts at full intensity (1.0) right at the
        //       straight edge (0°), and rolls off to zero via a cosine
        //       ease by the 45° midpoint
        //
        //     * the final stage is held flat at zero intensity for the
        //       rest of the corner, from 45° up to the diagonal at 90°
        //
        // this is used when drawing the lighting effects on a corner - the
        // linear gradient is "wrapped" round the curve of a corner like this
        // (diagram shows the rounded top right corner of a rectangle; "." marks
        // a pixel inside the corner radius with zero effect, blank is outside
        // the radius entirely - not part of the drawn shape):
        //
        //     t=0°          45°
        //     +------------/
        //     |▓▓▒▒▒     / |
        //     |▓▓▒▒░░░░/   |
        //     |▓▓▒░░░░...  |
        //     |▓▒░░░...... |
        //     |▓░░.........|
        //     |░...........|
        //     +------------+ t=90°
        //   (0,0)
        return CosineEase(theta, 0.0, 45.0, 1.0, 0.0);
    }

    /// <summary>
    /// Lightens <paramref name="baseColor"/> toward white by <paramref name="highlightLevel"/>,
    /// capped at <paramref name="highlightMax"/> so the effect stays subtle. A negative
    /// resulting alpha is clamped to zero (no effect) rather than darkening the colour -
    /// every current caller's <paramref name="highlightLevel"/> is non-negative, but this
    /// method doesn't rely on that being true.
    /// </summary>
    internal static Color ApplyHighlight(Color baseColor, double highlightLevel, double highlightMax)
    {
        var highlightAlpha = Math.Clamp(highlightLevel * highlightMax, 0.0, 1.0);
        var r = baseColor.R + (highlightAlpha * (255 - baseColor.R));
        var g = baseColor.G + (highlightAlpha * (255 - baseColor.G));
        var b = baseColor.B + (highlightAlpha * (255 - baseColor.B));
        return Color.FromArgb(
            baseColor.A, // we don't blend alpha, just carry the original through
            (int)Math.Clamp(r, 0, 255),
            (int)Math.Clamp(g, 0, 255),
            (int)Math.Clamp(b, 0, 255));
    }

    /// <summary>
    /// Darkens <paramref name="baseColor"/> toward black by <paramref name="shadowLevel"/>,
    /// capped at <paramref name="shadowMax"/> so the effect stays subtle. A negative
    /// resulting alpha is clamped to zero (no effect) rather than lightening the colour -
    /// every current caller's <paramref name="shadowLevel"/> is non-negative, but this
    /// method doesn't rely on that being true.
    /// </summary>
    internal static Color ApplyShadow(Color baseColor, double shadowLevel, double shadowMax)
    {
        var shadowAlpha = Math.Clamp(shadowLevel * shadowMax, 0.0, 1.0);
        var r = baseColor.R * (1 - shadowAlpha);
        var g = baseColor.G * (1 - shadowAlpha);
        var b = baseColor.B * (1 - shadowAlpha);
        return Color.FromArgb(
            baseColor.A, // we don't blend alpha, just carry the original through
            (int)Math.Clamp(r, 0, 255),
            (int)Math.Clamp(g, 0, 255),
            (int)Math.Clamp(b, 0, 255));
    }
}
