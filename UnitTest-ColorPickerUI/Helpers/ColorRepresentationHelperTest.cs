using ColorPicker.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;

namespace UnitTest_ColorPickerUI.Helpers
{
    [TestClass]
    public class ColorRepresentationHelperTest
    {
        [TestMethod]
        [DataRow(ColorRepresentationType.CMYK, "cmyk(0%, 0%, 0%, 100%)")]
        [DataRow(ColorRepresentationType.HEX, "#000000")]
        [DataRow(ColorRepresentationType.HSL, "hsl(0, 0%, 0%)")]
        [DataRow(ColorRepresentationType.HSV, "hsv(0, 0%, 0%)")]
        [DataRow(ColorRepresentationType.RGB, "rgb(0, 0, 0)")]

        public void ColorRGBtoCMYKZeroDiv(ColorRepresentationType type, string expected)
        {
            var result = ColorRepresentationHelper.GetStringRepresentation(Color.Black, type);
            Assert.AreEqual(result, expected);
        }
    }
}
