// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITest
{
    internal static class VisualHelper
    {
        #pragma warning disable SA1307
        [StructLayout(LayoutKind.Sequential)]
        private struct IMMERSIVE_COLOR_PREFERENCE
        {
            public uint dwColorSetIndex;
            public uint crStartColor;
            public uint crAccentColor;
        }
        #pragma warning restore SA1307

        [DllImport("uxtheme.dll", EntryPoint = "#120")]
        private static extern IntPtr GetUserColorPreference(ref IMMERSIVE_COLOR_PREFERENCE colorPreference, bool fForceReload);

        /// <summary>
        /// Gets the system accent color.
        /// </summary>
        /// <returns>The system accent color as a Color object.</returns>
        private static Color GetSystemAccentColor()
        {
            IMMERSIVE_COLOR_PREFERENCE colorPreference = default(IMMERSIVE_COLOR_PREFERENCE);
            GetUserColorPreference(ref colorPreference, true);
            return ToColor(colorPreference.crStartColor);
        }

        /// <summary>
        /// Converts a color value to a Color object.
        /// </summary>
        /// <param name="c">The color value.</param>
        /// <returns>The Color object.</returns>
        private static Color ToColor(uint c)
        {
            int r = (int)(c & 0xFF) % 256;
            int g = (int)((c >> 8) & 0xFF) % 256;
            int b = (int)(c >> 16) % 256;
            return Color.FromArgb(r, g, b);
        }

        /// <summary>
        /// Gets HSL values from a Color object.
        /// </summary>
        /// <param name="color">The Color object.</param>
        /// <returns>A tuple containing the HSL values.</returns>
        private static (double H, double S, double L) GetHSL(Color color)
        {
            double rNorm = color.R / 255.0;
            double gNorm = color.G / 255.0;
            double bNorm = color.B / 255.0;

            double max = Math.Max(rNorm, Math.Max(gNorm, bNorm));
            double min = Math.Min(rNorm, Math.Min(gNorm, bNorm));
            double h = 0, s = 0, l = (max + min) / 2;

            if (max != min)
            {
                double delta = max - min;
                s = l > 0.5 ? delta / (2 - max - min) : delta / (max + min);

                if (max == rNorm)
                {
                    h = ((gNorm - bNorm) / delta) + (gNorm < bNorm ? 6 : 0);
                }
                else if (max == gNorm)
                {
                    h = ((bNorm - rNorm) / delta) + 2;
                }
                else if (max == bNorm)
                {
                    h = ((rNorm - gNorm) / delta) + 4;
                }

                h /= 6;
            }

            return (h * 360, s * 100, l * 100);
        }

        /// <summary>
        /// Makes a specific color in an image transparent.
        /// </summary>
        /// <param name="imagePath">The path to the image file.</param>
        /// <param name="outputPath">The path to save the output image file.</param>
        /// <param name="targetColor">The target color to make transparent.</param>
        /// <param name="fuzz">The fuzz factor for color comparison, default is 2.</param>
        private static void MakeColorTransparent(string imagePath, string outputPath, Color targetColor, int fuzz = 2)
        {
            var hsl = GetHSL(targetColor);

            // Assert.IsNotNull(null, $"Target Color - H: {hsl.H}, S: {hsl.S}, L: {hsl.L}");
            using (Bitmap originalBitmap = new Bitmap(imagePath))
            {
                using (Bitmap bitmap = new Bitmap(originalBitmap))
                {
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            Color pixelColor = bitmap.GetPixel(x, y);
                            if (HueIsSame(pixelColor, targetColor, fuzz))
                            {
                                bitmap.SetPixel(x, y, Color.Transparent);
                            }
                        }
                    }

                    bitmap.Save(outputPath, ImageFormat.Png);
                }
            }
        }

        /// <summary>
        /// Erases the user preference color from an image. Will overwrite this image.
        /// </summary>
        /// <param name="imagePath">The path to the image file.</param>
        /// <param name="fuzz">The fuzz factor for color comparison, default is 2.</param>
        public static void EraseUserPreferenceColor(string imagePath, int fuzz = 2)
        {
            Color systemColor = GetSystemAccentColor();
            string tempPath = Path.GetTempFileName();
            MakeColorTransparent(imagePath, tempPath, systemColor, fuzz);
            File.Delete(imagePath);
            File.Move(tempPath, imagePath);
        }

        /// <summary>
        /// Compare two pixels with a fuzz factor
        /// </summary>
        /// <param name="c1">base color</param>
        /// <param name="c2">test color</param>
        /// <param name="fuzz">fuzz factor, default is 10</param>
        /// <returns>true if same, otherwise is false</returns>
        public static bool PixIsSame(Color c1, Color c2, int fuzz = 10)
        {
            return Math.Abs(c1.A - c2.A) <= fuzz && Math.Abs(c1.R - c2.R) <= fuzz && Math.Abs(c1.G - c2.G) <= fuzz && Math.Abs(c1.B - c2.B) <= fuzz;
        }

        /// <summary>
        /// Compares the hue of two colors with a fuzz factor.
        /// </summary>
        /// <param name="c1">The first color.</param>
        /// <param name="c2">The second color.</param>
        /// <param name="fuzz">The fuzz factor, default is 2.</param>
        /// <returns>True if the hues are the same, otherwise false.</returns>
        public static bool HueIsSame(Color c1, Color c2, int fuzz = 2)
        {
            var h1 = GetHSL(c1).H;
            var h2 = GetHSL(c2).H;
            return Math.Abs(h1 - h2) <= fuzz;
        }
    }
}
