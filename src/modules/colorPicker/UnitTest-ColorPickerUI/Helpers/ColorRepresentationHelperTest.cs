// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using ColorPicker.Helpers;
using ManagedCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ColorPicker.UnitTests
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
        [DataRow("CIELAB", "CIELab(0, 0, 0)")]
        [DataRow("CIEXYZ", "XYZ(0, 0, 0)")]
        [DataRow("VEC4", "(0f, 0f, 0f, 1f)")]
        [DataRow("Decimal", "0")]
        [DataRow("HEX Int", "0xFF000000")]

        public void GetStringRepresentationTest(string type, string expected)
        {
            var result = ColorRepresentationHelper.GetStringRepresentation(Color.Black, type, ColorFormatHelper.GetDefaultFormat(type));
            Assert.AreEqual(result, expected);
        }
    }
}
