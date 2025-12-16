// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdvancedPaste.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdvancedPaste.UnitTests.HelpersTests;

[TestClass]
public sealed class ClipboardItemHelperTests
{
    [TestMethod]
    [DataRow("#FFBFAB", true)]
    [DataRow("#000000", true)]
    [DataRow("#FFFFFF", true)]
    [DataRow("#fff", true)]
    [DataRow("#abc", true)]
    [DataRow("#123456", true)]
    [DataRow("#AbCdEf", true)]
    [DataRow("FFBFAB", false)] // Missing #
    [DataRow("#GGGGGG", false)] // Invalid hex characters
    [DataRow("#12345", false)] // Wrong length
    [DataRow("#1234567", false)] // Too long
    [DataRow("", false)]
    [DataRow(null, false)]
    [DataRow("  #FFF  ", true)] // Whitespace should be trimmed
    [DataRow("Not a color", false)]
    [DataRow("#", false)]
    [DataRow("##FFFFFF", false)]
    public void TestIsRgbHexColor(string input, bool expected)
    {
        bool result = ClipboardItemHelper.IsRgbHexColor(input);
        Assert.AreEqual(expected, result, $"IsRgbHexColor(\"{input}\") should return {expected}");
    }
}
