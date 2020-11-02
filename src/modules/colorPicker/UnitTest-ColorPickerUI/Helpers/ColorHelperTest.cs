using System;
using System.Drawing;
using ColorPicker.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest_ColorPickerUI.Helpers
{
    /// <summary>
    /// Test class to test <see cref="ColorConverter"/>
    /// </summary>
    [TestClass]
    public class ColorConverterTest
    {
        // test values taken from https://de.wikipedia.org/wiki/HSV-Farbraum
        [TestMethod]
        [DataRow(000, 000, 000, 000, 000, 000)]      // Black
        [DataRow(000, 000, 100, 100, 100, 100)]      // White
        [DataRow(000, 100, 050, 100, 000, 000)]      // Red
        [DataRow(015, 100, 050, 100, 025, 000)]      // Vermilion/Cinnabar
        [DataRow(020, 060, 022.5, 036, 018, 009)]    // Brown
        [DataRow(030, 100, 050, 100, 050, 000)]      // Orange
        [DataRow(045, 100, 050, 100, 075, 000)]      // Saffron
        [DataRow(060, 100, 050, 100, 100, 000)]      // Yellow
        [DataRow(075, 100, 050, 075, 100, 000)]      // Light green-yellow
        [DataRow(090, 100, 050, 050, 100, 000)]      // Green-yellow
        [DataRow(105, 100, 050, 025, 100, 000)]      // Lime
        [DataRow(120, 100, 025, 000, 050, 000)]      // Dark green
        [DataRow(120, 100, 050, 000, 100, 000)]      // Green
        [DataRow(135, 100, 050, 000, 100, 025)]      // Light blue-green
        [DataRow(150, 100, 050, 000, 100, 050)]      // Blue-green
        [DataRow(165, 100, 050, 000, 100, 075)]      // Green-cyan
        [DataRow(180, 100, 050, 000, 100, 100)]      // Cyan
        [DataRow(195, 100, 050, 000, 075, 100)]      // Blue-cyan
        [DataRow(210, 100, 050, 000, 050, 100)]      // Green-blue
        [DataRow(225, 100, 050, 000, 025, 100)]      // Light green-blue
        [DataRow(240, 100, 050, 000, 000, 100)]      // Blue
        [DataRow(255, 100, 050, 025, 000, 100)]      // Indigo
        [DataRow(270, 100, 050, 050, 000, 100)]      // Purple
        [DataRow(285, 100, 050, 075, 000, 100)]      // Blue-magenta
        [DataRow(300, 100, 050, 100, 000, 100)]      // Magenta
        [DataRow(315, 100, 050, 100, 000, 075)]      // Red-magenta
        [DataRow(330, 100, 050, 100, 000, 050)]      // Blue-red
        [DataRow(345, 100, 050, 100, 000, 025)]      // Light blue-red
        public void ColorRGBtoHSL(double hue, double saturation, double lightness, int red, int green, int blue)
        {
            red   = Convert.ToInt32(Math.Round(255d / 100d * red));     // [0%..100%] to [0..255]
            green = Convert.ToInt32(Math.Round(255d / 100d * green));   // [0%..100%] to [0..255]
            blue  = Convert.ToInt32(Math.Round(255d / 100d * blue));    // [0%..100%] to [0..255]

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
        [DataRow(000, 000, 000, 000, 000, 000)]      // Black
        [DataRow(000, 000, 100, 100, 100, 100)]      // White
        [DataRow(000, 100, 100, 100, 000, 000)]      // Red
        [DataRow(015, 100, 100, 100, 025, 000)]      // Vermilion/Cinnabar
        [DataRow(020, 075, 036, 036, 018, 009)]      // Brown
        [DataRow(030, 100, 100, 100, 050, 000)]      // Orange
        [DataRow(045, 100, 100, 100, 075, 000)]      // Saffron
        [DataRow(060, 100, 100, 100, 100, 000)]      // Yellow
        [DataRow(075, 100, 100, 075, 100, 000)]      // Light green-yellow
        [DataRow(090, 100, 100, 050, 100, 000)]      // Green-yellow
        [DataRow(105, 100, 100, 025, 100, 000)]      // Lime
        [DataRow(120, 100, 050, 000, 050, 000)]      // Dark green
        [DataRow(120, 100, 100, 000, 100, 000)]      // Green
        [DataRow(135, 100, 100, 000, 100, 025)]      // Light blue-green
        [DataRow(150, 100, 100, 000, 100, 050)]      // Blue-green
        [DataRow(165, 100, 100, 000, 100, 075)]      // Green-cyan
        [DataRow(180, 100, 100, 000, 100, 100)]      // Cyan
        [DataRow(195, 100, 100, 000, 075, 100)]      // Blue-cyan
        [DataRow(210, 100, 100, 000, 050, 100)]      // Green-blue
        [DataRow(225, 100, 100, 000, 025, 100)]      // Light green-blue
        [DataRow(240, 100, 100, 000, 000, 100)]      // Blue
        [DataRow(255, 100, 100, 025, 000, 100)]      // Indigo
        [DataRow(270, 100, 100, 050, 000, 100)]      // Purple
        [DataRow(285, 100, 100, 075, 000, 100)]      // Blue-magenta
        [DataRow(300, 100, 100, 100, 000, 100)]      // Magenta
        [DataRow(315, 100, 100, 100, 000, 075)]      // Red-magenta
        [DataRow(330, 100, 100, 100, 000, 050)]      // Blue-red
        [DataRow(345, 100, 100, 100, 000, 025)]      // Light blue-red
        public void ColorRGBtoHSV(double hue, double saturation, double value, int red, int green, int blue)
        {
            red   = Convert.ToInt32(Math.Round(255d / 100d * red));         // [0%..100%] to [0..255]
            green = Convert.ToInt32(Math.Round(255d / 100d * green));       // [0%..100%] to [0..255]
            blue  = Convert.ToInt32(Math.Round(255d / 100d * blue));        // [0%..100%] to [0..255]

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
        [DataRow(000, 000, 000, 100, 000, 000, 000)]     // Black
        [DataRow(000, 000, 000, 000, 255, 255, 255)]     // White
        [DataRow(000, 100, 100, 000, 255, 000, 000)]     // Red
        [DataRow(000, 075, 100, 000, 255, 064, 000)]     // Vermilion/Cinnabar
        [DataRow(000, 050, 075, 064, 092, 046, 023)]     // Brown
        [DataRow(000, 050, 100, 000, 255, 128, 000)]     // Orange
        [DataRow(000, 025, 100, 000, 255, 192, 000)]     // Saffron
        [DataRow(000, 000, 100, 000, 255, 255, 000)]     // Yellow
        [DataRow(025, 000, 100, 000, 192, 255, 000)]     // Light green-yellow
        [DataRow(050, 000, 100, 000, 128, 255, 000)]     // Green-yellow
        [DataRow(075, 000, 100, 000, 064, 255, 000)]     // Lime
        [DataRow(100, 000, 100, 050, 000, 128, 000)]     // Dark green
        [DataRow(100, 000, 100, 000, 000, 255, 000)]     // Green
        [DataRow(100, 000, 075, 000, 000, 255, 064)]     // Light blue-green
        [DataRow(100, 000, 050, 000, 000, 255, 128)]     // Blue-green
        [DataRow(100, 000, 025, 000, 000, 255, 192)]     // Green-cyan
        [DataRow(100, 000, 000, 000, 000, 255, 255)]     // Cyan
        [DataRow(100, 025, 000, 000, 000, 192, 255)]     // Blue-cyan
        [DataRow(100, 050, 000, 000, 000, 128, 255)]     // Green-blue
        [DataRow(100, 075, 000, 000, 000, 064, 255)]     // Light green-blue
        [DataRow(100, 100, 000, 000, 000, 000, 255)]     // Blue
        [DataRow(075, 100, 000, 000, 064, 000, 255)]     // Indigo
        [DataRow(050, 100, 000, 000, 128, 000, 255)]     // Purple
        [DataRow(025, 100, 000, 000, 192, 000, 255)]     // Blue-magenta
        [DataRow(000, 100, 000, 000, 255, 000, 255)]     // Magenta
        [DataRow(000, 100, 025, 000, 255, 000, 192)]     // Red-magenta
        [DataRow(000, 100, 050, 000, 255, 000, 128)]     // Blue-red
        [DataRow(000, 100, 075, 000, 255, 000, 064)]     // Light blue-red
        public void ColorRGBtoCMYK(int cyan, int magenta, int yellow, int blackKey, int red, int green, int blue)
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

        [TestMethod]
        public void ColorRGBtoCMYKZeroDiv()
        {
            for(var red = 0; red < 256; red++)
            {
                for(var blue = 0; blue < 256; blue++)
                {
                    for(var green = 0; green < 256; green++)
                    {
                        var color = Color.FromArgb(red, green, blue);

                        Exception exception = null;

                        try
                        {
                            _ = ColorHelper.ConvertToCMYKColor(color);
                        }
                        catch(Exception ex)
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
