// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.UI;

// IDE0047 (remove redundant parentheses) clashes with stylecop rules
#pragma warning disable IDE0047

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Extension methods for <see cref="Color"/>.
/// </summary>
internal static class ColorExtensions
{
    private const float ByteMax = byte.MaxValue;

    public static float CalculateBrightness(this Color color)
    {
        var (_, _, brightness) = color.ToHsv();
        return brightness;
    }

    /// <summary>
    /// Allows to change the brightness by a factor based on the HSV color space.
    /// </summary>
    /// <param name="color">Input color.</param>
    /// <param name="factor">The value of the brightness change factor from <see langword="100"/> to <see langword="-100"/>.</param>
    /// <returns>Updated color.</returns>
    public static Color UpdateBrightness(this Color color, float factor)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(factor, 100f);
        ArgumentOutOfRangeException.ThrowIfLessThan(factor, -100f);

        var (hue, saturation, rawBrightness) = color.ToHsv();
        var (red, green, blue) = FromHsvToRgb(hue, saturation, ToPercentage(rawBrightness + factor));
        return Color.FromArgb(color.A, ToColorByte(red), ToColorByte(green), ToColorByte(blue));
    }

    public static Color Update(this Color color, float brightnessFactor, float saturationFactor = 0, float luminanceFactor = 0)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(brightnessFactor, 100f);
        ArgumentOutOfRangeException.ThrowIfLessThan(brightnessFactor, -100f);

        ArgumentOutOfRangeException.ThrowIfGreaterThan(saturationFactor, 100f);
        ArgumentOutOfRangeException.ThrowIfLessThan(saturationFactor, -100f);

        ArgumentOutOfRangeException.ThrowIfGreaterThan(luminanceFactor, 100f);
        ArgumentOutOfRangeException.ThrowIfLessThan(luminanceFactor, -100f);

        var (hue, rawSaturation, rawBrightness) = color.ToHsv();

        var (red, green, blue) = FromHsvToRgb(
            hue,
            ToPercentage(rawSaturation + saturationFactor),
            ToPercentage(rawBrightness + brightnessFactor));

        if (luminanceFactor == 0)
        {
            return Color.FromArgb(color.A, ToColorByte(red), ToColorByte(green), ToColorByte(blue));
        }

        (hue, var saturation, var rawLuminance) = Color
            .FromArgb(color.A, ToColorByte(red), ToColorByte(green), ToColorByte(blue))
            .ToHsl();

        (red, green, blue) = FromHslToRgb(hue, saturation, ToPercentage(rawLuminance + luminanceFactor));

        return Color.FromArgb(color.A, ToColorByte(red), ToColorByte(green), ToColorByte(blue));
    }

    /// <summary>
    /// HSV representation models how colors appear under light.
    /// </summary>
    /// <returns><see langword="float"/> hue, <see langword="float"/> saturation, <see langword="float"/> brightness</returns>
    public static (float Hue, float Saturation, float Value) ToHsv(this Color color)
    {
        int red = color.R;
        int green = color.G;
        int blue = color.B;

        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));

        var fDelta = (max - min) / ByteMax;

        float hue;

        if (max <= 0)
        {
            return (0f, 0f, 0f);
        }

        var saturation = fDelta / (max / ByteMax);
        var value = max / ByteMax;

        if (fDelta <= 0.0)
        {
            return (0f, saturation * 100f, value * 100f);
        }

        if (max == red)
        {
            hue = ((green - blue) / ByteMax) / fDelta;
        }
        else if (max == green)
        {
            hue = 2f + (((blue - red) / ByteMax) / fDelta);
        }
        else
        {
            hue = 4f + (((red - green) / ByteMax) / fDelta);
        }

        if (hue < 0)
        {
            hue += 360;
        }

        return (hue * 60f, saturation * 100f, value * 100f);
    }

    /// <summary>
    /// Converts the color values stored as HSV (HSB) to RGB.
    /// </summary>
    private static (int R, int G, int B) FromHsvToRgb(float hue, float saturation, float brightness)
    {
        var red = 0;
        var green = 0;
        var blue = 0;

        if (AlmostEquals(saturation, 0, 0.01f))
        {
            red = green = blue = (int)(((brightness / 100f) * ByteMax) + 0.5f);

            return (red, green, blue);
        }

        hue /= 360f;
        brightness /= 100f;
        saturation /= 100f;

        var hueAngle = (hue - (float)Math.Floor(hue)) * 6.0f;
        var f = hueAngle - (float)Math.Floor(hueAngle);

        var p = brightness * (1.0f - saturation);
        var q = brightness * (1.0f - (saturation * f));
        var t = brightness * (1.0f - (saturation * (1.0f - f)));

        switch ((int)hueAngle)
        {
            case 0:
                red = (int)((brightness * 255.0f) + 0.5f);
                green = (int)((t * 255.0f) + 0.5f);
                blue = (int)((p * 255.0f) + 0.5f);

                break;
            case 1:
                red = (int)((q * 255.0f) + 0.5f);
                green = (int)((brightness * 255.0f) + 0.5f);
                blue = (int)((p * 255.0f) + 0.5f);

                break;
            case 2:
                red = (int)((p * 255.0f) + 0.5f);
                green = (int)((brightness * 255.0f) + 0.5f);
                blue = (int)((t * 255.0f) + 0.5f);

                break;
            case 3:
                red = (int)((p * 255.0f) + 0.5f);
                green = (int)((q * 255.0f) + 0.5f);
                blue = (int)((brightness * 255.0f) + 0.5f);

                break;
            case 4:
                red = (int)((t * 255.0f) + 0.5f);
                green = (int)((p * 255.0f) + 0.5f);
                blue = (int)((brightness * 255.0f) + 0.5f);

                break;
            case 5:
                red = (int)((brightness * 255.0f) + 0.5f);
                green = (int)((p * 255.0f) + 0.5f);
                blue = (int)((q * 255.0f) + 0.5f);

                break;
        }

        return (red, green, blue);
    }

    /// <summary>
    /// Converts the color values stored as HSL to RGB.
    /// </summary>
    private static (int R, int G, int B) FromHslToRgb(float hue, float saturation, float lightness)
    {
        if (AlmostEquals(saturation, 0, 0.01f))
        {
            var color = (int)(lightness * ByteMax);

            return (color, color, color);
        }

        lightness /= 100f;
        saturation /= 100f;

        var hueAngle = hue / 360f;

        return (
            CalcHslChannel(hueAngle + 0.333333333f, saturation, lightness),
            CalcHslChannel(hueAngle, saturation, lightness),
            CalcHslChannel(hueAngle - 0.333333333f, saturation, lightness)
        );
    }

    /// <summary>
    /// Calculates the color component for HSL.
    /// </summary>
    private static int CalcHslChannel(float color, float saturation, float lightness)
    {
        float num1;

        if (color > 1)
        {
            color -= 1f;
        }

        if (color < 0)
        {
            color += 1f;
        }

        if (lightness < 0.5f)
        {
            num1 = lightness * (1f + saturation);
        }
        else
        {
            num1 = lightness + saturation - (lightness * saturation);
        }

        var num2 = (2f * lightness) - num1;

        if (color * 6f < 1)
        {
            return (int)((num2 + ((num1 - num2) * 6f * color)) * ByteMax);
        }

        if (color * 2f < 1)
        {
            return (int)(num1 * ByteMax);
        }

        if (color * 3f < 2)
        {
            return (int)((num2 + ((num1 - num2) * (0.666666666f - color) * 6f)) * ByteMax);
        }

        return (int)(num2 * ByteMax);
    }

    /// <summary>
    /// Whether the floating point number is about the same.
    /// </summary>
    private static bool AlmostEquals(float numberOne, float numberTwo, float precision = 0)
    {
        if (precision <= 0)
        {
            precision = float.Epsilon;
        }

        return numberOne >= (numberTwo - precision) && numberOne <= (numberTwo + precision);
    }

    /// <summary>
    /// HSL representation models the way different paints mix together to create colour in the real world,
    /// with the lightness dimension resembling the varying amounts of black or white paint in the mixture.
    /// </summary>
    /// <returns><see langword="float"/> hue, <see langword="float"/> saturation, <see langword="float"/> lightness</returns>
    public static (float Hue, float Saturation, float Lightness) ToHsl(this Color color)
    {
        int red = color.R;
        int green = color.G;
        int blue = color.B;

        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));

        var fDelta = (max - min) / ByteMax;

        float hue;

        if (max <= 0)
        {
            return (0f, 0f, 0f);
        }

        var saturation = 0.0f;
        var lightness = ((max + min) / ByteMax) / 2.0f;

        if (fDelta <= 0.0)
        {
            return (0f, saturation * 100f, lightness * 100f);
        }

        saturation = fDelta / (max / ByteMax);

        if (max == red)
        {
            hue = ((green - blue) / ByteMax) / fDelta;
        }
        else if (max == green)
        {
            hue = 2f + (((blue - red) / ByteMax) / fDelta);
        }
        else
        {
            hue = 4f + (((red - green) / ByteMax) / fDelta);
        }

        if (hue < 0)
        {
            hue += 360;
        }

        return (hue * 60f, saturation * 100f, lightness * 100f);
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
    public static Color LerpHsv(this Color a, Color b, float t)
    {
        t = Clamp01(t);

        // Convert to HSV
        var (h1, s1, v1) = a.ToHsv();
        var (h2, s2, v2) = b.ToHsv();

        // Handle near-gray hues (undefined hue) by inheriting the other's hue
        const float satEps = 1e-4f;
        if (s1 < satEps && s2 >= satEps)
        {
            h1 = h2;
        }
        else if (s2 < satEps && s1 >= satEps)
        {
            h2 = h1;
        }

        // Shortest-arc hue interpolation with wrap
        var h = LerpHueDegrees(h1, h2, t);

        // Linear S, V, and A
        var s = Lerp(s1, s2, t);
        var v = Lerp(v1, v2, t);
        var aOut = (byte)Math.Round(Lerp(a.A / 255f, b.A / 255f, t) * 255f);

        // Back to RGB
        var (red, green, blue) = FromHsvToRgb(h, s, v);
        return Color.FromArgb(aOut, ToColorByte(red), ToColorByte(green), ToColorByte(blue));
    }

    private static float LerpHueDegrees(float a, float b, float t)
    {
        a = Mod360(a);
        b = Mod360(b);
        var delta = ((b - a + 540f) % 360f) - 180f;
        return Mod360(a + (delta * t));
    }

    private static float Mod360(float angle)
    {
        angle %= 360f;
        if (angle < 0f)
        {
            angle += 360f;
        }

        return angle;
    }

    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);

    private static float Clamp01(float x) => Math.Clamp(x, 0, 1);

    private static float ToPercentage(float value) => Math.Clamp(value, 0f, 100f);

    private static byte ToColorByte(int value) => (byte)Math.Clamp(value, byte.MinValue, byte.MaxValue);
}
