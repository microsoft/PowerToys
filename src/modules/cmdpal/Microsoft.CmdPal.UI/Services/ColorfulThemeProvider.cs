// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.UI.Xaml;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Microsoft.CmdPal.UI.Services;

/// <summary>
/// Provides theme appropriate for colorful (accented) appearance.
/// </summary>
internal sealed class ColorfulThemeProvider : IThemeProvider
{
    // Fluent dark:  #202020
    private static readonly Color DarkBaseColor = Color.FromArgb(255, 32, 32, 32);

    // Fluent light: #F3F3F3
    private static readonly Color LightBaseColor = Color.FromArgb(255, 243, 243, 243);

    private readonly UISettings _uiSettings;

    public string ThemeKey => "colorful";

    public string ResourcePath => "ms-appx:///Styles/Theme.Colorful.xaml";

    public ColorfulThemeProvider(UISettings uiSettings)
    {
        ArgumentNullException.ThrowIfNull(uiSettings);
        _uiSettings = uiSettings;
    }

    public AcrylicBackdropParameters GetAcrylicBackdrop(ThemeContext context)
    {
        var isLight = context.Theme == ElementTheme.Light ||
                      (context.Theme == ElementTheme.Default &&
                       _uiSettings.GetColorValue(UIColorType.Background).R > 128);

        var baseColor = isLight ? LightBaseColor : DarkBaseColor;

        // Windows is warping the hue of accent colors and running it through some curves to produce their accent shades.
        // This will attempt to mimic that behavior.
        var accentShades = AccentShades.Compute(context.Tint.LerpHsv(WindowsAccentHueWarpTransform.Transform(context.Tint), 0.5f));
        var blended = isLight ? accentShades.Light3 : accentShades.Dark2;
        var colorIntensityUser = (context.ColorIntensity ?? 100) / 100f;

        // For light theme, we want to reduce intensity a bit, and also we need to keep the color fairly light,
        // to avoid issues with text box caret.
        var colorIntensity = isLight ? 0.6f * colorIntensityUser : colorIntensityUser;
        var effectiveBgColor = ColorBlender.Blend(baseColor, blended, colorIntensity);

        return new AcrylicBackdropParameters(effectiveBgColor, effectiveBgColor, 0.8f, 0.8f);
    }

    private static class ColorBlender
    {
        /// <summary>
        /// Blends a semitransparent tint color over an opaque base color using alpha compositing.
        /// </summary>
        /// <param name="baseColor">The opaque base color (background)</param>
        /// <param name="tintColor">The semitransparent tint color (foreground)</param>
        /// <param name="intensity">The intensity of the tint (0.0 - 1.0)</param>
        /// <returns>The resulting blended color</returns>
        public static Color Blend(Color baseColor, Color tintColor, float intensity)
        {
            // Normalize alpha to 0.0 - 1.0 range
            intensity = Math.Clamp(intensity, 0f, 1f);

            // Alpha compositing formula: result = tint * alpha + base * (1 - alpha)
            var r = (byte)((tintColor.R * intensity) + (baseColor.R * (1 - intensity)));
            var g = (byte)((tintColor.G * intensity) + (baseColor.G * (1 - intensity)));
            var b = (byte)((tintColor.B * intensity) + (baseColor.B * (1 - intensity)));

            // Result is fully opaque since base is opaque
            return Color.FromArgb(255, r, g, b);
        }
    }

    private static class WindowsAccentHueWarpTransform
    {
        private static readonly (double HIn, double HOut)[] HueMap =
        [
            (0, 0),
            (10, 1),
            (20, 6),
            (30, 10),
            (40, 14),
            (50, 19),
            (60, 36),
            (70, 94),
            (80, 112),
            (90, 120),
            (100, 120),
            (110, 120),
            (120, 120),
            (130, 120),
            (140, 120),
            (150, 125),
            (160, 135),
            (170, 142),
            (180, 178),
            (190, 205),
            (200, 220),
            (210, 229),
            (220, 237),
            (230, 241),
            (240, 243),
            (250, 244),
            (260, 245),
            (270, 248),
            (280, 252),
            (290, 276),
            (300, 293),
            (310, 313),
            (320, 330),
            (330, 349),
            (340, 353),
            (350, 357)
        ];

        public static Color Transform(Color c, Options? opt = null)
        {
            opt ??= new Options();

            // to HSV
            RgbToHsv(c, out var h, out var s, out var v); // h in [0,360), s,v in [0,1]

            // 1) Hue warp via LUT
            var h2 = RemapHueLut(h);

            // 2) Saturation tweak
            var s2 = Clamp01(Math.Pow(Clamp01(s), opt.SaturationGamma) * opt.SaturationGain);

            // 3) Value tone curve
            var v2 = Clamp01((opt.ValueScaleA * v) + opt.ValueBiasB);

            // back to RGB
            HsvToRgb(h2, s2, v2, out var r, out var g, out var b);
            return Color.FromArgb(c.A, (byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
        }

        // Hue LUT remap (piecewise-linear with cyclic wrap)
        private static double RemapHueLut(double hDeg)
        {
            // Normalize to [0,360)
            hDeg = Mod(hDeg, 360.0);

            // Fast paths: below first or above last – handle cyclicly by comparing to endpoints +/-360
            // Build a small search window that includes -360 and +360 copies to handle wrap cleanly.
            Span<(double HIn, double HOut)> buf = stackalloc (double, double)[HueMap.Length + 2];
            for (var i = 0; i < HueMap.Length; i++)
            {
                buf[i] = HueMap[i];
            }

            buf[HueMap.Length + 0] = (HueMap[^1].HIn - 360.0, HueMap[^1].HOut - 360.0);
            buf[HueMap.Length + 1] = (HueMap[0].HIn + 360.0, HueMap[0].HOut + 360.0);

            // Find segment [i,i+1] s.t. hIn_i <= hDeg <= hIn_{i+1}, considering wrap
            var best = double.NaN;
            var bestDist = double.PositiveInfinity;

            for (var pass = -1; pass <= 1; pass++)
            {
                var hh = hDeg + (360.0 * pass);
                for (var i = 0; i < buf.Length - 1; i++)
                {
                    var a = buf[i];
                    var b = buf[i + 1];

                    if (hh >= a.HIn && hh <= b.HIn)
                    {
                        var t = (hh - a.HIn) / (b.HIn - a.HIn + 1e-12);
                        var ho = Lerp(a.HOut, b.HOut, t);

                        // Map back to [0,360)
                        ho = Mod(ho, 360.0);

                        // choose the closest reconstructed domain
                        var dist = Math.Abs(hh - ((a.HIn + b.HIn) * 0.5));
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            best = ho;
                        }

                        break;
                    }
                }
            }

            // Fallback (shouldn't happen)
            return double.IsNaN(best) ? hDeg : best;
        }

        // RGB/HSV utilities (HSV with H∈[0,360), S,V∈[0,1])
        private static void RgbToHsv(Color c, out double h, out double s, out double v)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            v = max;

            var d = max - min;
            s = max <= 0 ? 0 : (d / max);

            if (d < 1e-12)
            {
                h = 0; // undefined; choose 0
            }
            else
            {
                double hue;
                if (Math.Abs(max - r) < double.Epsilon * 2)
                {
                    hue = ((g - b) / d) + (g < b ? 6 : 0);
                }
                else if (Math.Abs(max - g) < double.Epsilon * 2)
                {
                    hue = ((b - r) / d) + 2;
                }
                else
                {
                    hue = ((r - g) / d) + 4;
                }

                h = (hue / 6.0) * 360.0;
            }
        }

        private static void HsvToRgb(double h, double s, double v, out double r, out double g, out double b)
        {
            h = Mod(h, 360.0);
            if (s <= 1e-12)
            {
                r = g = b = v;
                return;
            }

            var hh = h / 60.0;
            var i = (int)Math.Floor(hh);
            var f = hh - i;

            var p = v * (1.0 - s);
            var q = v * (1.0 - (s * f));
            var t = v * (1.0 - (s * (1.0 - f)));

            switch (Mod(i, 6))
            {
                case 0:
                    r = v;
                    g = t;
                    b = p;
                    break;
                case 1:
                    r = q;
                    g = v;
                    b = p;
                    break;
                case 2:
                    r = p;
                    g = v;
                    b = t;
                    break;
                case 3:
                    r = p;
                    g = q;
                    b = v;
                    break;
                case 4:
                    r = t;
                    g = p;
                    b = v;
                    break;
                default:
                    r = v;
                    g = p;
                    b = q;
                    break;
            }
        }

        private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

        private static double Mod(double x, double m) => ((x % m) + m) % m;

        private static double Clamp01(double x) => x < 0 ? 0 : (x > 1 ? 1 : x);

        public sealed class Options
        {
            // Saturation boost (1.0 = no change). Typical: 1.3–1.8
            public double SaturationGain { get; init; } = 1.0;

            // Optional saturation gamma (1.0 = linear). <1.0 raises low S a bit; >1.0 preserves low S.
            public double SaturationGamma { get; init; } = 1.00;

            // Value (V) remap: V' = a*V + b   (tone curve; clamp applied)
            // Example that lifts blacks & compresses whites slightly: a=0.50, b=0.08
            public double ValueScaleA { get; init; } = 0.6;

            public double ValueBiasB { get; init; } = 0.01;
        }
    }

    private static class AccentShades
    {
        public static (Color Light3, Color Light2, Color Light1, Color Dark1, Color Dark2, Color Dark3) Compute(Color accent)
        {
            var light1 = accent.Update(brightnessFactor: 15, saturationFactor: -12);
            var light2 = accent.Update(brightnessFactor: 30, saturationFactor: -24);
            var light3 = accent.Update(brightnessFactor: 45, saturationFactor: -36);

            var dark1 = accent.UpdateBrightness(factor: -5);
            var dark2 = accent.UpdateBrightness(factor: -10);
            var dark3 = accent.UpdateBrightness(factor: -15);

            return (light3, light2, light1, dark1, dark2, dark3);
        }

        private static double ToLinear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        private static byte ToSrgb(double cLin)
        {
            var c = cLin <= 0.0031308 ? 12.92 * cLin : (1.055 * Math.Pow(cLin, 1 / 2.4)) - 0.055;
            return (byte)Math.Clamp(Math.Round(c * 255.0), 0, 255);
        }
    }
}
