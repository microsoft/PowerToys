// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Shell.Helpers;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Shell.UnitTests;

[TestClass]
public class NormalizeCommandLineTests : CommandPaletteUnitTestBase
{
    [TestMethod]
    [DataRow("ping bing.com", "c:\\Windows\\system32\\ping.exe", "bing.com")]
    [DataRow("curl bing.com", "c:\\Windows\\system32\\curl.exe", "bing.com")]
    [DataRow("ipconfig /all", "c:\\Windows\\system32\\ipconfig.exe", "/all")]
    public void NormalizeCommandLineSimple(string input, string expectedExe, string expectedArgs = "")
    {
        ShellListPageHelpers.NormalizeCommandLineAndArgs(input, out var exe, out var args);

        Assert.AreEqual(expectedExe, exe, ignoreCase: true, culture: System.Globalization.CultureInfo.InvariantCulture);
        Assert.AreEqual(expectedArgs, args, ignoreCase: true, culture: System.Globalization.CultureInfo.InvariantCulture);
    }

    [TestMethod]
    [DataRow("\"C:\\Program Files\\Windows Defender\\MsMpEng.exe\"", "C:\\Program Files\\Windows Defender\\MsMpEng.exe")]
    [DataRow("C:\\Program Files\\Windows Defender\\MsMpEng.exe", "C:\\Program Files\\Windows Defender\\MsMpEng.exe")]
    public void NormalizeCommandLineSpacesInExecutablePath(string input, string expectedExe, string expectedArgs = "")
    {
        ShellListPageHelpers.NormalizeCommandLineAndArgs(input, out var exe, out var args);

        Assert.AreEqual(expectedExe, exe);
        Assert.AreEqual(expectedArgs, args);
    }
}
