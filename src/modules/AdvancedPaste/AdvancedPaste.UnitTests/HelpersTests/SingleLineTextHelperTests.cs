// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using AdvancedPaste.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdvancedPaste.UnitTests.HelpersTests;

[TestClass]
public sealed class SingleLineTextHelperTests
{
    [TestMethod]
    [DataRow("First\r\nSecond", "First Second")]
    [DataRow("First\nSecond", "First Second")]
    [DataRow("First\rSecond", "First Second")]
    [DataRow("First\n\nSecond", "First Second")]
    [DataRow("First\n  \nSecond", "First Second")]
    [DataRow("  First \r\n\t Second  ", "First Second")]
    [DataRow("Already on one line", "Already on one line")]
    [DataRow("Keep\tinternal\ttabs", "Keep\tinternal\ttabs")]
    [DataRow("", "")]
    [DataRow(" \t ", "")]
    public void Convert_ReplacesLineBreaksWithSingleSpace(string input, string expected)
    {
        Assert.AreEqual(expected, SingleLineTextHelper.Convert(input));
    }

    [TestMethod]
    public void Convert_ReplacesUnicodeLineSeparators()
    {
        const string input = "One\u0085Two\u2028Three\u2029Four\u000BFive\u000CSix";
        const string expected = "One Two Three Four Five Six";

        Assert.AreEqual(expected, SingleLineTextHelper.Convert(input));
    }

    [TestMethod]
    public void Convert_PreservesUnicodeText()
    {
        const string input = "Caffè ☕\r\nمرحبا 🌍";
        const string expected = "Caffè ☕ مرحبا 🌍";

        Assert.AreEqual(expected, SingleLineTextHelper.Convert(input));
    }

    [TestMethod]
    public void Convert_NullInput_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => SingleLineTextHelper.Convert(null));
    }
}
