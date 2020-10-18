using ColorPicker.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib;
using System.Drawing;
using Xunit;

namespace UnitTest_ColorPickerUI.Helpers
{
    public class ColorRepresentationHelperTest
    {
        [Theory]
        [InlineData(ColorRepresentationType.CMYK, "cmyk(0%, 0%, 0%, 100%)")]
        [InlineData(ColorRepresentationType.HEX, "#000000")]
        [InlineData(ColorRepresentationType.HSL, "hsl(0, 0%, 0%)")]
        [InlineData(ColorRepresentationType.HSV, "hsv(0, 0%, 0%)")]
        [InlineData(ColorRepresentationType.RGB, "rgb(0, 0, 0)")]

        public void ColorRGBtoCMYKZeroDiv(ColorRepresentationType type, string expected)
        {
            var result = ColorRepresentationHelper.GetStringRepresentation(Color.Black, type);
            Assert.Equal(result, expected);
        }
    }
}
