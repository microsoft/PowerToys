// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

using ColorPicker.Helpers;
using ManagedCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.Helpers
{
    [TestClass]
    public class ColorRepresentationHelperTest
    {
        [TestMethod]
        [DataRow("CMYK", "cmyk(0%, 0%, 0%, 100%)")]
        [DataRow("HEX", "000000")]
        [DataRow("NCol", "R0, 0%, 100%")]
        [DataRow("HSB", "hsb(0, 0%, 0%)")]
        [DataRow("HSI", "hsi(0, 0%, 0%)")]
        [DataRow("HSL", "hsl(0, 0%, 0%)")]
        [DataRow("HSV", "hsv(0, 0%, 0%)")]
        [DataRow("HWB", "hwb(0, 0%, 100%)")]
        [DataRow("RGB", "rgb(0, 0, 0)")]
        [DataRow("CIEXYZ", "XYZ(0, 0, 0)")]
        [DataRow("CIELAB", "CIELab(0, 0, 0)")]
        [DataRow("Oklab", "oklab(0, 0, 0)")]
        [DataRow("Oklch", "oklch(0, 0, 0)")]
        [DataRow("VEC4", "(0f, 0f, 0f, 1f)")]
        [DataRow("Decimal", "0")]
        [DataRow("HEX Int", "0xFF000000")]

        public void GetStringRepresentationTest(string type, string expected)
        {
            var result = ColorRepresentationHelper.GetStringRepresentation(Color.Black, ColorFormatHelper.GetDefaultFormat(type));
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow(0, 128, 255, "%ReF", "0")]
        [DataRow(0, 128, 255, "r=%ReF", "r=0")]
        [DataRow(0, 128, 255, "%ReF%Gr", "0128")]
        [DataRow(0, 128, 255, "%ReF,%GrF,%BlF", "0,.5,1")]
        [DataRow(1, 2, 255, "%ReF,%GrF,%BlF", "0,.01,1")]
        [DataRow(0, 0, 0, "%ReF%GrF%BlF", "000")]
        [DataRow(0, 128, 255, "%Ref,%Grf,%Blf", "0,0.5,1")]
        public void GetStringRepresentationFloatFormatWithZeroChannelTest(int red, int green, int blue, string format, string expected)
        {
            var result = ColorRepresentationHelper.GetStringRepresentation(Color.FromArgb(red, green, blue), format);
            Assert.AreEqual(expected, result);
        }
    }
}
