using System;
using System.Drawing;
using ColorPicker.Helpers;
using Xunit;

namespace UnitTest_ColorPickerUI.Helpers
{
    /// <summary>
    /// Test class to test <see cref="ColorConverter"/>
    /// </summary>
    public class ColorConverterTest
    {
        // test values taken from https://de.wikipedia.org/wiki/HSV-Farbraum
        [Theory]
        [InlineData(000, 000, 000, 000, 000, 000)]      // Black
        [InlineData(000, 000, 100, 100, 100, 100)]      // White
        [InlineData(000, 100, 050, 100, 000, 000)]      // Red
        [InlineData(015, 100, 050, 100, 025, 000)]      // Vermilion/Cinnabar
        [InlineData(020, 060, 022.5, 036, 018, 009)]    // Brown
        [InlineData(030, 100, 050, 100, 050, 000)]      // Orange
        [InlineData(045, 100, 050, 100, 075, 000)]      // Saffron
        [InlineData(060, 100, 050, 100, 100, 000)]      // Yellow
        [InlineData(075, 100, 050, 075, 100, 000)]      // Light green-yellow
        [InlineData(090, 100, 050, 050, 100, 000)]      // Green-yellow
        [InlineData(105, 100, 050, 025, 100, 000)]      // Lime
        [InlineData(120, 100, 025, 000, 050, 000)]      // Dark green
        [InlineData(120, 100, 050, 000, 100, 000)]      // Green
        [InlineData(135, 100, 050, 000, 100, 025)]      // Light blue-green
        [InlineData(150, 100, 050, 000, 100, 050)]      // Blue-green
        [InlineData(165, 100, 050, 000, 100, 075)]      // Green-cyan
        [InlineData(180, 100, 050, 000, 100, 100)]      // Cyan
        [InlineData(195, 100, 050, 000, 075, 100)]      // Blue-cyan
        [InlineData(210, 100, 050, 000, 050, 100)]      // Green-blue
        [InlineData(225, 100, 050, 000, 025, 100)]      // Light green-blue
        [InlineData(240, 100, 050, 000, 000, 100)]      // Blue
        [InlineData(255, 100, 050, 025, 000, 100)]      // Indigo
        [InlineData(270, 100, 050, 050, 000, 100)]      // Purple
        [InlineData(285, 100, 050, 075, 000, 100)]      // Blue-magenta
        [InlineData(300, 100, 050, 100, 000, 100)]      // Magenta
        [InlineData(315, 100, 050, 100, 000, 075)]      // Red-magenta
        [InlineData(330, 100, 050, 100, 000, 050)]      // Blue-red
        [InlineData(345, 100, 050, 100, 000, 025)]      // Light blue-red
        public void ColorRGBtoHSL(float hue, float saturation, float lightness, int red, int green, int blue)
        {
            red   = Convert.ToInt32(Math.Round(255f / 100f * red));     // [0%..100%] to [0..255]
            green = Convert.ToInt32(Math.Round(255f / 100f * green));   // [0%..100%] to [0..255]
            blue  = Convert.ToInt32(Math.Round(255f / 100f * blue));    // [0%..100%] to [0..255]

            var color = Color.FromArgb(255, red, green, blue);
            var result = ColorHelper.ConvertToHSLColor(color);

            // hue[0°..360°]
            Assert.InRange(result.hue, hue - 0.2, hue + 0.2);

            // saturation[0..1]
            Assert.InRange(result.saturation * 100, saturation - 0.2, saturation + 0.2);

            // lightness[0..1]
            Assert.InRange(result.lightness * 100, lightness - 0.2, lightness + 0.2);
        }

        // test values taken from https://de.wikipedia.org/wiki/HSV-Farbraum
        [Theory]
        [InlineData(000, 000, 000, 000, 000, 000)]      // Black
        [InlineData(000, 000, 100, 100, 100, 100)]      // White
        [InlineData(000, 100, 100, 100, 000, 000)]      // Red
        [InlineData(015, 100, 100, 100, 025, 000)]      // Vermilion/Cinnabar
        [InlineData(020, 075, 036, 036, 018, 009)]      // Brown
        [InlineData(030, 100, 100, 100, 050, 000)]      // Orange
        [InlineData(045, 100, 100, 100, 075, 000)]      // Saffron
        [InlineData(060, 100, 100, 100, 100, 000)]      // Yellow
        [InlineData(075, 100, 100, 075, 100, 000)]      // Light green-yellow
        [InlineData(090, 100, 100, 050, 100, 000)]      // Green-yellow
        [InlineData(105, 100, 100, 025, 100, 000)]      // Lime
        [InlineData(120, 100, 050, 000, 050, 000)]      // Dark green
        [InlineData(120, 100, 100, 000, 100, 000)]      // Green
        [InlineData(135, 100, 100, 000, 100, 025)]      // Light blue-green
        [InlineData(150, 100, 100, 000, 100, 050)]      // Blue-green
        [InlineData(165, 100, 100, 000, 100, 075)]      // Green-cyan
        [InlineData(180, 100, 100, 000, 100, 100)]      // Cyan
        [InlineData(195, 100, 100, 000, 075, 100)]      // Blue-cyan
        [InlineData(210, 100, 100, 000, 050, 100)]      // Green-blue
        [InlineData(225, 100, 100, 000, 025, 100)]      // Light green-blue
        [InlineData(240, 100, 100, 000, 000, 100)]      // Blue
        [InlineData(255, 100, 100, 025, 000, 100)]      // Indigo
        [InlineData(270, 100, 100, 050, 000, 100)]      // Purple
        [InlineData(285, 100, 100, 075, 000, 100)]      // Blue-magenta
        [InlineData(300, 100, 100, 100, 000, 100)]      // Magenta
        [InlineData(315, 100, 100, 100, 000, 075)]      // Red-magenta
        [InlineData(330, 100, 100, 100, 000, 050)]      // Blue-red
        [InlineData(345, 100, 100, 100, 000, 025)]      // Light blue-red
        public void ColorRGBtoHSV(float hue, float saturation, float value, int red, int green, int blue)
        {
            red   = Convert.ToInt32(Math.Round(255f / 100f * red));         // [0%..100%] to [0..255]
            green = Convert.ToInt32(Math.Round(255f / 100f * green));       // [0%..100%] to [0..255]
            blue  = Convert.ToInt32(Math.Round(255f / 100f * blue));        // [0%..100%] to [0..255]

            var color = Color.FromArgb(255, red, green, blue);
            var result = ColorHelper.ConvertToHSVColor(color);

            // hue [0°..360°]
            Assert.InRange(result.hue, hue - 0.2, hue + 0.2);

            // saturation[0..1]
            Assert.InRange(result.saturation * 100, saturation - 0.2, saturation + 0.2);

            // value[0..1]
            Assert.InRange(result.value * 100, value - 0.2, value + 0.2);
        }

        [Theory]
        [InlineData(000, 000, 000, 100, 000, 000, 000)]     // Black
        [InlineData(000, 000, 000, 000, 255, 255, 255)]     // White
        [InlineData(000, 100, 100, 000, 255, 000, 000)]     // Red
        [InlineData(000, 075, 100, 000, 255, 064, 000)]     // Vermilion/Cinnabar
        [InlineData(000, 050, 075, 064, 092, 046, 023)]     // Brown
        [InlineData(000, 050, 100, 000, 255, 128, 000)]     // Orange
        [InlineData(000, 025, 100, 000, 255, 192, 000)]     // Saffron
        [InlineData(000, 000, 100, 000, 255, 255, 000)]     // Yellow
        [InlineData(025, 000, 100, 000, 192, 255, 000)]     // Light green-yellow
        [InlineData(050, 000, 100, 000, 128, 255, 000)]     // Green-yellow
        [InlineData(075, 000, 100, 000, 064, 255, 000)]     // Lime
        [InlineData(100, 000, 100, 050, 000, 128, 000)]     // Dark green
        [InlineData(100, 000, 100, 000, 000, 255, 000)]     // Green
        [InlineData(100, 000, 075, 000, 000, 255, 064)]     // Light blue-green
        [InlineData(100, 000, 050, 000, 000, 255, 128)]     // Blue-green
        [InlineData(100, 000, 025, 000, 000, 255, 192)]     // Green-cyan
        [InlineData(100, 000, 000, 000, 000, 255, 255)]     // Cyan
        [InlineData(100, 025, 000, 000, 000, 192, 255)]     // Blue-cyan
        [InlineData(100, 050, 000, 000, 000, 128, 255)]     // Green-blue
        [InlineData(100, 075, 000, 000, 000, 064, 255)]     // Light green-blue
        [InlineData(100, 100, 000, 000, 000, 000, 255)]     // Blue
        [InlineData(075, 100, 000, 000, 064, 000, 255)]     // Indigo
        [InlineData(050, 100, 000, 000, 128, 000, 255)]     // Purple
        [InlineData(025, 100, 000, 000, 192, 000, 255)]     // Blue-magenta
        [InlineData(000, 100, 000, 000, 255, 000, 255)]     // Magenta
        [InlineData(000, 100, 025, 000, 255, 000, 192)]     // Red-magenta
        [InlineData(000, 100, 050, 000, 255, 000, 128)]     // Blue-red
        [InlineData(000, 100, 075, 000, 255, 000, 064)]     // Light blue-red
        public void ColorRGBtoCMYK(byte cyan, byte magenta, byte yellow, byte blackKey, int red, int green, int blue)
        {
            var color = Color.FromArgb(255, red, green, blue);
            var result = ColorHelper.ConvertToCMYKColor(color);

            // cyan[0..1]
            Assert.InRange(result.cyan * 100, cyan - 0.5, cyan + 0.5);

            // magenta[0..1]
            Assert.InRange(result.magenta * 100, magenta - 0.5, magenta + 0.5);

            // yellow[0..1]
            Assert.InRange(result.yellow * 100, yellow - 0.5, yellow + 0.5);

            // black[0..1]
            Assert.InRange(result.blackKey * 100, blackKey - 0.5, blackKey + 0.5);
        }
    }
}
