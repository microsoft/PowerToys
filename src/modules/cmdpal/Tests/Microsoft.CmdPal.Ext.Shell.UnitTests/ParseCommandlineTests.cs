// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Ext.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Shell.UnitTests;

/// <summary>
/// Tests for the C# port of the native RunHistory.ParseCommandline.
/// Ported from the OsClient RunNative.UnitTests project so we can verify the
/// C# port has the same behavior as the C++ implementation.
/// </summary>
[TestClass]
public class ParseCommandlineTests
{
    [TestMethod]
    [DataRow("cmd", "", true, "C:\\Windows\\System32\\cmd.exe", "")]
    [DataRow("cmd.exe", "", true, "C:\\Windows\\System32\\cmd.exe", "")]
    [DataRow("cmd.exe /c dir", "", true, "C:\\Windows\\System32\\cmd.exe", "/c dir")]
    public void SimpleCommands(
        string commandLine,
        string workingDirectory,
        bool expectedSuccess,
        string expectedFullFilePath,
        string expectedArguments)
    {
        // Act
        ParseCommandlineResult result = RunHistory.ParseCommandline(commandLine, workingDirectory);

        // Assert
        if (expectedSuccess)
        {
            Assert.AreEqual(0, result.Result);
        }
        else
        {
            Assert.AreNotEqual(0, result.Result);
        }

        Assert.IsFalse(result.IsUri);

        if (expectedSuccess)
        {
            StringAssert.Contains(result.FilePath, expectedFullFilePath, System.StringComparison.InvariantCultureIgnoreCase);
            Assert.AreEqual(expectedArguments, result.Arguments, true, CultureInfo.InvariantCulture);
        }
    }

    [TestMethod]
    [DataRow("https://www.microsoft.com")]
    [DataRow("http://example.com")]
    [DataRow("ftp://ftp.example.com")]
    [DataRow("mailto:test@example.com")]
    public void SimpleUrls(string url)
    {
        // Act
        ParseCommandlineResult result = RunHistory.ParseCommandline(url, string.Empty);

        // Assert
        Assert.AreEqual(0, result.Result);
        Assert.IsTrue(result.IsUri);
        Assert.AreEqual(url, result.FilePath);
        Assert.AreEqual(string.Empty, result.Arguments);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("\t")]
    public void WithEmptyOrWhitespace(string commandLine)
    {
        // Act
        ParseCommandlineResult result = RunHistory.ParseCommandline(commandLine, string.Empty);

        // Assert
        // Should not crash, but may fail with appropriate error code
        Assert.AreEqual(0, result.Result);

        // ParseCommandline doesn't trim whitespace
        Assert.AreEqual(commandLine, result.FilePath);
        Assert.AreEqual(string.Empty, result.Arguments);
        Assert.IsFalse(result.IsUri);
    }

    [TestMethod]
    public void WithQuotedPathContainingSpaces()
    {
        // Arrange
        var commandLine = "\"C:\\Program Files\\Windows NT\\Accessories\\wordpad.exe\" \"C:\\Users\\Test\\Documents\\file with spaces.rtf\"";

        // Act
        ParseCommandlineResult result = RunHistory.ParseCommandline(commandLine, string.Empty);

        // Assert
        Assert.AreEqual(0, result.Result);
        Assert.IsFalse(result.IsUri);
        StringAssert.Contains(result.FilePath, "wordpad.exe");
        StringAssert.Contains(result.Arguments, "file with spaces.rtf");
    }

    [TestMethod]
    [DataRow(@"\\?\C:", @"\\?\C:")]
    [DataRow(@"\\?\", @"\\?\")]
    [DataRow(@"\\?\C:\Windows", @"\\?\C:\Windows")]
    public void NtPaths(string commandLine, string expectedFullFilePath)
    {
        // Act
        ParseCommandlineResult result = RunHistory.ParseCommandline(commandLine, string.Empty);

        // Assert
        Assert.AreEqual(0, result.Result);
        Assert.AreEqual(expectedFullFilePath, result.FilePath);
        Assert.AreEqual(string.Empty, result.Arguments);
        Assert.IsFalse(result.IsUri);
    }
}
