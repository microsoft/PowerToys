// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class IconPathParserTests
{
    [TestMethod]
    [DataRow(@"C:\Windows\notepad.exe", @"C:\Windows\notepad.exe", 0)]
    [DataRow(@"C:\Windows\System32\shell32.dll,-210", @"C:\Windows\System32\shell32.dll", -210)]
    [DataRow(@"C:\shortcut.lnk,0", @"C:\shortcut.lnk", 0)]
    [DataRow(@"C:\icons.dll,010", @"C:\icons.dll", 8)]
    [DataRow(@"C:\icons.dll,0x10", @"C:\icons.dll", 16)]
    public void ParsesSupportedBinaryIconReferences(string input, string expectedPath, int expectedIndex)
    {
        Assert.IsTrue(IconPathParser.TryParseBinaryIconReference(input, out var result));
        Assert.AreEqual(expectedPath, result.Path);
        Assert.AreEqual(expectedIndex, result.Index);
    }

    [TestMethod]
    [DataRow(@"C:\icon.png")]
    [DataRow(@"C:\APP.EXE,0")]
    [DataRow(@"C:\icons.dll,not-an-index")]
    [DataRow(@"C:\folder,with-comma\icons.dll,1")]
    public void RejectsInputsTheNativeConverterDidNotTreatAsBinaryIcons(string input)
    {
        Assert.IsFalse(IconPathParser.TryParseBinaryIconReference(input, out _));
    }
}
