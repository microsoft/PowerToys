// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using ColorPicker.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ColorPicker.UnitTests
{
    [TestClass]
    public class ColorRepresentationHelperTest
    {
        [TestMethod]
        [DataRow(ColorRepresentationType.CMYK, "cmyk(0%, 0%, 0%, 100%)")]
        [DataRow(ColorRepresentationType.HEX, "000000")]
        [DataRow(ColorRepresentationType.NCol, "R0, 0%, 100%")]
        [DataRow(ColorRepresentationType.HSB, "hsb(0, 0%, 0%)")]
        [DataRow(ColorRepresentationType.HSI, "hsi(0, 0%, 0%)")]
        [DataRow(ColorRepresentationType.HSL, "hsl(0, 0%, 0%)")]
        [DataRow(ColorRepresentationType.HSV, "hsv(0, 0%, 0%)")]
        [DataRow(ColorRepresentationType.HWB, "hwb(0, 0%, 100%)")]
        [DataRow(ColorRepresentationType.RGB, "rgb(0, 0, 0)")]
        [DataRow(ColorRepresentationType.CIELAB, "CIELab(0, 0, 0)")]
        [DataRow(ColorRepresentationType.CIEXYZ, "xyz(0, 0, 0)")]
        [DataRow(ColorRepresentationType.VEC4, "(0f, 0f, 0f, 1f)")]
        [DataRow(ColorRepresentationType.DecimalValue, "0")]

        public void GetStringRepresentationTest(ColorRepresentationType type, string expected)
        {
            var result = ColorRepresentationHelper.GetStringRepresentation(Color.Black, type);
            Assert.AreEqual(result, expected);
        }
    }
}
