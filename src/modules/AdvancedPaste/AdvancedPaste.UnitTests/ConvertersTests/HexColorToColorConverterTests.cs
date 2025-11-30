// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdvancedPaste.Converters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.UI;

namespace AdvancedPaste.UnitTests.ConvertersTests;

[TestClass]
public sealed class HexColorToColorConverterTests
{
    [TestInitialize]
    public void Setup()
    {
    }

    [TestMethod]
    public void TestConvert_ValidSixDigitHex_ReturnsColor()
    {
        Color? result = HexColorConverterHelper.ConvertHexColorToRgb("#FFBFAB");
        Assert.IsNotNull(result);

        var color = (Windows.UI.Color)result;
        Assert.AreEqual(255, color.R);
        Assert.AreEqual(191, color.G);
        Assert.AreEqual(171, color.B);
        Assert.AreEqual(255, color.A);
    }

    [TestMethod]
    public void TestConvert_ValidThreeDigitHex_ReturnsColor()
    {
        Color? result = HexColorConverterHelper.ConvertHexColorToRgb("#abc");
        Assert.IsNotNull(result);

        var color = (Windows.UI.Color)result;

        // #abc should expand to #aabbcc
        Assert.AreEqual(170, color.R); // 0xaa
        Assert.AreEqual(187, color.G); // 0xbb
        Assert.AreEqual(204, color.B); // 0xcc
        Assert.AreEqual(255, color.A);
    }

    [TestMethod]
    public void TestConvert_NullOrEmpty_ReturnsNull()
    {
        Assert.IsNull(HexColorConverterHelper.ConvertHexColorToRgb(null));
        Assert.IsNull(HexColorConverterHelper.ConvertHexColorToRgb(string.Empty));
        Assert.IsNull(HexColorConverterHelper.ConvertHexColorToRgb("   "));
    }

    [TestMethod]
    public void TestConvert_InvalidHex_ReturnsNull()
    {
        Assert.IsNull(HexColorConverterHelper.ConvertHexColorToRgb("#GGGGGG"));
        Assert.IsNull(HexColorConverterHelper.ConvertHexColorToRgb("#12345"));
    }
}
