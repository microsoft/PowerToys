// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI.Helpers;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Extension methods for <see cref="Color"/>.
/// </summary>
internal static class ColorExtensions
{
    /// <param name="color">Input color.</param>
    public static double CalculateBrightness(this Color color)
    {
        return color.ToHsv().V;
    }

    /// <summary>
    /// Allows to change the brightness by a factor based on the HSV color space.
    /// </summary>
    /// <param name="color">Input color.</param>
    /// <param name="brightnessFactor">The brightness adjustment factor, ranging from -1 to 1.</param>
    /// <returns>Updated color.</returns>
    public static Color UpdateBrightness(this Color color, double brightnessFactor)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(brightnessFactor, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(brightnessFactor, -1);

        var hsvColor = color.ToHsv();
        return ColorHelper.FromHsv(hsvColor.H, hsvColor.S, Math.Clamp(hsvColor.V + brightnessFactor, 0, 1), hsvColor.A);
    }

    /// <summary>
    /// Updates the color by adjusting brightness, saturation, and luminance factors.
    /// </summary>
    /// <param name="color">Input color.</param>
    /// <param name="brightnessFactor">The brightness adjustment factor, ranging from -1 to 1.</param>
    /// <param name="saturationFactor">The saturation adjustment factor, ranging from -1 to 1. Defaults to 0.</param>
    /// <param name="luminanceFactor">The luminance adjustment factor, ranging from -1 to 1. Defaults to 0.</param>
    /// <returns>Updated color.</returns>
    public static Color Update(this Color color, double brightnessFactor, double saturationFactor = 0, double luminanceFactor = 0)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(brightnessFactor, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(brightnessFactor, -1);

        ArgumentOutOfRangeException.ThrowIfGreaterThan(saturationFactor, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(saturationFactor, -1);

        ArgumentOutOfRangeException.ThrowIfGreaterThan(luminanceFactor, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(luminanceFactor, -1);

        var hsv = color.ToHsv();

        var rgb = ColorHelper.FromHsv(
            hsv.H,
            Clamp01(hsv.S + saturationFactor),
            Clamp01(hsv.V + brightnessFactor));

        if (luminanceFactor == 0)
        {
            return rgb;
        }

        var hsl = rgb.ToHsl();
        var lightness = Clamp01(hsl.L + luminanceFactor);
        return ColorHelper.FromHsl(hsl.H, hsl.S, lightness);
    }

    /// <summary>
    /// Linearly interpolates between two colors in HSV space.
    /// Hue is blended along the shortest arc on the color wheel (wrap-aware).
    /// Saturation, Value, and Alpha are blended linearly.
    /// </summary>
    /// <param name="a">Start color.</param>
    /// <param name="b">End color.</param>
    /// <param name="t">Interpolation factor in [0,1].</param>
    /// <returns>Interpolated color.</returns>
    public static Color LerpHsv(this Color a, Color b, double t)
    {
        t = Clamp01(t);

        // Convert to HSV
        var hslA = a.ToHsv();
        var hslB = b.ToHsv();

        var h1 = hslA.H;
        var h2 = hslB.H;

        // Handle near-gray hues (undefined hue) by inheriting the other's hue
        const double satEps = 1e-4f;
        if (hslA.S < satEps && hslB.S >= satEps)
        {
            h1 = h2;
        }
        else if (hslB.S < satEps && hslA.S >= satEps)
        {
            h2 = h1;
        }

        return ColorHelper.FromHsv(
            hue: LerpHueDegrees(h1, h2, t),
            saturation: Lerp(hslA.S, hslB.S, t),
            value: Lerp(hslA.V, hslB.V, t),
            alpha: (byte)Math.Round(Lerp(hslA.A, hslB.A, t)));
    }

    private static double LerpHueDegrees(double a, double b, double t)
    {
        a = Mod360(a);
        b = Mod360(b);
        var delta = ((b - a + 540f) % 360f) - 180f;
        return Mod360(a + (delta * t));
    }

    private static double Mod360(double angle)
    {
        angle %= 360f;
        if (angle < 0f)
        {
            angle += 360f;
        }

        return angle;
    }

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    private static double Clamp01(double x) => Math.Clamp(x, 0, 1);
}
