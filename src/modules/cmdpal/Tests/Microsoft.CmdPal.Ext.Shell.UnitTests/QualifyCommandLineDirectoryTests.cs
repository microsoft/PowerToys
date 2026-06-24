// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.CmdPal.Ext.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Shell.UnitTests;

/// <summary>
/// Tests for the C# port of the native RunHistory.QualifyCommandLineDirectory.
/// Ported from the OsClient RunNative.UnitTests project so we can verify the
/// C# port has the same behavior as the C++ implementation.
/// </summary>
[TestClass]
public class QualifyCommandLineDirectoryTests
{
    /// <summary>
    /// All of these cases should result in the default directory being used.
    /// The passed in commandLine doesn't specify a path. We just return the
    /// default directory.
    /// </summary>
    [TestMethod]
    [DataRow("notepad.exe", "C:\\Users\\TestUser\\Documents\\test.txt")]
    [DataRow("setup.exe", "D:\\Install\\setup.exe")]
    [DataRow("cmd", "C:\\Windows\\System32\\cmd.exe")]
    public void RawFilename(string commandLine, string filePath)
    {
        // Arrange
        var defaultDirectory = "C:\\default\\dir";

        // Act
        var result = RunHistory.QualifyCommandLineDirectory(commandLine, filePath, defaultDirectory);

        // Assert
        Assert.AreEqual(defaultDirectory, result, true, CultureInfo.InvariantCulture);
    }

    [TestMethod]
    [DataRow("cmd", "C:\\Windows\\System32\\cmd.exe", "C:\\Users\\Default", "C:\\Users\\Default")]
    [DataRow("..\\cmd", "C:\\Windows\\System32\\cmd.exe", "C:\\Users\\Default", "C:\\Windows\\System32")]
    [DataRow("..\\..\\cmd", "C:\\cmd.exe", "C:\\Users\\Default", "C:\\")]
    [DataRow("..\\..\\cmd", "C:\\Windows\\System32\\cmd.exe", "C:\\Users\\Default", "C:\\Windows\\System32")]
    [DataRow("..\\abc", "C:\\abc.exe", "C:\\Users\\", "C:\\")]
    [DataRow("\\abc", "C:\\foo\\abc.exe", "C:\\Users\\", "C:\\foo")]
    [DataRow("I\\Do\\Not\\Matter\\abc", "C:\\foo\\abc.exe", "C:\\Users\\", "C:\\foo")]
    [DataRow("notepad C:\\Temp\\file.txt", "C:\\Windows\\System32\\notepad.exe", "C:\\Users\\", "C:\\Windows\\System32")]
    public void JustUseTheDirectoryOfTheFullPath(
        string commandLine,
        string fullPath,
        string defaultDirectory,
        string expectedDirectory)
    {
        // Act
        var result = RunHistory.QualifyCommandLineDirectory(commandLine, fullPath, defaultDirectory);

        // Assert
        Assert.AreEqual(expectedDirectory, result, true, CultureInfo.InvariantCulture);
    }

    [TestMethod]
    [DataRow("cmd", "C:\\Windows\\System32\\cmd.exe", "\\", "\\")]
    [DataRow("cmd", "C:\\Windows\\System32\\cmd.exe", "C:\\Users\\Default", "C:\\Users\\Default")]
    [DataRow("cmd", "C:\\Windows\\System32\\cmd.exe", "C:/Users/Default", "C:/Users/Default")]
    [DataRow("cmd", "C:\\Windows\\System32\\cmd.exe", "\\Default", "\\Default")]
    [DataRow("cmd", "..\\cmd.exe", "c:\\users\\Default", "C:\\Users\\Default")]
    public void WithRelativePath(
        string commandLine,
        string filePath,
        string defaultDirectory,
        string expectedDirectory)
    {
        // Act
        var result = RunHistory.QualifyCommandLineDirectory(commandLine, filePath, defaultDirectory);

        // Assert
        Assert.IsTrue(result.Length > 0);
        Assert.AreEqual(expectedDirectory, result, true, CultureInfo.InvariantCulture);
    }

    [TestMethod]
    public void WithEmptyInputs_ShouldHandleGracefully()
    {
        // Act
        var result = RunHistory.QualifyCommandLineDirectory(string.Empty, string.Empty, string.Empty);

        // Assert
        Assert.IsNotNull(result);
    }

    [TestMethod]
    [DataRow("https://www.microsoft.com", "https://www.microsoft.com", "C:\\Users\\Test")]
    [DataRow("http://example.com", "http://example.com", "")]
    public void WithUrls_ShouldHandleAppropriately(
        string commandLine,
        string filePath,
        string defaultDirectory)
    {
        // Act
        var result = RunHistory.QualifyCommandLineDirectory(commandLine, filePath, defaultDirectory);

        // Assert
        Assert.IsNotNull(result);

        // URLs typically don't have working directories in the traditional sense
    }

    [TestMethod]
    [DataRow("C:\\Program Files\\App\\app.exe", "C:\\Program Files\\App\\app.exe", "")]
    [DataRow("D:\\Games\\game.exe", "D:\\Games\\game.exe", "C:\\Users\\Test")]
    public void WithAbsolutePaths_ShouldExtractDirectory(
        string commandLine,
        string filePath,
        string defaultDirectory)
    {
        // Act
        var result = RunHistory.QualifyCommandLineDirectory(commandLine, filePath, defaultDirectory);

        // Assert
        Assert.IsTrue(result.Length > 0);
        var expectedDir = System.IO.Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(expectedDir))
        {
            Assert.AreEqual(expectedDir, result, true, CultureInfo.InvariantCulture);
        }
    }

    [TestMethod]
    public void WithNetworkPath_ShouldHandleUncPaths()
    {
        // Arrange
        var commandLine = "\\\\server\\share\\app.exe";
        var filePath = "\\\\server\\share\\app.exe";
        var defaultDirectory = "C:\\Users\\Test";

        // Act
        var result = RunHistory.QualifyCommandLineDirectory(commandLine, filePath, defaultDirectory);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Length > 0);
    }
}
