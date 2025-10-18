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
    private void NormalizeTestCore(string input, string expectedExe, string expectedArgs = "")
    {
        ShellListPageHelpers.NormalizeCommandLineAndArgs(input, out var exe, out var args);

        Assert.AreEqual(expectedExe, exe, ignoreCase: true, culture: System.Globalization.CultureInfo.InvariantCulture);
        Assert.AreEqual(expectedArgs, args);
    }

    [TestMethod]
    [DataRow("ping bing.com", "c:\\Windows\\system32\\ping.exe", "bing.com")]
    [DataRow("curl bing.com", "c:\\Windows\\system32\\curl.exe", "bing.com")]
    [DataRow("ipconfig /all", "c:\\Windows\\system32\\ipconfig.exe", "/all")]
    [DataRow("ipconfig a b \"c d\"", "c:\\Windows\\system32\\ipconfig.exe", "a b \"c d\"")]
    public void NormalizeCommandLineSimple(string input, string expectedExe, string expectedArgs = "")
    {
        NormalizeTestCore(input, expectedExe, expectedArgs);
    }

    [TestMethod]
    [DataRow("\"C:\\Program Files\\Windows Defender\\MsMpEng.exe\"", "C:\\Program Files\\Windows Defender\\MsMpEng.exe")]
    [DataRow("C:\\Program Files\\Windows Defender\\MsMpEng.exe", "C:\\Program Files\\Windows Defender\\MsMpEng.exe")]
    public void NormalizeCommandLineSpacesInExecutablePath(string input, string expectedExe, string expectedArgs = "")
    {
        NormalizeTestCore(input, expectedExe, expectedArgs);
    }

    [TestMethod]
    [DataRow("%SystemRoot%\\system32\\cmd.exe", "C:\\Windows\\System32\\cmd.exe")]
    public void NormalizeWithEnvVar(string input, string expectedExe, string expectedArgs = "")
    {
        NormalizeTestCore(input, expectedExe, expectedArgs);
    }

    [TestMethod]
    [DataRow("cmd --run --test", "C:\\Windows\\System32\\cmd.exe", "--run --test")]
    [DataRow("cmd    --run   --test  ", "C:\\Windows\\System32\\cmd.exe", "--run --test")]
    [DataRow("cmd \"--run --test\" --pass", "C:\\Windows\\System32\\cmd.exe", "\"--run --test\" --pass")]
    public void NormalizeArgsWithSpaces(string input, string expectedExe, string expectedArgs = "")
    {
        NormalizeTestCore(input, expectedExe, expectedArgs);
    }

    [TestMethod]
    [DataRow("ThereIsNoWayYouHaveAnExecutableNamedThisOnThePipeline", "ThereIsNoWayYouHaveAnExecutableNamedThisOnThePipeline", "")]
    [DataRow("C:\\ThisPathDoesNotExist\\NoExecutable.exe", "C:\\ThisPathDoesNotExist\\NoExecutable.exe", "")]
    public void NormalizeNonExistentExecutable(string input, string expectedExe, string expectedArgs = "")
    {
        NormalizeTestCore(input, expectedExe, expectedArgs);
    }

    [TestMethod]
    [DataRow("C:\\Windows", "c:\\Windows", "")]
    [DataRow("C:\\Windows foo /bar", "c:\\Windows", "foo /bar")]
    public void NormalizeDirectoryAsExecutable(string input, string expectedExe, string expectedArgs = "")
    {
        NormalizeTestCore(input, expectedExe, expectedArgs);
    }
}
