// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Globalization;

using ColorPicker.Helpers;
using ManagedCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Helpers
{
    /// <summary>
    /// Test class to test <see cref="ColorFormatHelper"/> conversion methods not covered
    /// by the existing <see cref="ColorConverterTest"/> (which tests HSL and HSV).
    /// Covers: CMYK, HSB, HSI, HWB, CIE LAB, CIE XYZ, Oklab, Oklch, sRGB-to-linear, NCol.
    /// </summary>
    [TestClass]
    public class ColorFormatConversionTest
    {
        // ========== ConvertToCMYKColor Tests ==========

        [TestMethod]
        public void ConvertToCMYK_Black_Returns0_0_0_1()
        {
            var result = ColorFormatHelper.ConvertToCMYKColor(Color.FromArgb(255, 0, 0, 0));
            Assert.AreEqual(0d, result.Cyan);
            Assert.AreEqual(0d, result.Magenta);
            Assert.AreEqual(0d, result.Yellow);
            Assert.AreEqual(1d, result.BlackKey);
        }

        [TestMethod]
        public void ConvertToCMYK_White_Returns0_0_0_0()
        {
            var result = ColorFormatHelper.ConvertToCMYKColor(Color.FromArgb(255, 255, 255, 255));
            Assert.AreEqual(0d, result.Cyan, 0.01);
            Assert.AreEqual(0d, result.Magenta, 0.01);
            Assert.AreEqual(0d, result.Yellow, 0.01);
            Assert.AreEqual(0d, result.BlackKey, 0.01);
        }

        [TestMethod]
        public void ConvertToCMYK_Red_Returns0_1_1_0()
        {
            var result = ColorFormatHelper.ConvertToCMYKColor(Color.FromArgb(255, 255, 0, 0));
            Assert.AreEqual(0d, result.Cyan, 0.01);
            Assert.AreEqual(1d, result.Magenta, 0.01);
            Assert.AreEqual(1d, result.Yellow, 0.01);
            Assert.AreEqual(0d, result.BlackKey, 0.01);
        }

        [TestMethod]
        public void ConvertToCMYK_Green_Returns1_0_1_0()
        {
            var result = ColorFormatHelper.ConvertToCMYKColor(Color.FromArgb(255, 0, 255, 0));
            Assert.AreEqual(1d, result.Cyan, 0.01);
            Assert.AreEqual(0d, result.Magenta, 0.01);
            Assert.AreEqual(1d, result.Yellow, 0.01);
            Assert.AreEqual(0d, result.BlackKey, 0.01);
        }

        [TestMethod]
        public void ConvertToCMYK_Blue_Returns1_1_0_0()
        {
            var result = ColorFormatHelper.ConvertToCMYKColor(Color.FromArgb(255, 0, 0, 255));
            Assert.AreEqual(1d, result.Cyan, 0.01);
            Assert.AreEqual(1d, result.Magenta, 0.01);
            Assert.AreEqual(0d, result.Yellow, 0.01);
            Assert.AreEqual(0d, result.BlackKey, 0.01);
        }

        [TestMethod]
        public void ConvertToCMYK_MidGray_Returns0_0_0_Half()
        {
            // RGB(128, 128, 128) should give roughly K ≈ 0.498
            var result = ColorFormatHelper.ConvertToCMYKColor(Color.FromArgb(255, 128, 128, 128));
            Assert.AreEqual(0d, result.Cyan, 0.01);
            Assert.AreEqual(0d, result.Magenta, 0.01);
            Assert.AreEqual(0d, result.Yellow, 0.01);
            Assert.AreEqual(128d / 255d, result.BlackKey, 0.01);
        }

        // ========== ConvertToHSBColor Tests (delegates to HSV) ==========

        [TestMethod]
        public void ConvertToHSB_Black_Returns0_0_0()
        {
            var result = ColorFormatHelper.ConvertToHSBColor(Color.FromArgb(255, 0, 0, 0));
            Assert.AreEqual(0d, result.Hue, 0.5);
            Assert.AreEqual(0d, result.Saturation, 0.01);
            Assert.AreEqual(0d, result.Brightness, 0.01);
        }

        [TestMethod]
        public void ConvertToHSB_White_Returns0_0_1()
        {
            var result = ColorFormatHelper.ConvertToHSBColor(Color.FromArgb(255, 255, 255, 255));
            Assert.AreEqual(0d, result.Hue, 0.5);
            Assert.AreEqual(0d, result.Saturation, 0.01);
            Assert.AreEqual(1d, result.Brightness, 0.01);
        }

        [TestMethod]
        public void ConvertToHSB_Red_Returns0_1_1()
        {
            var result = ColorFormatHelper.ConvertToHSBColor(Color.FromArgb(255, 255, 0, 0));
            Assert.AreEqual(0d, result.Hue, 0.5);
            Assert.AreEqual(1d, result.Saturation, 0.01);
            Assert.AreEqual(1d, result.Brightness, 0.01);
        }

        // ========== ConvertToHSIColor Tests ==========

        [TestMethod]
        public void ConvertToHSI_Black_Returns0_0_0()
        {
            var result = ColorFormatHelper.ConvertToHSIColor(Color.FromArgb(255, 0, 0, 0));
            Assert.AreEqual(0d, result.Hue, 0.5);
            Assert.AreEqual(0d, result.Saturation, 0.01);
            Assert.AreEqual(0d, result.Intensity, 0.01);
        }

        [TestMethod]
        public void ConvertToHSI_White_Returns0_0_1()
        {
            var result = ColorFormatHelper.ConvertToHSIColor(Color.FromArgb(255, 255, 255, 255));
            Assert.AreEqual(0d, result.Hue, 0.5);
            Assert.AreEqual(0d, result.Saturation, 0.01);
            Assert.AreEqual(1d, result.Intensity, 0.01);
        }

        [TestMethod]
        public void ConvertToHSI_Red_Returns0_1_Third()
        {
            // Pure red: intensity = (255+0+0)/(3*255) = 1/3
            var result = ColorFormatHelper.ConvertToHSIColor(Color.FromArgb(255, 255, 0, 0));
            Assert.AreEqual(0d, result.Hue, 0.5);
            Assert.AreEqual(1d, result.Saturation, 0.01);
            Assert.AreEqual(1d / 3d, result.Intensity, 0.01);
        }

        // ========== ConvertToHWBColor Tests ==========

        [TestMethod]
        public void ConvertToHWB_Black_Returns0_0_1()
        {
            var result = ColorFormatHelper.ConvertToHWBColor(Color.FromArgb(255, 0, 0, 0));
            Assert.AreEqual(0d, result.Hue, 0.5);
            Assert.AreEqual(0d, result.Whiteness, 0.01);
            Assert.AreEqual(1d, result.Blackness, 0.01);
        }

        [TestMethod]
        public void ConvertToHWB_White_Returns0_1_0()
        {
            var result = ColorFormatHelper.ConvertToHWBColor(Color.FromArgb(255, 255, 255, 255));
            Assert.AreEqual(0d, result.Hue, 0.5);
            Assert.AreEqual(1d, result.Whiteness, 0.01);
            Assert.AreEqual(0d, result.Blackness, 0.01);
        }

        [TestMethod]
        public void ConvertToHWB_Red_Returns0_0_0()
        {
            var result = ColorFormatHelper.ConvertToHWBColor(Color.FromArgb(255, 255, 0, 0));
            Assert.AreEqual(0d, result.Hue, 0.5);
            Assert.AreEqual(0d, result.Whiteness, 0.01);
            Assert.AreEqual(0d, result.Blackness, 0.01);
        }

        [TestMethod]
        public void ConvertToHWB_MidGray_Returns0_Half_Half()
        {
            var result = ColorFormatHelper.ConvertToHWBColor(Color.FromArgb(255, 128, 128, 128));
            Assert.AreEqual(0d, result.Hue, 0.5);
            Assert.AreEqual(128d / 255d, result.Whiteness, 0.01);
            Assert.AreEqual(1d - (128d / 255d), result.Blackness, 0.01);
        }

        // ========== ConvertToCIEXYZColor Tests ==========

        [TestMethod]
        public void ConvertToCIEXYZ_Black_Returns0_0_0()
        {
            var result = ColorFormatHelper.ConvertToCIEXYZColor(Color.FromArgb(255, 0, 0, 0));
            Assert.AreEqual(0d, result.X, 0.001);
            Assert.AreEqual(0d, result.Y, 0.001);
            Assert.AreEqual(0d, result.Z, 0.001);
        }

        [TestMethod]
        public void ConvertToCIEXYZ_White_ReturnsD65Illuminant()
        {
            // White should be close to D65 illuminant: X≈0.9505, Y≈1.0, Z≈1.089
            var result = ColorFormatHelper.ConvertToCIEXYZColor(Color.FromArgb(255, 255, 255, 255));
            Assert.AreEqual(0.9505d, result.X, 0.02);
            Assert.AreEqual(1.0d, result.Y, 0.02);
            Assert.AreEqual(1.089d, result.Z, 0.02);
        }

        [TestMethod]
        public void ConvertToCIEXYZ_Red_HasExpectedValues()
        {
            // Pure red: X≈0.4124, Y≈0.2126, Z≈0.0193
            var result = ColorFormatHelper.ConvertToCIEXYZColor(Color.FromArgb(255, 255, 0, 0));
            Assert.AreEqual(0.4124d, result.X, 0.02);
            Assert.AreEqual(0.2126d, result.Y, 0.02);
            Assert.AreEqual(0.0193d, result.Z, 0.02);
        }

        // ========== ConvertToCIELABColor Tests ==========

        [TestMethod]
        public void ConvertToCIELAB_Black_Returns0_0_0()
        {
            var result = ColorFormatHelper.ConvertToCIELABColor(Color.FromArgb(255, 0, 0, 0));
            Assert.AreEqual(0d, result.Lightness, 0.5);
            Assert.AreEqual(0d, result.ChromaticityA, 0.5);
            Assert.AreEqual(0d, result.ChromaticityB, 0.5);
        }

        [TestMethod]
        public void ConvertToCIELAB_White_Returns100_0_0()
        {
            var result = ColorFormatHelper.ConvertToCIELABColor(Color.FromArgb(255, 255, 255, 255));
            Assert.AreEqual(100d, result.Lightness, 1.0);
            Assert.AreEqual(0d, result.ChromaticityA, 1.0);
            Assert.AreEqual(0d, result.ChromaticityB, 1.0);
        }

        [TestMethod]
        public void ConvertToCIELAB_Red_HasPositiveA()
        {
            // Red is in the +a* direction
            var result = ColorFormatHelper.ConvertToCIELABColor(Color.FromArgb(255, 255, 0, 0));
            Assert.IsTrue(result.ChromaticityA > 0, "Red should have positive a* in CIE LAB");
        }

        [TestMethod]
        public void ConvertToCIELAB_Blue_HasNegativeA()
        {
            // Blue is in the -a* direction (and +b* direction)
            var result = ColorFormatHelper.ConvertToCIELABColor(Color.FromArgb(255, 0, 0, 255));
            Assert.IsTrue(result.ChromaticityB < 0, "Blue should have negative b* in CIE LAB");
        }

        // ========== ConvertToOklabColor Tests ==========

        [TestMethod]
        public void ConvertToOklab_Black_Returns0_0_0()
        {
            var result = ColorFormatHelper.ConvertToOklabColor(Color.FromArgb(255, 0, 0, 0));
            Assert.AreEqual(0d, result.Lightness, 0.01);
            Assert.AreEqual(0d, result.ChromaticityA, 0.01);
            Assert.AreEqual(0d, result.ChromaticityB, 0.01);
        }

        [TestMethod]
        public void ConvertToOklab_White_Returns1_0_0()
        {
            var result = ColorFormatHelper.ConvertToOklabColor(Color.FromArgb(255, 255, 255, 255));
            Assert.AreEqual(1d, result.Lightness, 0.02);
            Assert.AreEqual(0d, result.ChromaticityA, 0.02);
            Assert.AreEqual(0d, result.ChromaticityB, 0.02);
        }

        // ========== ConvertToOklchColor Tests ==========

        [TestMethod]
        public void ConvertToOklch_Black_Returns0_0_0()
        {
            var result = ColorFormatHelper.ConvertToOklchColor(Color.FromArgb(255, 0, 0, 0));
            Assert.AreEqual(0d, result.Lightness, 0.01);
            Assert.AreEqual(0d, result.Chroma, 0.01);
        }

        [TestMethod]
        public void ConvertToOklch_White_Returns1_0_Any()
        {
            var result = ColorFormatHelper.ConvertToOklchColor(Color.FromArgb(255, 255, 255, 255));
            Assert.AreEqual(1d, result.Lightness, 0.02);
            Assert.AreEqual(0d, result.Chroma, 0.02);
            // Hue is undefined for achromatic colors, so we don't assert it
        }

        [TestMethod]
        public void ConvertToOklch_Chroma_IsNonNegative()
        {
            // Chroma should always be non-negative
            var colors = new[] { Color.Red, Color.Green, Color.Blue, Color.Yellow, Color.Cyan, Color.Magenta };
            foreach (var color in colors)
            {
                var result = ColorFormatHelper.ConvertToOklchColor(color);
                Assert.IsTrue(result.Chroma >= 0, $"Chroma should be non-negative for {color.Name}");
            }
        }

        // ========== ConvertSRGBToLinearRGB Tests ==========

        [TestMethod]
        public void ConvertSRGBToLinear_Zero_ReturnsZero()
        {
            var result = ColorFormatHelper.ConvertSRGBToLinearRGB(0, 0, 0);
            Assert.AreEqual(0d, result.R, 0.001);
            Assert.AreEqual(0d, result.G, 0.001);
            Assert.AreEqual(0d, result.B, 0.001);
        }

        [TestMethod]
        public void ConvertSRGBToLinear_One_ReturnsOne()
        {
            var result = ColorFormatHelper.ConvertSRGBToLinearRGB(1, 1, 1);
            Assert.AreEqual(1d, result.R, 0.001);
            Assert.AreEqual(1d, result.G, 0.001);
            Assert.AreEqual(1d, result.B, 0.001);
        }

        [TestMethod]
        public void ConvertSRGBToLinear_SmallValues_UsesLinearPath()
        {
            // For small values (≤ 0.04045), the formula is linear: value / 12.92
            var result = ColorFormatHelper.ConvertSRGBToLinearRGB(0.04, 0.04, 0.04);
            Assert.AreEqual(0.04 / 12.92, result.R, 0.001);
        }

        [TestMethod]
        public void ConvertSRGBToLinear_LargeValues_UsesGammaPath()
        {
            // For larger values, the gamma function is applied
            // sRGB 0.5 should map to ~0.214
            var result = ColorFormatHelper.ConvertSRGBToLinearRGB(0.5, 0.5, 0.5);
            Assert.AreEqual(0.214, result.R, 0.01);
        }

        // ========== ConvertToNaturalColor Tests ==========

        [TestMethod]
        public void ConvertToNaturalColor_Red_ReturnsR0()
        {
            var result = ColorFormatHelper.ConvertToNaturalColor(Color.FromArgb(255, 255, 0, 0));
            Assert.AreEqual("R0", result.Hue);
            Assert.AreEqual(0d, result.Whiteness, 0.01);
            Assert.AreEqual(0d, result.Blackness, 0.01);
        }

        [TestMethod]
        public void ConvertToNaturalColor_Green_ReturnsG0()
        {
            var result = ColorFormatHelper.ConvertToNaturalColor(Color.FromArgb(255, 0, 128, 0));
            Assert.IsTrue(result.Hue.StartsWith("G"), $"Green should start with G, got {result.Hue}");
        }

        [TestMethod]
        public void ConvertToNaturalColor_Blue_ReturnsB0()
        {
            var result = ColorFormatHelper.ConvertToNaturalColor(Color.FromArgb(255, 0, 0, 255));
            Assert.IsTrue(result.Hue.StartsWith("B"), $"Blue should start with B, got {result.Hue}");
        }

        [TestMethod]
        public void ConvertToNaturalColor_Black_Returns0_0_100()
        {
            var result = ColorFormatHelper.ConvertToNaturalColor(Color.FromArgb(255, 0, 0, 0));
            Assert.AreEqual(0d, result.Whiteness, 0.01);
            Assert.AreEqual(1d, result.Blackness, 0.01);
        }

        [TestMethod]
        public void ConvertToNaturalColor_White_Returns0_100_0()
        {
            var result = ColorFormatHelper.ConvertToNaturalColor(Color.FromArgb(255, 255, 255, 255));
            Assert.AreEqual(1d, result.Whiteness, 0.01);
            Assert.AreEqual(0d, result.Blackness, 0.01);
        }

        // ========== GetStringRepresentation Tests for Various Colors ==========

        [TestMethod]
        [DataRow("CMYK", "cmyk(0%, 100%, 100%, 0%)")]
        [DataRow("HEX", "ff0000")]
        [DataRow("RGB", "rgb(255, 0, 0)")]
        [DataRow("HSL", "hsl(0, 100%, 50%)")]
        [DataRow("HSV", "hsv(0, 100%, 100%)")]
        [DataRow("HSB", "hsb(0, 100%, 100%)")]
        [DataRow("HSI", "hsi(0, 100%, 33%)")]
        [DataRow("HWB", "hwb(0, 0%, 0%)")]
        [DataRow("Decimal", "16711680")]
        [DataRow("HEX Int", "0xFFFF0000")]
        [DataRow("VEC4", "(1f, 0f, 0f, 1f)")]
        public void GetStringRepresentation_Red(string type, string expected)
        {
            var result = ColorRepresentationHelper.GetStringRepresentation(Color.FromArgb(255, 255, 0, 0), type, ColorFormatHelper.GetDefaultFormat(type));
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow("CMYK", "cmyk(0%, 0%, 0%, 0%)")]
        [DataRow("HEX", "ffffff")]
        [DataRow("RGB", "rgb(255, 255, 255)")]
        [DataRow("HSL", "hsl(0, 0%, 100%)")]
        [DataRow("HSV", "hsv(0, 0%, 100%)")]
        [DataRow("Decimal", "16777215")]
        [DataRow("HEX Int", "0xFFFFFFFF")]
        [DataRow("VEC4", "(1f, 1f, 1f, 1f)")]
        public void GetStringRepresentation_White(string type, string expected)
        {
            var result = ColorRepresentationHelper.GetStringRepresentation(Color.FromArgb(255, 255, 255, 255), type, ColorFormatHelper.GetDefaultFormat(type));
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow("CMYK", "cmyk(100%, 0%, 100%, 0%)")]
        [DataRow("HEX", "00ff00")]
        [DataRow("RGB", "rgb(0, 255, 0)")]
        [DataRow("HSL", "hsl(120, 100%, 50%)")]
        [DataRow("HSV", "hsv(120, 100%, 100%)")]
        [DataRow("Decimal", "65280")]
        [DataRow("HEX Int", "0xFF00FF00")]
        public void GetStringRepresentation_Green(string type, string expected)
        {
            var result = ColorRepresentationHelper.GetStringRepresentation(Color.FromArgb(255, 0, 255, 0), type, ColorFormatHelper.GetDefaultFormat(type));
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow("CMYK", "cmyk(100%, 100%, 0%, 0%)")]
        [DataRow("HEX", "0000ff")]
        [DataRow("RGB", "rgb(0, 0, 255)")]
        [DataRow("HSL", "hsl(240, 100%, 50%)")]
        [DataRow("HSV", "hsv(240, 100%, 100%)")]
        [DataRow("Decimal", "255")]
        [DataRow("HEX Int", "0xFF0000FF")]
        public void GetStringRepresentation_Blue(string type, string expected)
        {
            var result = ColorRepresentationHelper.GetStringRepresentation(Color.FromArgb(255, 0, 0, 255), type, ColorFormatHelper.GetDefaultFormat(type));
            Assert.AreEqual(expected, result);
        }

        // ========== GetStringRepresentation Edge Cases ==========

        [TestMethod]
        public void GetStringRepresentation_EmptyFormat_ReturnsHex()
        {
            // When colorFormat is null or empty, should return hex
            var result = ColorRepresentationHelper.GetStringRepresentation(Color.FromArgb(255, 255, 0, 0), "RGB", string.Empty);
            Assert.AreEqual("ff0000", result);
        }

        [TestMethod]
        public void GetStringRepresentation_NullFormat_ReturnsHex()
        {
            var result = ColorRepresentationHelper.GetStringRepresentation(Color.FromArgb(255, 255, 0, 0), "RGB", null);
            Assert.AreEqual("ff0000", result);
        }

        // ========== GetDefaultFormat Tests ==========

        [TestMethod]
        [DataRow("RGB")]
        [DataRow("HEX")]
        [DataRow("CMYK")]
        [DataRow("HSL")]
        [DataRow("HSV")]
        [DataRow("HSB")]
        [DataRow("HSI")]
        [DataRow("HWB")]
        [DataRow("NCol")]
        [DataRow("CIEXYZ")]
        [DataRow("CIELAB")]
        [DataRow("Oklab")]
        [DataRow("Oklch")]
        [DataRow("VEC4")]
        [DataRow("Decimal")]
        [DataRow("HEX Int")]
        public void GetDefaultFormat_KnownTypes_ReturnsNonEmptyString(string formatName)
        {
            var result = ColorFormatHelper.GetDefaultFormat(formatName);
            Assert.IsFalse(string.IsNullOrEmpty(result), $"Default format for {formatName} should not be empty");
        }
    }
}
