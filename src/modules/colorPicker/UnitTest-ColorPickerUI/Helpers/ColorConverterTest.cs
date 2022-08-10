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
        public void ColorRGBtoHSBTest(double hue, double saturation, double value, int red, int green, int blue)
        {
            red = Convert.ToInt32(Math.Round(255d / 100d * red));         // [0%..100%] to [0..255]
            green = Convert.ToInt32(Math.Round(255d / 100d * green));       // [0%..100%] to [0..255]
            blue = Convert.ToInt32(Math.Round(255d / 100d * blue));        // [0%..100%] to [0..255]

            var color = Color.FromArgb(255, red, green, blue);
            var result = ColorHelper.ConvertToHSBColor(color);

            // hue [0°..360°]
            Assert.AreEqual(result.hue, hue, 0.2d);

            // saturation[0..1]
            Assert.AreEqual(result.saturation * 100d, saturation, 0.2d);

            // value[0..1]
            Assert.AreEqual(result.brightness * 100d, value, 0.2d);
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

            var red = int.Parse(hexValue.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var green = int.Parse(hexValue.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var blue = int.Parse(hexValue.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

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

            var red = int.Parse(hexValue.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var green = int.Parse(hexValue.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var blue = int.Parse(hexValue.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

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

            var red = int.Parse(hexValue.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var green = int.Parse(hexValue.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var blue = int.Parse(hexValue.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

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
        [DataRow("FFFFFF", 100.00, 0.00, 0.00)] // white
        [DataRow("808080", 53.59, 0.00, 0.00)] // gray
        [DataRow("000000", 0.00, 0.00, 0.00)] // black
        [DataRow("FF0000", 53.24, 80.09, 67.20)] // red
        [DataRow("008000", 46.23, -51.70, 49.90)] // green
        [DataRow("80FFFF", 93.16, -35.23, -10.87)] // cyan
        [DataRow("8080FF", 59.20, 33.10, -63.46)] // blue
        [DataRow("BF40BF", 50.10, 65.50, -41.48)] // magenta
        [DataRow("BFBF00", 75.04, -17.35, 76.03)] // yellow
        [DataRow("008000", 46.23, -51.70, 49.90)] // green
        [DataRow("8080FF", 59.20, 33.10, -63.46)] // blue
        [DataRow("BF40BF", 50.10, 65.50, -41.48)] // magenta
        [DataRow("0048BA", 34.35, 27.94, -64.80)] // absolute zero
        [DataRow("B0BF1A", 73.91, -23.39, 71.15)] // acid green
        [DataRow("D0FF14", 93.87, -40.20, 88.97)] // arctic lime
        [DataRow("1B4D3E", 29.13, -20.96, 3.95)] // brunswick green
        [DataRow("FFEF00", 93.01, -13.86, 91.48)] // canary yellow
        [DataRow("FFA600", 75.16, 23.41, 79.10)] // cheese
        [DataRow("1A2421", 13.18, -5.23, 0.56)] // dark jungle green
        [DataRow("003399", 25.76, 28.89, -59.09)] // dark powder blue
        [DataRow("D70A53", 46.03, 71.90, 18.03)] // debian red
        [DataRow("80FFD5", 92.09, -45.08, 9.29)] // fathom secret green
        [DataRow("EFDFBB", 89.26, -0.13, 19.65)] // dutch white
        [DataRow("5218FA", 36.65, 75.63, -97.70)] // han purple
        [DataRow("FF496C", 59.08, 69.89, 21.80)] // infra red
        [DataRow("545AA7", 41.20, 19.32, -42.34)] // liberty
        [DataRow("E6A8D7", 75.91, 30.13, -14.79)] // light orchid
        [DataRow("ADDFAD", 84.32, -25.67, 19.37)] // light moss green
        [DataRow("E3F988", 94.25, -23.70, 51.58)] // mindaro
        public void ColorRGBtoCIELABTest(string hexValue, double lightness, double chromaticityA, double chromaticityB)
        {
            if (string.IsNullOrWhiteSpace(hexValue))
            {
                Assert.IsNotNull(hexValue);
            }

            Assert.IsTrue(hexValue.Length >= 6);

            var red = int.Parse(hexValue.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var green = int.Parse(hexValue.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var blue = int.Parse(hexValue.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            var color = Color.FromArgb(255, red, green, blue);
            var result = ColorHelper.ConvertToCIELABColor(color);

            // lightness[0..100]
            Assert.AreEqual(Math.Round(result.lightness, 2), lightness);

            // chromaticityA[-128..127]
            Assert.AreEqual(Math.Round(result.chromaticityA, 2), chromaticityA);

            // chromaticityB[-128..127]
            Assert.AreEqual(Math.Round(result.chromaticityB, 2), chromaticityB);
        }

        // The following results are computed using LittleCMS2, an open-source color management engine,
        // with the following command-line arguments:
        //   echo 0xFF 0xFF 0xFF | transicc -i "*sRGB" -o "*XYZ" -t 3 -d 0
        // where "0xFF 0xFF 0xFF" are filled in with the hexadecimal red/green/blue values;
        // "-t 3" means using absolute colorimetric intent, in other words, disabling white point scaling;
        // "-d 0" means disabling chromatic adaptation, otherwise it will output CIEXYZ-D50 instead of D65.
        //
        // If we have the same results as the reference output listed below, it means our algorithm is accurate.
        [TestMethod]
        [DataRow("FFFFFF", 95.0456, 100.0000, 108.9058)] // white
        [DataRow("808080", 20.5166, 21.5861, 23.5085)] // gray
        [DataRow("000000", 0.0000, 0.0000, 0.0000)] // black
        [DataRow("FF0000", 41.2391, 21.2639, 1.9331)] // red
        [DataRow("008000", 7.7188, 15.4377, 2.5729)] // green
        [DataRow("80FFFF", 62.7084, 83.3261, 107.3900)] // cyan
        [DataRow("8080FF", 34.6688, 27.2469, 98.0434)] // blue
        [DataRow("BF40BF", 32.7217, 18.5062, 51.1405)] // magenta
        [DataRow("BFBF00", 40.1154, 48.3384, 7.2171)] // yellow
        [DataRow("008000", 7.7188, 15.4377, 2.5729)] // green
        [DataRow("8080FF", 34.6688, 27.2469, 98.0434)] // blue
        [DataRow("BF40BF", 32.7217, 18.5062, 51.1405)] // magenta
        [DataRow("0048BA", 11.1792, 8.1793, 47.4455)] // absolute zero
        [DataRow("B0BF1A", 36.7205, 46.5663, 8.0311)] // acid green
        [DataRow("D0FF14", 61.8965, 84.9797, 13.8037)] // arctic lime
        [DataRow("1B4D3E", 3.9752, 5.8883, 5.4847)] // brunswick green
        [DataRow("FFEF00", 72.1042, 82.9942, 12.2215)] // canary yellow
        [DataRow("FFA600", 54.8747, 48.5351, 6.4783)] // cheese
        [DataRow("1A2421", 1.3313, 1.5911, 1.6759)] // dark jungle green
        [DataRow("003399", 6.9329, 4.6672, 30.6735)] // dark powder blue
        [DataRow("D70A53", 29.6934, 15.2913, 9.5719)] // debian red
        [DataRow("80FFD5", 56.6693, 80.9105, 75.5840)] // fathom secret green
        [DataRow("EFDFBB", 70.9510, 74.7146, 57.6991)] // dutch white
        [DataRow("5218FA", 21.0597, 9.3488, 91.1403)] // han purple
        [DataRow("FF496C", 46.3280, 27.1114, 16.9814)] // infra red
        [DataRow("545AA7", 14.2864, 11.9869, 38.1214)] // liberty
        [DataRow("E6A8D7", 58.8989, 49.7359, 70.7897)] // light orchid
        [DataRow("ADDFAD", 51.1617, 64.6757, 49.3246)] // light moss green
        [DataRow("E3F988", 69.9955, 85.8597, 36.1785)] // mindaro
        public void ColorRGBtoCIEXYZTest(string hexValue, double x, double y, double z)
        {
            if (string.IsNullOrWhiteSpace(hexValue))
            {
                Assert.IsNotNull(hexValue);
            }

            Assert.IsTrue(hexValue.Length >= 6);

            var red = int.Parse(hexValue.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var green = int.Parse(hexValue.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var blue = int.Parse(hexValue.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            var color = Color.FromArgb(255, red, green, blue);
            var result = ColorHelper.ConvertToCIEXYZColor(color);

            // x[0..0.95047]
            Assert.AreEqual(Math.Round(result.x * 100, 4), x);

            // y[0..1]
            Assert.AreEqual(Math.Round(result.y * 100, 4), y);

            // z[0..1.08883]
            Assert.AreEqual(Math.Round(result.z * 100, 4), z);
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

                        // intentionally trying to catch
                        catch (Exception ex)
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
