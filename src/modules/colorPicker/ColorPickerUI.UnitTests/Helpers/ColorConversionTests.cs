// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Globalization;

using ManagedCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Helpers
{
    /// <summary>
    /// Extended test class for color conversion edge cases, alpha channel handling,
    /// HEX parsing/formatting, boundary values, and clipboard formatting.
    /// </summary>
    [TestClass]
    public class ColorConversionTests
    {
        // =====================================================================
        // RGB <-> HSL edge case tests
        // =====================================================================

        [TestMethod]
        public void HSL_Black_ShouldBeZeroHueSaturation()
        {
            var black = Color.FromArgb(255, 0, 0, 0);
            var (hue, saturation, lightness) = ColorFormatHelper.ConvertToHSLColor(black);
            Assert.AreEqual(0, hue, "Black hue should be 0");
            Assert.AreEqual(0, saturation, "Black saturation should be 0");
            Assert.AreEqual(0, lightness, "Black lightness should be 0");
        }

        [TestMethod]
        public void HSL_White_ShouldBe100Lightness()
        {
            var white = Color.FromArgb(255, 255, 255, 255);
            var (hue, saturation, lightness) = ColorFormatHelper.ConvertToHSLColor(white);
            Assert.AreEqual(0, hue, "White hue should be 0");
            Assert.AreEqual(0, saturation, "White saturation should be 0");
            Assert.AreEqual(100, lightness, "White lightness should be 100");
        }

        [TestMethod]
        public void HSL_PureRed_ShouldBeHue0()
        {
            var red = Color.FromArgb(255, 255, 0, 0);
            var (hue, saturation, lightness) = ColorFormatHelper.ConvertToHSLColor(red);
            Assert.AreEqual(0, hue, "Pure red hue should be 0");
            Assert.AreEqual(100, saturation, "Pure red saturation should be 100");
            Assert.AreEqual(50, lightness, "Pure red lightness should be 50");
        }

        [TestMethod]
        public void HSL_PureGreen_ShouldBeHue120()
        {
            var green = Color.FromArgb(255, 0, 255, 0);
            var (hue, saturation, lightness) = ColorFormatHelper.ConvertToHSLColor(green);
            Assert.AreEqual(120, hue, "Pure green hue should be 120");
            Assert.AreEqual(100, saturation, "Pure green saturation should be 100");
            Assert.AreEqual(50, lightness, "Pure green lightness should be 50");
        }

        [TestMethod]
        public void HSL_PureBlue_ShouldBeHue240()
        {
            var blue = Color.FromArgb(255, 0, 0, 255);
            var (hue, saturation, lightness) = ColorFormatHelper.ConvertToHSLColor(blue);
            Assert.AreEqual(240, hue, "Pure blue hue should be 240");
            Assert.AreEqual(100, saturation, "Pure blue saturation should be 100");
            Assert.AreEqual(50, lightness, "Pure blue lightness should be 50");
        }

        [TestMethod]
        public void HSL_MidGray_ShouldHaveZeroSaturation()
        {
            var gray = Color.FromArgb(255, 128, 128, 128);
            var (hue, saturation, lightness) = ColorFormatHelper.ConvertToHSLColor(gray);
            Assert.AreEqual(0, saturation, "Gray saturation should be 0");
            Assert.IsTrue(lightness > 40 && lightness < 60, "Gray lightness should be around 50");
        }

        // =====================================================================
        // RGB <-> HSV conversion tests
        // =====================================================================

        [TestMethod]
        public void HSV_Black_ShouldBeZeroValue()
        {
            var black = Color.FromArgb(255, 0, 0, 0);
            var (hue, saturation, value) = ColorFormatHelper.ConvertToHSVColor(black);
            Assert.AreEqual(0, hue, "Black HSV hue should be 0");
            Assert.AreEqual(0, saturation, "Black HSV saturation should be 0");
            Assert.AreEqual(0, value, "Black HSV value should be 0");
        }

        [TestMethod]
        public void HSV_White_ShouldBe100Value()
        {
            var white = Color.FromArgb(255, 255, 255, 255);
            var (hue, saturation, value) = ColorFormatHelper.ConvertToHSVColor(white);
            Assert.AreEqual(0, saturation, "White HSV saturation should be 0");
            Assert.AreEqual(100, value, "White HSV value should be 100");
        }

        [TestMethod]
        public void HSV_PureRed_ShouldBeFull()
        {
            var red = Color.FromArgb(255, 255, 0, 0);
            var (hue, saturation, value) = ColorFormatHelper.ConvertToHSVColor(red);
            Assert.AreEqual(0, hue, "Pure red HSV hue should be 0");
            Assert.AreEqual(100, saturation, "Pure red HSV saturation should be 100");
            Assert.AreEqual(100, value, "Pure red HSV value should be 100");
        }

        [TestMethod]
        public void HSV_PureCyan_ShouldBeHue180()
        {
            var cyan = Color.FromArgb(255, 0, 255, 255);
            var (hue, saturation, value) = ColorFormatHelper.ConvertToHSVColor(cyan);
            Assert.AreEqual(180, hue, "Cyan HSV hue should be 180");
            Assert.AreEqual(100, saturation, "Cyan HSV saturation should be 100");
            Assert.AreEqual(100, value, "Cyan HSV value should be 100");
        }

        // =====================================================================
        // HEX formatting tests
        // =====================================================================

        [TestMethod]
        public void HEX_Black_ShouldBe000000()
        {
            var black = Color.FromArgb(255, 0, 0, 0);
            var result = ColorFormatHelper.GetStringRepresentation(black, "%Rex%Grx%Blx");
            Assert.AreEqual("000000", result, "Black HEX should be 000000");
        }

        [TestMethod]
        public void HEX_White_ShouldBeFFFFFF()
        {
            var white = Color.FromArgb(255, 255, 255, 255);
            var result = ColorFormatHelper.GetStringRepresentation(white, "%Rex%Grx%Blx");
            Assert.AreEqual("ffffff", result, "White HEX should be ffffff");
        }

        [TestMethod]
        public void HEX_PureRed_ShouldBeFF0000()
        {
            var red = Color.FromArgb(255, 255, 0, 0);
            var result = ColorFormatHelper.GetStringRepresentation(red, "%Rex%Grx%Blx");
            Assert.AreEqual("ff0000", result, "Red HEX should be ff0000");
        }

        [TestMethod]
        public void HEX_UpperCase_ShouldFormat()
        {
            var blue = Color.FromArgb(255, 0, 0, 255);
            var result = ColorFormatHelper.GetStringRepresentation(blue, "%ReX%GrX%BlX");
            Assert.AreEqual("0000FF", result, "Blue HEX uppercase should be 0000FF");
        }

        // =====================================================================
        // Default format tests
        // =====================================================================

        [TestMethod]
        public void DefaultFormat_HEX_ShouldReturnExpected()
        {
            var format = ColorFormatHelper.GetDefaultFormat("HEX");
            Assert.AreEqual("%Rex%Grx%Blx", format, "Default HEX format mismatch");
        }

        [TestMethod]
        public void DefaultFormat_RGB_ShouldReturnExpected()
        {
            var format = ColorFormatHelper.GetDefaultFormat("RGB");
            Assert.AreEqual("rgb(%Re, %Gr, %Bl)", format, "Default RGB format mismatch");
        }

        [TestMethod]
        public void DefaultFormat_HSL_ShouldReturnExpected()
        {
            var format = ColorFormatHelper.GetDefaultFormat("HSL");
            Assert.AreEqual("hsl(%Hu, %Sl%, %Ll%)", format, "Default HSL format mismatch");
        }

        [TestMethod]
        public void DefaultFormat_HSV_ShouldReturnExpected()
        {
            var format = ColorFormatHelper.GetDefaultFormat("HSV");
            Assert.AreEqual("hsv(%Hu, %Sb%, %Va%)", format, "Default HSV format mismatch");
        }

        [TestMethod]
        public void DefaultFormat_CMYK_ShouldReturnExpected()
        {
            var format = ColorFormatHelper.GetDefaultFormat("CMYK");
            Assert.AreEqual("cmyk(%Cy%, %Ma%, %Ye%, %Bk%)", format, "Default CMYK format mismatch");
        }

        [TestMethod]
        public void DefaultFormat_Unknown_ShouldReturnEmpty()
        {
            var format = ColorFormatHelper.GetDefaultFormat("NONEXISTENT");
            Assert.AreEqual(string.Empty, format, "Unknown format should return empty");
        }

        // =====================================================================
        // VEC4 format - alpha channel handling tests
        // =====================================================================

        [TestMethod]
        public void VEC4_Format_ShouldHardcodeAlpha1f()
        {
            var format = ColorFormatHelper.GetDefaultFormat("VEC4");
            Assert.IsTrue(format.Contains("1f"), "VEC4 format should contain hardcoded 1f for alpha");
        }

        [TestMethod]
        public void VEC4_Black_ShouldFormatCorrectly()
        {
            var black = Color.FromArgb(255, 0, 0, 0);
            var format = ColorFormatHelper.GetDefaultFormat("VEC4");
            var result = ColorFormatHelper.GetStringRepresentation(black, format);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("0.000"), "VEC4 black should contain 0.000 values");
        }

        [TestMethod]
        public void VEC4_White_ShouldFormatCorrectly()
        {
            var white = Color.FromArgb(255, 255, 255, 255);
            var format = ColorFormatHelper.GetDefaultFormat("VEC4");
            var result = ColorFormatHelper.GetStringRepresentation(white, format);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("1.000"), "VEC4 white should contain 1.000 values");
        }

        [TestMethod]
        public void HexInt_Format_ShouldHardcodeFFAlpha()
        {
            var format = ColorFormatHelper.GetDefaultFormat("HEX Int");
            Assert.IsTrue(format.Contains("0xFF"), "HEX Int format should contain hardcoded 0xFF for alpha");
        }

        // =====================================================================
        // CMYK conversion tests
        // =====================================================================

        [TestMethod]
        public void CMYK_Black_ShouldBe100Key()
        {
            var black = Color.FromArgb(255, 0, 0, 0);
            var (cyan, magenta, yellow, blackKey) = ColorFormatHelper.ConvertToCMYKColor(black);
            Assert.AreEqual(0, cyan, "Black CMYK cyan should be 0");
            Assert.AreEqual(0, magenta, "Black CMYK magenta should be 0");
            Assert.AreEqual(0, yellow, "Black CMYK yellow should be 0");
            Assert.AreEqual(100, blackKey, "Black CMYK key should be 100");
        }

        [TestMethod]
        public void CMYK_White_ShouldBeZeroAll()
        {
            var white = Color.FromArgb(255, 255, 255, 255);
            var (cyan, magenta, yellow, blackKey) = ColorFormatHelper.ConvertToCMYKColor(white);
            Assert.AreEqual(0, cyan, "White CMYK cyan should be 0");
            Assert.AreEqual(0, magenta, "White CMYK magenta should be 0");
            Assert.AreEqual(0, yellow, "White CMYK yellow should be 0");
            Assert.AreEqual(0, blackKey, "White CMYK key should be 0");
        }

        // =====================================================================
        // Clipboard formatting tests (string representation)
        // =====================================================================

        [TestMethod]
        public void Clipboard_RGB_FormatShouldWork()
        {
            var color = Color.FromArgb(255, 128, 64, 32);
            var result = ColorFormatHelper.GetStringRepresentation(color, "rgb(%Re, %Gr, %Bl)");
            Assert.AreEqual("rgb(128, 64, 32)", result, "RGB clipboard format mismatch");
        }

        [TestMethod]
        public void Clipboard_HSL_FormatShouldWork()
        {
            var red = Color.FromArgb(255, 255, 0, 0);
            var result = ColorFormatHelper.GetStringRepresentation(red, "hsl(%Hu, %Sl%, %Ll%)");
            Assert.AreEqual("hsl(0, 100%, 50%)", result, "HSL clipboard format for red mismatch");
        }

        [TestMethod]
        public void Clipboard_NullColor_ShouldReturnEmpty()
        {
            var result = ColorFormatHelper.GetStringRepresentation(null, "rgb(%Re, %Gr, %Bl)");
            Assert.AreEqual(string.Empty, result, "Null color should return empty string");
        }

        // =====================================================================
        // Alpha channel preservation tests
        // =====================================================================

        [TestMethod]
        public void Alpha_SemiTransparent_ShouldBePreservedInHex()
        {
            var semiTransparent = Color.FromArgb(128, 255, 0, 0);
            Assert.AreEqual(128, semiTransparent.A, "Alpha should be 128");
            Assert.AreEqual(255, semiTransparent.R, "Red should be 255");
        }

        [TestMethod]
        public void Alpha_FullyTransparent_ShouldBeZero()
        {
            var transparent = Color.FromArgb(0, 100, 100, 100);
            Assert.AreEqual(0, transparent.A, "Alpha should be 0 for fully transparent");
        }

        [TestMethod]
        public void Alpha_High_ShouldNotTruncate()
        {
            // Regression test: alpha values close to max (> 0.99 when normalized)
            // should not be silently truncated to 255/1.0
            var almostOpaque = Color.FromArgb(253, 200, 100, 50);
            Assert.AreEqual(253, almostOpaque.A, "Alpha 253 should not be truncated to 255");

            var result = ColorFormatHelper.GetStringRepresentation(almostOpaque, "%Re %Gr %Bl");
            Assert.AreEqual("200 100 50", result, "RGB values should be correct regardless of alpha");
        }

        [TestMethod]
        public void Alpha_254_ShouldNotBecomeOpaque()
        {
            // Verify that alpha=254 (0.996 normalized) is not rounded to 255
            var color = Color.FromArgb(254, 128, 128, 128);
            Assert.AreEqual(254, color.A, "Alpha 254 should remain 254, not become 255");
        }

        // =====================================================================
        // Boundary value tests
        // =====================================================================

        [TestMethod]
        public void Boundary_MinRGB_ShouldConvertToHSL()
        {
            var minColor = Color.FromArgb(255, 0, 0, 0);
            var (hue, saturation, lightness) = ColorFormatHelper.ConvertToHSLColor(minColor);
            Assert.IsTrue(hue >= 0, "Hue should not be negative");
            Assert.IsTrue(saturation >= 0, "Saturation should not be negative");
            Assert.IsTrue(lightness >= 0, "Lightness should not be negative");
        }

        [TestMethod]
        public void Boundary_MaxRGB_ShouldConvertToHSL()
        {
            var maxColor = Color.FromArgb(255, 255, 255, 255);
            var (hue, saturation, lightness) = ColorFormatHelper.ConvertToHSLColor(maxColor);
            Assert.IsTrue(hue <= 360, "Hue should not exceed 360");
            Assert.IsTrue(saturation <= 100, "Saturation should not exceed 100");
            Assert.IsTrue(lightness <= 100, "Lightness should not exceed 100");
        }

        [TestMethod]
        public void Boundary_MaxRGB_ShouldConvertToHSV()
        {
            var maxColor = Color.FromArgb(255, 255, 255, 255);
            var (hue, saturation, value) = ColorFormatHelper.ConvertToHSVColor(maxColor);
            Assert.IsTrue(hue <= 360, "HSV hue should not exceed 360");
            Assert.IsTrue(saturation <= 100, "HSV saturation should not exceed 100");
            Assert.IsTrue(value <= 100, "HSV value should not exceed 100");
        }

        [TestMethod]
        public void Boundary_SingleChannel_RedOnly()
        {
            var color = Color.FromArgb(255, 1, 0, 0);
            var (hue, saturation, value) = ColorFormatHelper.ConvertToHSVColor(color);
            Assert.AreEqual(0, hue, "Minimal red should still have hue 0");
            Assert.AreEqual(100, saturation, "Minimal red should have full saturation");
        }

        [TestMethod]
        public void Boundary_SingleChannel_GreenOnly()
        {
            var color = Color.FromArgb(255, 0, 1, 0);
            var (hue, saturation, value) = ColorFormatHelper.ConvertToHSVColor(color);
            Assert.AreEqual(120, hue, "Minimal green should have hue 120");
        }

        [TestMethod]
        public void Boundary_SingleChannel_BlueOnly()
        {
            var color = Color.FromArgb(255, 0, 0, 1);
            var (hue, saturation, value) = ColorFormatHelper.ConvertToHSVColor(color);
            Assert.AreEqual(240, hue, "Minimal blue should have hue 240");
        }

        // =====================================================================
        // CIELAB conversion edge cases
        // =====================================================================

        [TestMethod]
        public void CIELAB_Black_ShouldHaveZeroLightness()
        {
            var black = Color.FromArgb(255, 0, 0, 0);
            var (lightness, a, b) = ColorFormatHelper.ConvertToCIELABColor(black);
            Assert.AreEqual(0, lightness, 1.0, "CIELAB black lightness should be near 0");
        }

        [TestMethod]
        public void CIELAB_White_ShouldHave100Lightness()
        {
            var white = Color.FromArgb(255, 255, 255, 255);
            var (lightness, a, b) = ColorFormatHelper.ConvertToCIELABColor(white);
            Assert.AreEqual(100, lightness, 1.0, "CIELAB white lightness should be near 100");
        }

        // =====================================================================
        // HWB conversion tests
        // =====================================================================

        [TestMethod]
        public void HWB_White_ShouldBe100Whiteness()
        {
            var white = Color.FromArgb(255, 255, 255, 255);
            var (hue, whiteness, blackness) = ColorFormatHelper.ConvertToHWBColor(white);
            Assert.AreEqual(100, whiteness, "White HWB whiteness should be 100");
            Assert.AreEqual(0, blackness, "White HWB blackness should be 0");
        }

        [TestMethod]
        public void HWB_Black_ShouldBe100Blackness()
        {
            var black = Color.FromArgb(255, 0, 0, 0);
            var (hue, whiteness, blackness) = ColorFormatHelper.ConvertToHWBColor(black);
            Assert.AreEqual(0, whiteness, "Black HWB whiteness should be 0");
            Assert.AreEqual(100, blackness, "Black HWB blackness should be 100");
        }

        // =====================================================================
        // Decimal and HEX Int format tests
        // =====================================================================

        [TestMethod]
        public void Decimal_Black_ShouldBeZero()
        {
            var black = Color.FromArgb(255, 0, 0, 0);
            var result = ColorFormatHelper.GetStringRepresentation(black, "%Dv");
            Assert.AreEqual("0", result, "Black decimal value should be 0");
        }

        [TestMethod]
        public void Decimal_White_ShouldBeMaxValue()
        {
            var white = Color.FromArgb(255, 255, 255, 255);
            var result = ColorFormatHelper.GetStringRepresentation(white, "%Dv");
            Assert.AreEqual("16777215", result, "White decimal value should be 16777215");
        }
    }
}
