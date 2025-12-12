// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI.Helpers;
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

        public static Color Transform(Color input, Options? opt = null)
        {
            opt ??= new Options();
            var hsv = input.ToHsv();
            return ColorHelper.FromHsv(
                RemapHueLut(hsv.H),
                Clamp01(Math.Pow(hsv.S, opt.SaturationGamma) * opt.SaturationGain),
                Clamp01((opt.ValueScaleA * hsv.V) + opt.ValueBiasB),
                input.A);
        }

        // Hue LUT remap (piecewise-linear with cyclic wrap)
        private static double RemapHueLut(double hDeg)
        {
            // Normalize to [0,360)
            hDeg = Mod(hDeg, 360.0);

            // Handle wrap-around case: hDeg is between last entry (350°) and 360°
            var last = HueMap[^1];
            var first = HueMap[0];
            if (hDeg >= last.HIn)
            {
                // Interpolate between last entry and first entry (wrapped by 360°)
                var t = (hDeg - last.HIn) / (first.HIn + 360.0 - last.HIn + 1e-12);
                var ho = Lerp(last.HOut, first.HOut + 360.0, t);
                return Mod(ho, 360.0);
            }

            // Find segment [i, i+1] where HueMap[i].HIn <= hDeg < HueMap[i+1].HIn
            for (var i = 0; i < HueMap.Length - 1; i++)
            {
                var a = HueMap[i];
                var b = HueMap[i + 1];

                if (hDeg >= a.HIn && hDeg < b.HIn)
                {
                    var t = (hDeg - a.HIn) / (b.HIn - a.HIn + 1e-12);
                    return Lerp(a.HOut, b.HOut, t);
                }
            }

            // Fallback (shouldn't happen)
            return hDeg;
        }

        private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

        private static double Mod(double x, double m) => ((x % m) + m) % m;

        private static double Clamp01(double x) => x < 0 ? 0 : (x > 1 ? 1 : x);

        public sealed class Options
        {
            // Saturation boost (1.0 = no change). Typical: 1.3–1.8
            public double SaturationGain { get; init; } = 1.0;

            // Optional saturation gamma (1.0 = linear). <1.0 raises low S a bit; >1.0 preserves low S.
            public double SaturationGamma { get; init; } = 1.0;

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
            var light1 = accent.Update(brightnessFactor: 0.15, saturationFactor: -0.12);
            var light2 = accent.Update(brightnessFactor: 0.30, saturationFactor: -0.24);
            var light3 = accent.Update(brightnessFactor: 0.45, saturationFactor: -0.36);

            var dark1 = accent.UpdateBrightness(brightnessFactor: -0.05f);
            var dark2 = accent.UpdateBrightness(brightnessFactor: -0.01f);
            var dark3 = accent.UpdateBrightness(brightnessFactor: -0.015f);

            return (light3, light2, light1, dark1, dark2, dark3);
        }
    }
}
