// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Globalization;
using ColorPicker.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ColorPicker.UnitTests
{
    /// <summary>
    /// Test class to test <see cref="ColorConverter"/>
    /// </summary>
    [TestClass]
    public class ColorConverterTest
    {
        // test values taken from https://de.wikipedia.org/wiki/HSV-Farbraum
        [TestMethod]
        [DataRow(000, 000, 000, 000, 000, 000)] // Black
        [DataRow(000, 000, 100, 100, 100, 100)] // White
        [DataRow(000, 100, 050, 100, 000, 000)] // Red
        [DataRow(015, 100, 050, 100, 025, 000)] // Vermilion/Cinnabar
        [DataRow(020, 060, 022.5, 036, 018, 009)] // Brown
        [DataRow(030, 100, 050, 100, 050, 000)] // Orange
        [DataRow(045, 100, 050, 100, 075, 000)] // Saffron
        [DataRow(060, 100, 050, 100, 100, 000)] // Yellow
        [DataRow(075, 100, 050, 075, 100, 000)] // Light green-yellow
        [DataRow(090, 100, 050, 050, 100, 000)] // Green-yellow
        [DataRow(105, 100, 050, 025, 100, 000)] // Lime
        [DataRow(120, 100, 025, 000, 050, 000)] // Dark green
        [DataRow(120, 100, 050, 000, 100, 000)] // Green
        [DataRow(135, 100, 050, 000, 100, 025)] // Light blue-green
        [DataRow(150, 100, 050, 000, 100, 050)] // Blue-green
        [DataRow(165, 100, 050, 000, 100, 075)] // Green-cyan
        [DataRow(180, 100, 050, 000, 100, 100)] // Cyan
        [DataRow(195, 100, 050, 000, 075, 100)] // Blue-cyan
        [DataRow(210, 100, 050, 000, 050, 100)] // Green-blue
        [DataRow(225, 100, 050, 000, 025, 100)] // Light green-blue
        [DataRow(240, 100, 050, 000, 000, 100)] // Blue
        [DataRow(255, 100, 050, 025, 000, 100)] // Indigo
        [DataRow(270, 100, 050, 050, 000, 100)] // Purple
        [DataRow(285, 100, 050, 075, 000, 100)] // Blue-magenta
        [DataRow(300, 100, 050, 100, 000, 100)] // Magenta
        [DataRow(315, 100, 050, 100, 000, 075)] // Red-magenta
        [DataRow(330, 100, 050, 100, 000, 050)] // Blue-red
        [DataRow(345, 100, 050, 100, 000, 025)] // Light blue-red
        public void ColorRGBtoHSLTest(double hue, double saturation, double lightness, int red, int green, int blue)
        {
            red = Convert.ToInt32(Math.Round(255d / 100d * red));     // [0%..100%] to [0..255]
            green = Convert.ToInt32(Math.Round(255d / 100d * green));   // [0%..100%] to [0..255]
            blue = Convert.ToInt32(Math.Round(255d / 100d * blue));    // [0%..100%] to [0..255]

            var color = Color.FromArgb(255, red, green, blue);
            var result = ColorHelper.ConvertToHSLColor(color);

            // hue[0°..360°]
            Assert.AreEqual(result.hue, hue, 0.2d);

            // saturation[0..1]
            Assert.AreEqual(result.saturation * 100d, saturation, 0.2d);

            // lightness[0..1]
            Assert.AreEqual(result.lightness * 100d, lightness, 0.2d);
        }

        // test values taken from https://de.wikipedia.org/wiki/HSV-Farbraum
        [TestMethod]
        [DataRow(000, 000, 000, 000, 000, 000)] // Black
        [DataRow(000, 000, 100, 100, 100, 100)] // White
        [DataRow(000, 100, 100, 100, 000, 000)] // Red
        [DataRow(015, 100, 100, 100, 025, 000)] // Vermilion/Cinnabar
        [DataRow(020, 075, 036, 036, 018, 009)] // Brown
        [DataRow(030, 100, 100, 100, 050, 000)] // Orange
        [DataRow(045, 100, 100, 100, 075, 000)] // Saffron
        [DataRow(060, 100, 100, 100, 100, 000)] // Yellow
        [DataRow(075, 100, 100, 075, 100, 000)] // Light green-yellow
        [DataRow(090, 100, 100, 050, 100, 000)] // Green-yellow
        [DataRow(105, 100, 100, 025, 100, 000)] // Lime
        [DataRow(120, 100, 050, 000, 050, 000)] // Dark green
        [DataRow(120, 100, 100, 000, 100, 000)] // Green
        [DataRow(135, 100, 100, 000, 100, 025)] // Light blue-green
        [DataRow(150, 100, 100, 000, 100, 050)] // Blue-green
        [DataRow(165, 100, 100, 000, 100, 075)] // Green-cyan
        [DataRow(180, 100, 100, 000, 100, 100)] // Cyan
        [DataRow(195, 100, 100, 000, 075, 100)] // Blue-cyan
        [DataRow(210, 100, 100, 000, 050, 100)] // Green-blue
        [DataRow(225, 100, 100, 000, 025, 100)] // Light green-blue
        [DataRow(240, 100, 100, 000, 000, 100)] // Blue
        [DataRow(255, 100, 100, 025, 000, 100)] // Indigo
        [DataRow(270, 100, 100, 050, 000, 100)] // Purple
        [DataRow(285, 100, 100, 075, 000, 100)] // Blue-magenta
        [DataRow(300, 100, 100, 100, 000, 100)] // Magenta
        [DataRow(315, 100, 100, 100, 000, 075)] // Red-magenta
        [DataRow(330, 100, 100, 100, 000, 050)] // Blue-red
        [DataRow(345, 100, 100, 100, 000, 025)] // Light blue-red
        public void ColorRGBtoHSVTest(double hue, double saturation, double value, int red, int green, int blue)
        {
            red = Convert.ToInt32(Math.Round(255d / 100d * red));         // [0%..100%] to [0..255]
            green = Convert.ToInt32(Math.Round(255d / 100d * green));       // [0%..100%] to [0..255]
            blue = Convert.ToInt32(Math.Round(255d / 100d * blue));        // [0%..100%] to [0..255]

            var color = Color.FromArgb(255, red, green, blue);
            var result = ColorHelper.ConvertToHSVColor(color);

            // hue [0°..360°]
            Assert.AreEqual(result.hue, hue, 0.2d);

            // saturation[0..1]
            Assert.AreEqual(result.saturation * 100d, saturation, 0.2d);

            // value[0..1]
            Assert.AreEqual(result.value * 100d, value, 0.2d);
        }

        [TestMethod]
        [DataRow(000, 000, 000, 100, 000, 000, 000)] // Black
        [DataRow(000, 000, 000, 000, 255, 255, 255)] // White
        [DataRow(000, 100, 100, 000, 255, 000, 000)] // Red
        [DataRow(000, 075, 100, 000, 255, 064, 000)] // Vermilion/Cinnabar
        [DataRow(000, 050, 075, 064, 092, 046, 023)] // Brown
        [DataRow(000, 050, 100, 000, 255, 128, 000)] // Orange
        [DataRow(000, 025, 100, 000, 255, 192, 000)] // Saffron
        [DataRow(000, 000, 100, 000, 255, 255, 000)] // Yellow
        [DataRow(025, 000, 100, 000, 192, 255, 000)] // Light green-yellow
        [DataRow(050, 000, 100, 000, 128, 255, 000)] // Green-yellow
        [DataRow(075, 000, 100, 000, 064, 255, 000)] // Lime
        [DataRow(100, 000, 100, 050, 000, 128, 000)] // Dark green
        [DataRow(100, 000, 100, 000, 000, 255, 000)] // Green
        [DataRow(100, 000, 075, 000, 000, 255, 064)] // Light blue-green
        [DataRow(100, 000, 050, 000, 000, 255, 128)] // Blue-green
        [DataRow(100, 000, 025, 000, 000, 255, 192)] // Green-cyan
        [DataRow(100, 000, 000, 000, 000, 255, 255)] // Cyan
        [DataRow(100, 025, 000, 000, 000, 192, 255)] // Blue-cyan
        [DataRow(100, 050, 000, 000, 000, 128, 255)] // Green-blue
        [DataRow(100, 075, 000, 000, 000, 064, 255)] // Light green-blue
        [DataRow(100, 100, 000, 000, 000, 000, 255)] // Blue
        [DataRow(075, 100, 000, 000, 064, 000, 255)] // Indigo
        [DataRow(050, 100, 000, 000, 128, 000, 255)] // Purple
        [DataRow(025, 100, 000, 000, 192, 000, 255)] // Blue-magenta
        [DataRow(000, 100, 000, 000, 255, 000, 255)] // Magenta
        [DataRow(000, 100, 025, 000, 255, 000, 192)] // Red-magenta
        [DataRow(000, 100, 050, 000, 255, 000, 128)] // Blue-red
        [DataRow(000, 100, 075, 000, 255, 000, 064)] // Light blue-red
        public void ColorRGBtoCMYKTest(int cyan, int magenta, int yellow, int blackKey, int red, int green, int blue)
        {
            var color = Color.FromArgb(255, red, green, blue);
            var result = ColorHelper.ConvertToCMYKColor(color);

            // cyan[0..1]
            Assert.AreEqual(result.cyan * 100d, cyan, 0.5d);

            // magenta[0..1]
            Assert.AreEqual(result.magenta * 100d, magenta, 0.5d);

            // yellow[0..1]
            Assert.AreEqual(result.yellow * 100d, yellow, 0.5d);

            // black[0..1]
            Assert.AreEqual(result.blackKey * 100d, blackKey, 0.5d);
        }

        // values taken from https://en.wikipedia.org/wiki/HSL_and_HSV#Examples
        [TestMethod]
        [DataRow("FFFFFF", 000.0, 000.0, 100.0)] // white
        [DataRow("808080", 000.0, 000.0, 050.0)] // gray
        [DataRow("000000", 000.0, 000.0, 000.0)] // black
        [DataRow("FF0000", 000.0, 100.0, 033.3)] // red
        [DataRow("BFBF00", 060.0, 100.0, 050.0)] // yellow
        [DataRow("008000", 120.0, 100.0, 016.7)] // green
        [DataRow("80FFFF", 180.0, 040.0, 083.3)] // cyan
        [DataRow("8080FF", 240.0, 025.0, 066.7)] // blue
        [DataRow("BF40BF", 300.0, 057.1, 058.3)] // magenta
        [DataRow("A0A424", 061.8, 069.9, 047.1)]
        [DataRow("411BEA", 251.1, 075.6, 042.6)]
        [DataRow("1EAC41", 134.9, 066.7, 034.9)]
        [DataRow("F0C80E", 049.5, 091.1, 059.3)]
        [DataRow("B430E5", 283.7, 068.6, 059.6)]
        [DataRow("ED7651", 014.3, 044.6, 057.0)]
        [DataRow("FEF888", 056.9, 036.3, 083.5)]
        [DataRow("19CB97", 162.4, 080.0, 049.5)]
        [DataRow("362698", 248.3, 053.3, 031.9)]
        [DataRow("7E7EB8", 240.5, 013.5, 057.0)]
        public void ColorRGBtoHSITest(string hexValue, double hue, double saturation, double intensity)
        {
            if (string.IsNullOrWhiteSpace(hexValue))
            {
                Assert.IsNotNull(hexValue);
            }

            Assert.IsTrue(hexValue.Length >= 6);

            var red = int.Parse(hexValue.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var green = int.Parse(hexValue.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var blue = int.Parse(hexValue.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            var color = Color.FromArgb(255, red, green, blue);
            var result = ColorHelper.ConvertToHSIColor(color);

            // hue[0°..360°]
            Assert.AreEqual(result.hue, hue, 0.5d);

            // saturation[0..1]
            Assert.AreEqual(result.saturation * 100d, saturation, 0.5d);

            // intensity[0..1]
            Assert.AreEqual(result.intensity * 100d, intensity, 0.5d);
        }

        // values taken from https://en.wikipedia.org/wiki/HSL_and_HSV#Examples
        // and manual convert via https://colorconv.com/hwb
        [TestMethod]
        [DataRow("FFFFFF", 000, 100, 000)] // white
        [DataRow("808080", 000, 050, 050)] // gray
        [DataRow("000000", 000, 000, 100)] // black
        [DataRow("FF0000", 000, 000, 000)] // red
        [DataRow("BFBF00", 060, 000, 025)] // yellow
        [DataRow("008000", 120, 000, 050)] // green
        [DataRow("80FFFF", 180, 050, 000)] // cyan
        [DataRow("8080FF", 240, 050, 000)] // blue
        [DataRow("BF40BF", 300, 025, 025)] // magenta
        [DataRow("A0A424", 062, 014, 036)]
        [DataRow("411BEA", 251, 011, 008)]
        [DataRow("1EAC41", 135, 012, 033)]
        [DataRow("F0C80E", 049, 005, 006)]
        [DataRow("B430E5", 284, 019, 010)]
        [DataRow("ED7651", 014, 032, 007)]
        [DataRow("FEF888", 057, 053, 000)]
        [DataRow("19CB97", 162, 010, 020)]
        [DataRow("362698", 248, 015, 040)]
        [DataRow("7E7EB8", 240, 049, 028)]
        public void ColorRGBtoHWBTest(string hexValue, double hue, double whiteness, double blackness)
        {
            if (string.IsNullOrWhiteSpace(hexValue))
            {
                Assert.IsNotNull(hexValue);
            }

            Assert.IsTrue(hexValue.Length >= 6);

            var red = int.Parse(hexValue.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var green = int.Parse(hexValue.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var blue = int.Parse(hexValue.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            var color = Color.FromArgb(255, red, green, blue);
            var result = ColorHelper.ConvertToHWBColor(color);

            // hue[0°..360°]
            Assert.AreEqual(result.hue, hue, 0.5d);

            // whiteness[0..1]
            Assert.AreEqual(result.whiteness * 100d, whiteness, 0.5d);

            // blackness[0..1]
            Assert.AreEqual(result.blackness * 100d, blackness, 0.5d);
        }

        // values taken from https://en.wikipedia.org/wiki/HSL_and_HSV#Examples
        // and manual convert via https://colorconv.com/hwb
        [TestMethod]
        [DataRow("FFFFFF", "R0", 100, 000)] // white
        [DataRow("808080", "R0", 050, 050)] // gray
        [DataRow("000000", "R0", 000, 100)] // black
        [DataRow("FF0000", "R0", 000, 000)] // red
        [DataRow("BFBF00", "Y0", 000, 025)] // yellow
        [DataRow("008000", "G0", 000, 050)] // green
        [DataRow("80FFFF", "C0", 050, 000)] // cyan
        [DataRow("8080FF", "B0", 050, 000)] // blue
        [DataRow("BF40BF", "M0", 025, 025)] // magenta
        [DataRow("A0A424", "Y3", 014, 036)]
        [DataRow("411BEA", "B18", 011, 008)]
        [DataRow("1EAC41", "G25", 012, 033)]
        [DataRow("F0C80E", "R82", 005, 006)]
        [DataRow("B430E5", "B73", 019, 010)]
        [DataRow("ED7651", "R24", 032, 007)]
        [DataRow("FEF888", "R95", 053, 000)]
        [DataRow("19CB97", "G71", 010, 020)]
        [DataRow("362698", "B14", 015, 040)]
        [DataRow("7E7EB8", "B0", 049, 028)]
        public void ColorRGBtoNColTest(string hexValue, string hue, double whiteness, double blackness)
        {
            if (string.IsNullOrWhiteSpace(hexValue))
            {
                Assert.IsNotNull(hexValue);
            }

            Assert.IsTrue(hexValue.Length >= 6);

            var red = int.Parse(hexValue.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var green = int.Parse(hexValue.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var blue = int.Parse(hexValue.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            var color = Color.FromArgb(255, red, green, blue);
            var result = ColorHelper.ConvertToNaturalColor(color);

            // hue
            Assert.AreEqual(result.hue, hue);

            // whiteness[0..1]
            Assert.AreEqual(result.whiteness * 100d, whiteness, 0.5d);

            // blackness[0..1]
            Assert.AreEqual(result.blackness * 100d, blackness, 0.5d);
        }

        [TestMethod]
        public void ColorRGBtoCMYKZeroDivTest()
        {
            for (var red = 0; red < 256; red++)
            {
                for (var blue = 0; blue < 256; blue++)
                {
                    for (var green = 0; green < 256; green++)
                    {
                        var color = Color.FromArgb(red, green, blue);

                        Exception? exception = null;

                        try
                        {
                            _ = ColorHelper.ConvertToCMYKColor(color);
                        }
#pragma warning disable CA1031 // Do not catch general exception types

                        // intentionally trying to catch
                        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                        {
                            exception = ex;
                        }

                        Assert.IsNull(exception);
                    }
                }
            }
        }
    }
}
