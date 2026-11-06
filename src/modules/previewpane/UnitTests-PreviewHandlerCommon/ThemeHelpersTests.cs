// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

using ManagedCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PreviewHandlerCommonUnitTests
{
    [TestClass]
    public class ThemeHelpersTests
    {
        private static readonly string[] BaseColors = ["Light", "Dark"];

        [DataTestMethod]
        [DataRow((byte)0, (byte)0, (byte)0, "Dark")] // "High Contrast Black" window background
        [DataRow((byte)255, (byte)255, (byte)255, "Light")] // "High Contrast White" window background
        [DataRow((byte)32, (byte)32, (byte)32, "Dark")] // dark contrast theme window background, e.g. Aquatic (#202020)
        [DataRow((byte)255, (byte)250, (byte)239, "Light")] // light contrast theme window background, e.g. Desert (#FFFAEF)
        [DataRow((byte)127, (byte)127, (byte)127, "Dark")] // lightness (127 + 127) / 510 < 0.5
        [DataRow((byte)128, (byte)127, (byte)127, "Light")] // lightness (128 + 127) / 510 == 0.5
        [DataRow((byte)255, (byte)0, (byte)0, "Light")] // fully saturated colors have a lightness of 0.5
        [DataRow((byte)0, (byte)254, (byte)0, "Dark")]
        public void GetBaseColorFromWindowColorShouldClassifyByLightness(byte red, byte green, byte blue, string expected)
        {
            Assert.AreEqual(expected, ThemeHelpers.GetBaseColorFromWindowColor(red, green, blue));
        }

        [TestMethod]
        public void GetBaseColorFromWindowColorShouldMatchSystemDrawingBrightness()
        {
            // ControlzEx, which GetWindowsBaseColor replaces, used Color.GetBrightness() < 0.5 for the high contrast window color.
            // The brightness only depends on the largest and smallest channel, so two channels cover every combination.
            for (var red = 0; red <= byte.MaxValue; red++)
            {
                for (var green = 0; green <= byte.MaxValue; green++)
                {
                    var expected = Color.FromArgb(red, green, green).GetBrightness() < .5 ? "Dark" : "Light";
                    var actual = ThemeHelpers.GetBaseColorFromWindowColor((byte)red, (byte)green, (byte)green);
                    if (expected != actual)
                    {
                        Assert.Fail($"RGB({red}, {green}, {green}) was classified as {actual} instead of {expected}.");
                    }
                }
            }
        }

        [TestMethod]
        public void GetWindowsBaseColorShouldReturnLightOrDark()
        {
            CollectionAssert.Contains(BaseColors, ThemeHelpers.GetWindowsBaseColor());
        }
    }
}
