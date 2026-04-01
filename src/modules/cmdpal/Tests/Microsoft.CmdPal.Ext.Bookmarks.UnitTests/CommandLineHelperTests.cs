// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using System;
using System.IO;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public class CommandLineHelperTests
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private static string _tempTestDir;

    private static string _tempTestFile;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    [ClassInitialize]
    public static void ClassSetup(TestContext context)
    {
        // Create temporary test directory and file
        _tempTestDir = Path.Combine(Path.GetTempPath(), "CommandLineHelperTests_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempTestDir);

        _tempTestFile = Path.Combine(_tempTestDir, "testfile.txt");
        File.WriteAllText(_tempTestFile, "test");
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        // Clean up test directory
        if (Directory.Exists(_tempTestDir))
        {
            Directory.Delete(_tempTestDir, true);
        }
    }

    [TestMethod]
    [DataRow("%TEMP%", false, true, DisplayName = "Expands TEMP environment variable")]
    [DataRow("%USERPROFILE%", false, true, DisplayName = "Expands USERPROFILE environment variable")]
    [DataRow("%SystemRoot%", false, true, DisplayName = "Expands SystemRoot environment variable")]
    public void Expand_WithEnvironmentVariables_ExpandsCorrectly(string input, bool expandShell, bool shouldExist)
    {
        // Act
        var result = CommandLineHelper.ExpandPathToPhysicalFile(input, expandShell, out var full);

        // Assert
        Assert.AreEqual(shouldExist, result, $"Expected result {shouldExist} for input '{input}'");
        if (shouldExist)
        {
            Assert.IsFalse(full.Contains('%'), "Output should not contain % symbols after expansion");
            Assert.IsTrue(Path.Exists(full), $"Expanded path '{full}' should exist");
        }
    }

    [TestMethod]
    [DataRow("shell:Downloads", true, DisplayName = "Expands shell:Downloads when expandShell is true")]
    [DataRow("shell:Desktop", true, DisplayName = "Expands shell:Desktop when expandShell is true")]
    [DataRow("shell:Documents", true, DisplayName = "Expands shell:Documents when expandShell is true")]
    public void Expand_WithShellPaths_ExpandsWhenFlagIsTrue(string input, bool expandShell)
    {
        // Act
        var result = CommandLineHelper.ExpandPathToPhysicalFile(input, expandShell, out var full);

        // Assert
        if (result)
        {
            Assert.IsFalse(full.StartsWith("shell:", StringComparison.OrdinalIgnoreCase), "Shell prefix should be resolved");
            Assert.IsTrue(Path.Exists(full), $"Expanded shell path '{full}' should exist");
        }

        // Note: Result may be false if ShellNames.TryGetFileSystemPath fails
    }

    [TestMethod]
    [DataRow("shell:Personal", false, DisplayName = "Does not expand shell: when expandShell is false")]
    public void Expand_WithShellPaths_DoesNotExpandWhenFlagIsFalse(string input, bool expandShell)
    {
        // Act
        var result = CommandLineHelper.ExpandPathToPhysicalFile(input, expandShell, out var full);

        // Assert - shell: paths won't exist as literal paths
        Assert.IsFalse(result, "Should return false for unexpanded shell path");
        Assert.AreEqual(input, full, "Output should match input when not expanding shell paths");
    }

    [TestMethod]
    [DataRow("shell:Personal\\subfolder", true, "\\subfolder", DisplayName = "Expands shell path with subfolder")]
    [DataRow("shell:Desktop\\test.txt", true, "\\test.txt", DisplayName = "Expands shell path with file")]
    public void Expand_WithShellPathsAndSubpaths_CombinesCorrectly(string input, bool expandShell, string expectedEnding)
    {
        // Act
        var result = CommandLineHelper.ExpandPathToPhysicalFile(input, expandShell, out var full);

        // Note: Result depends on whether the combined path exists
        if (result)
        {
            Assert.IsFalse(full.StartsWith("shell:", StringComparison.OrdinalIgnoreCase), "Shell prefix should be resolved");
            Assert.IsTrue(full.EndsWith(expectedEnding, StringComparison.OrdinalIgnoreCase), "Output should end with the subpath");
        }
    }

    [TestMethod]
    public void Expand_WithExistingDirectory_ReturnsFullPath()
    {
        // Arrange
        var input = _tempTestDir;

        // Act
        var result = CommandLineHelper.ExpandPathToPhysicalFile(input, false, out var full);

        // Assert
        Assert.IsTrue(result, "Should return true for existing directory");
        Assert.AreEqual(Path.GetFullPath(_tempTestDir), full, "Should return full path");
    }

    [TestMethod]
    public void Expand_WithExistingFile_ReturnsFullPath()
    {
        // Arrange
        var input = _tempTestFile;

        // Act
        var result = CommandLineHelper.ExpandPathToPhysicalFile(input, false, out var full);

        // Assert
        Assert.IsTrue(result, "Should return true for existing file");
        Assert.AreEqual(Path.GetFullPath(_tempTestFile), full, "Should return full path");
    }

    [TestMethod]
    [DataRow("C:\\NonExistent\\Path\\That\\Does\\Not\\Exist", false, "C:\\NonExistent\\Path\\That\\Does\\Not\\Exist", DisplayName = "Nonexistent absolute path")]
    [DataRow("NonExistentFile.txt", false, "NonExistentFile.txt", DisplayName = "Nonexistent relative path")]
    public void Expand_WithNonExistentPath_ReturnsFalse(string input, bool expandShell, string expectedFull)
    {
        // Act
        var result = CommandLineHelper.ExpandPathToPhysicalFile(input, expandShell, out var full);

        // Assert
        Assert.IsFalse(result, "Should return false for nonexistent path");
        Assert.AreEqual(expectedFull, full, "Output should be empty string");
    }

    [TestMethod]
    [DataRow("", false, DisplayName = "Empty string")]
    [DataRow("   ", false, DisplayName = "Whitespace only")]
    public void Expand_WithEmptyOrWhitespace_ReturnsFalse(string input, bool expandShell)
    {
        // Act
        var result = CommandLineHelper.ExpandPathToPhysicalFile(input, expandShell, out var full);

        // Assert
        Assert.IsFalse(result, "Should return false for empty/whitespace input");
    }

    [TestMethod]
    [DataRow("%TEMP%\\testsubdir", false, DisplayName = "Env var with subdirectory")]
    [DataRow("%USERPROFILE%\\Desktop", false, DisplayName = "USERPROFILE with Desktop")]
    public void Expand_WithEnvironmentVariableAndSubpath_ExpandsCorrectly(string input, bool expandShell)
    {
        // Act
        var result = CommandLineHelper.ExpandPathToPhysicalFile(input, expandShell, out var full);

        // Result depends on whether the path exists
        if (result)
        {
            Assert.IsFalse(full.Contains('%'), "Should expand environment variables");
            Assert.IsTrue(Path.Exists(full), "Expanded path should exist");
        }
    }

    [TestMethod]
    public void Expand_WithRelativePath_ConvertsToAbsoluteWhenExists()
    {
        // Arrange
        var relativePath = Path.GetRelativePath(Environment.CurrentDirectory, _tempTestDir);

        // Act
        var result = CommandLineHelper.ExpandPathToPhysicalFile(relativePath, false, out var full);

        // Assert
        if (result)
        {
            Assert.IsTrue(Path.IsPathRooted(full), "Output should be absolute path");
            Assert.IsTrue(Path.Exists(full), "Expanded path should exist");
        }
    }

    [TestMethod]
    [DataRow("InvalidShell:Path", true, DisplayName = "Invalid shell path format")]
    public void Expand_WithInvalidShellPath_ReturnsFalse(string input, bool expandShell)
    {
        // Act
        var result = CommandLineHelper.ExpandPathToPhysicalFile(input, expandShell, out var full);

        // Assert
        // If ShellNames.TryGetFileSystemPath returns false, method returns false
        Assert.IsFalse(result || Path.Exists(full), "Should return false or path should not exist");
    }

    [DataTestMethod]

    // basic
    [DataRow("cmd ping", "cmd", "ping")]
    [DataRow("cmd ping pong", "cmd", "ping pong")]
    [DataRow("cmd \"ping pong\"", "cmd", "\"ping pong\"")]

    // no tail / trailing whitespace after head
    [DataRow("cmd", "cmd", "")]
    [DataRow("cmd   ", "cmd", "")]

    // spacing & tabs between args should be preserved in tail
    [DataRow("cmd    ping    pong", "cmd", "ping    pong")]
    [DataRow("cmd\tping\tpong", "cmd", "ping\tpong")]

    // leading whitespace before head
    [DataRow("   cmd ping", "", "cmd ping")]
    [DataRow("\t  cmd   ping", "", "cmd   ping")]

    // quoted tail variants
    [DataRow("cmd \"\"", "cmd", "\"\"")]
    [DataRow("cmd \"a \\\"quoted\\\" arg\" b", "cmd", "\"a \\\"quoted\\\" arg\" b")]

    // quoted head (spaces in path)
    [DataRow(@"""C:\Program Files\nodejs\node.exe"" -v", @"C:\Program Files\nodejs\node.exe", "-v")]
    [DataRow(@"""C:\Program Files\Git\bin\bash.exe""", @"C:\Program Files\Git\bin\bash.exe", "")]
    [DataRow(@"  ""C:\Program Files\Git\bin\bash.exe""   -lc ""hi""", @"", @"""C:\Program Files\Git\bin\bash.exe""   -lc ""hi""")]
    [DataRow(@"""C:\Program Files (x86)\MSBuild\Current\Bin\MSBuild.exe"" Test.sln", @"C:\Program Files (x86)\MSBuild\Current\Bin\MSBuild.exe", "Test.sln")]

    // quoted simple head (still should strip quotes for head)
    [DataRow(@"""cmd"" ping", "cmd", "ping")]

    // common CLI shapes
    [DataRow("git --version", "git", "--version")]
    [DataRow("dotnet build -c Release", "dotnet", "build -c Release")]

    // UNC paths
    [DataRow("\"\\\\server\\share\\\" with args", "\\\\server\\share\\", "with args")]
    public void SplitHeadAndArgs(string input, string expectedHead, string expectedTail)
    {
        // Act
        var result = CommandLineHelper.SplitHeadAndArgs(input);

        // Assert
        // If ShellNames.TryGetFileSystemPath returns false, method returns false
        Assert.AreEqual(expectedHead, result.Head);
        Assert.AreEqual(expectedTail, result.Tail);
    }

    [DataTestMethod]
    [DataRow(@"C:\program files\myapp\app.exe -param ""1"" -param 2", @"C:\program files\myapp\app.exe -param", @"""1"" -param 2")]
    [DataRow(@"git commit -m test", "git commit -m test", "")]
    [DataRow(@"""C:\Program Files\App\app.exe"" -v", "", @"""C:\Program Files\App\app.exe"" -v")]
    [DataRow(@"tool a\\\""b c ""d e"" f", @"tool a\\\""b c", @"""d e"" f")] // escaped quote before first real one
    [DataRow("C:\\Some\\\"Path\\file.txt", "C:\\Some\\\"Path\\file.txt", "")]
    [DataRow(@"   ""C:\p\app.exe"" -v",                 "", @"""C:\p\app.exe"" -v")] // first token is quoted
    public void SplitLongestHeadBeforeQuotedArg_Tests(string input, string expectedHead, string expectedTail)
    {
        var (head, tail) = CommandLineHelper.SplitLongestHeadBeforeQuotedArg(input);
        Assert.AreEqual(expectedHead, head);
        Assert.AreEqual(expectedTail, tail);
    }
}
