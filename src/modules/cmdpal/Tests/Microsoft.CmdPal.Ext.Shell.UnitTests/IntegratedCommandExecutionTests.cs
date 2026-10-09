// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Ext.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Shell.UnitTests;

/// <summary>
/// Tests for the integrated workflow of ParseCommandline followed by
/// QualifyCommandLineDirectory, which mirrors the behavior in
/// ShellExecCmdLineWithSite.
///
/// Ported from the OsClient RunNative.UnitTests project so we can verify the
/// C# port has the same behavior as the C++ implementation.
/// </summary>
public static class IntegratedCommandExecutionTests
{
    [TestClass]
    public class ParseAndQualifyWorkflow
    {
        // For these tests, because the commandline isn't a full path to a file,
        // we'll expect to just use the cwd as the qualifiedDirectory
        // (which is the path we'll be creating this process in)
        [TestMethod]
        [DataRow("cmd.exe", "C:\\Users\\TestUser", "C:\\Windows\\System32\\cmd.exe", "", "C:\\Users\\TestUser")]
        [DataRow("cmd", "C:\\Users\\TestUser", "C:\\Windows\\System32\\cmd.exe", "", "C:\\Users\\TestUser")]
        [DataRow("setup.exe", "D:\\Install", "setup.exe", "", "D:\\Install")]
        public void SimpleExecutable_ParsesThenQualifiesCorrectly(
            string commandLine,
            string cwd,
            string expectedFilePath,
            string expectedArguments,
            string expectedWorkingDirectory)
        {
            // Act - Parse the command line first (like ShellExecCmdLineWithSite does)
            ParseCommandlineResult parseResult = RunHistory.ParseCommandline(commandLine, cwd);

            // Assert - Parsing should succeed
            Assert.AreEqual(0, parseResult.Result, $"Parse should succeed for '{commandLine}'");
            Assert.IsFalse(parseResult.IsUri);
            Assert.AreEqual(expectedFilePath, parseResult.FilePath, true, CultureInfo.InvariantCulture);
            Assert.AreEqual(expectedArguments, parseResult.Arguments, true, CultureInfo.InvariantCulture);

            // Act - Then qualify the working directory using the parsed file path
            var qualifiedDirectory = RunHistory.QualifyCommandLineDirectory(
                commandLine,
                parseResult.FilePath,
                cwd);

            // Assert - Working directory should be determined from the file path
            Assert.AreEqual(expectedWorkingDirectory, qualifiedDirectory, true, CultureInfo.InvariantCulture);
        }

        // Interestingly enough, the `cmd C:\Temp\file.txt` case here _will_
        // qualify the CWD to system32, because of the \ in the arguments
        [TestMethod]
        [DataRow("cmd /c dir", "C:\\Users\\TestUser", "C:\\Windows\\System32\\cmd.exe", "/c dir", "C:\\Users\\TestUser")]
        [DataRow("cmd C:\\Temp\\file.txt", "C:\\Users\\TestUser", "C:\\Windows\\System32\\cmd.exe", "C:\\Temp\\file.txt", "C:\\Windows\\System32")]
        [DataRow("ping google.com", "C:\\Users\\TestUser", "C:\\Windows\\System32\\ping.exe", "google.com", "C:\\Users\\TestUser")]
        public void ExecutableWithArguments_ParsesThenQualifiesCorrectly(
            string commandLine,
            string defaultDirectory,
            string expectedFilePath,
            string expectedArguments,
            string expectedWorkingDirectory)
        {
            // Act
            ParseCommandlineResult parseResult = RunHistory.ParseCommandline(commandLine, defaultDirectory);

            // Assert
            Assert.AreEqual(0, parseResult.Result, $"Parse should succeed for '{commandLine}'");
            Assert.IsFalse(parseResult.IsUri);
            Assert.AreEqual(expectedFilePath, parseResult.FilePath, true, CultureInfo.InvariantCulture);
            Assert.AreEqual(expectedArguments, parseResult.Arguments, true, CultureInfo.InvariantCulture);

            var qualifiedDirectory = RunHistory.QualifyCommandLineDirectory(
                commandLine,
                parseResult.FilePath,
                defaultDirectory);

            Assert.AreEqual(expectedWorkingDirectory, qualifiedDirectory, true, CultureInfo.InvariantCulture);
        }

        // C:\Program Files\WindowsPowerShell\Modules: A dir that does exist with spaces.
        // "C:\Program Files (x86)\App\app.exe" --config=test: A quoted path, so we trust the whole thing is real
        // D:\Games\game.exe -windowed: A normal absolute path with arguments. Doesn't exist, but it is a single word, so we can use as the file correctly.
        // C:\Program Files\MyApp\app.exe: is a file that _doesn't_ exist, so we fall back to c:\Program as the exe
        [TestMethod]
        [DataRow("C:\\Program Files\\WindowsPowerShell\\Modules", "C:\\Users\\TestUser", "C:\\Program Files\\WindowsPowerShell\\Modules", "", "C:\\Program Files\\WindowsPowerShell")]
        [DataRow("\"C:\\Program Files (x86)\\App\\app.exe\" --config=test", "C:\\Users\\TestUser", "C:\\Program Files (x86)\\App\\app.exe", "--config=test", "C:\\Program Files (x86)\\App")]
        [DataRow("C:\\Program Files\\MyApp\\app.exe", "C:\\Users\\TestUser", "C:\\Program", "Files\\MyApp\\app.exe", "C:\\")]
        [DataRow("D:\\Games\\game.exe -windowed", "C:\\Users\\TestUser", "D:\\Games\\game.exe", "-windowed", "D:\\Games")]
        public void AbsolutePaths_ParsesThenQualifiesCorrectly(
            string commandLine,
            string defaultDirectory,
            string expectedFilePath,
            string expectedArguments,
            string expectedWorkingDirectory)
        {
            ParseCommandlineResult parseResult = RunHistory.ParseCommandline(commandLine, defaultDirectory);

            Assert.AreEqual(0, parseResult.Result, $"Parse should succeed for '{commandLine}'");
            Assert.IsFalse(parseResult.IsUri);
            Assert.AreEqual(expectedFilePath, parseResult.FilePath, true, CultureInfo.InvariantCulture);
            Assert.AreEqual(expectedArguments, parseResult.Arguments, true, CultureInfo.InvariantCulture);

            var qualifiedDirectory = RunHistory.QualifyCommandLineDirectory(
                commandLine,
                parseResult.FilePath,
                defaultDirectory);

            Assert.AreEqual(expectedWorkingDirectory, qualifiedDirectory, true, CultureInfo.InvariantCulture);
        }

        [TestMethod]
        [DataRow("https://www.microsoft.com", "C:\\Users\\TestUser")]
        [DataRow("http://example.com", "C:\\Users\\TestUser")]
        [DataRow("ftp://ftp.example.com", "C:\\Users\\TestUser")]
        public void Urls_ParsesThenUsesDefaultDirectory(string commandLine, string defaultDirectory)
        {
            ParseCommandlineResult parseResult = RunHistory.ParseCommandline(commandLine, defaultDirectory);

            Assert.AreEqual(0, parseResult.Result, $"Parse should succeed for '{commandLine}'");
            Assert.IsTrue(parseResult.IsUri);
            Assert.AreEqual(commandLine, parseResult.FilePath, true, CultureInfo.InvariantCulture);
            Assert.AreEqual(string.Empty, parseResult.Arguments, true, CultureInfo.InvariantCulture);

            var qualifiedDirectory = RunHistory.QualifyCommandLineDirectory(
                commandLine,
                parseResult.FilePath,
                defaultDirectory);

            Assert.AreEqual(defaultDirectory, qualifiedDirectory, true, CultureInfo.InvariantCulture);
        }

        [TestMethod]
        public void RelativePath_ParsesThenQualifiesBasedOnCommand()
        {
            var commandLine = "..\\tools\\myTool.exe";
            var defaultDirectory = "C:\\Users\\TestUser";

            ParseCommandlineResult parseResult = RunHistory.ParseCommandline(commandLine, defaultDirectory);

            Assert.AreEqual(0, parseResult.Result, "Parse should succeed for relative path");
            Assert.IsFalse(parseResult.IsUri);
            Assert.IsNotNull(parseResult.FilePath);
            Assert.IsTrue(parseResult.FilePath.Length > 0);

            var qualifiedDirectory = RunHistory.QualifyCommandLineDirectory(
                commandLine,
                parseResult.FilePath,
                defaultDirectory);

            Assert.IsNotNull(qualifiedDirectory);
            Assert.IsTrue(qualifiedDirectory.Length > 0);
        }

        [TestMethod]
        public void NetworkPath_ParsesThenQualifiesUncPath()
        {
            var commandLine = "\\\\localhost\\c$\\Windows\\System32\\cmd.exe";
            var defaultDirectory = "C:\\Users\\TestUser";

            ParseCommandlineResult parseResult = RunHistory.ParseCommandline(commandLine, defaultDirectory);

            Assert.AreEqual(0, parseResult.Result, "Parse should succeed for UNC path");
            Assert.IsFalse(parseResult.IsUri);
            Assert.AreEqual("\\\\localhost\\c$\\Windows\\System32\\cmd.exe", parseResult.FilePath);

            var qualifiedDirectory = RunHistory.QualifyCommandLineDirectory(
                commandLine,
                parseResult.FilePath,
                defaultDirectory);

            Assert.IsNotNull(qualifiedDirectory);
            Assert.IsTrue(qualifiedDirectory.Length > 0);
        }
    }

    /// <summary>
    /// Tests edge cases and error conditions in the integrated workflow.
    /// </summary>
    [TestClass]
    public class ParseAndQualifyEdgeCases
    {
        [TestMethod]
        [DataRow("", "")]
        [DataRow("   ", "")]
        public void EmptyOrWhitespaceInputs_ShouldHandleGracefully(string commandLine, string defaultDirectory)
        {
            ParseCommandlineResult parseResult = RunHistory.ParseCommandline(commandLine, defaultDirectory);

            var qualifiedDirectory = RunHistory.QualifyCommandLineDirectory(
                commandLine,
                parseResult.FilePath ?? string.Empty,
                defaultDirectory);

            Assert.IsNotNull(qualifiedDirectory);
        }

        [TestMethod]
        public void ParseFailure_ShouldStillAllowDirectoryQualification()
        {
            var commandLine = "some_nonexistent_command_that_might_fail";
            var defaultDirectory = "C:\\Users\\TestUser";

            ParseCommandlineResult parseResult = RunHistory.ParseCommandline(commandLine, defaultDirectory);

            var qualifiedDirectory = RunHistory.QualifyCommandLineDirectory(
                commandLine,
                parseResult.FilePath ?? commandLine,
                defaultDirectory);

            Assert.IsNotNull(qualifiedDirectory);
            Assert.IsTrue(qualifiedDirectory.Length > 0);
        }

        [TestMethod]
        [DataRow("notepad", "")]
        [DataRow("cmd", null)]
        public void NullOrEmptyDefaultDirectory_ShouldHandleGracefully(string commandLine, string defaultDirectory)
        {
            ParseCommandlineResult parseResult = RunHistory.ParseCommandline(commandLine, defaultDirectory ?? string.Empty);

            var qualifiedDirectory = RunHistory.QualifyCommandLineDirectory(
                commandLine,
                parseResult.FilePath ?? commandLine,
                defaultDirectory ?? string.Empty);

            Assert.IsNotNull(qualifiedDirectory);
        }
    }

    /// <summary>
    /// Tests that verify the behavior matches what ShellExecCmdLineWithSite expects.
    /// </summary>
    [TestClass]
    public class ShellExecCompatibility
    {
        [TestMethod]
        [DataRow("setup.exe", "D:\\Install\\MyApp")]
        [DataRow("install.msi", "C:\\Temp\\Installer")]
        public void SetupPrograms_WorkingDirectoryFromExecutableLocation(string commandLine, string defaultDirectory)
        {
            // This test verifies the comment in ShellExecCmdLineWithSite:
            // "this needs to be here. app installs rely on the current directory
            // to be the directory with the setup.exe"
            ParseCommandlineResult parseResult = RunHistory.ParseCommandline(commandLine, defaultDirectory);

            if (parseResult.Result == 0)
            {
                var qualifiedDirectory = RunHistory.QualifyCommandLineDirectory(
                    commandLine,
                    parseResult.FilePath,
                    defaultDirectory);

                if (!parseResult.IsUri && !string.IsNullOrEmpty(parseResult.FilePath))
                {
                    var expectedDir = System.IO.Path.GetDirectoryName(parseResult.FilePath);
                    if (!string.IsNullOrEmpty(expectedDir))
                    {
                        Assert.AreEqual(expectedDir, qualifiedDirectory, true, CultureInfo.InvariantCulture);
                    }
                }
            }
        }

        [TestMethod]
        [DataRow("notepad", "C:\\Users\\TestUser", "C:\\Users\\TestUser")]
        [DataRow("C:\\Windows\\notepad.exe", "C:\\Users\\TestUser", "C:\\Windows")]
        [DataRow("..\\tools\\app.exe", "C:\\Users\\TestUser", "C:\\Users\\tools")]
        public void PathQualificationDecision_BasedOnCommandLineContent(
            string commandLine,
            string defaultDirectory,
            string expectedDirectory)
        {
            // This test verifies the logic described in QualifyCommandLineDirectory:
            // "only qualify if the original command line contains a backslash or a colon"
            ParseCommandlineResult parseResult = RunHistory.ParseCommandline(commandLine, defaultDirectory);
            var qualifiedDirectory = RunHistory.QualifyCommandLineDirectory(
                commandLine,
                parseResult.FilePath ?? commandLine,
                defaultDirectory);

            Assert.AreEqual(expectedDirectory, qualifiedDirectory, true, CultureInfo.InvariantCulture);
        }

        [TestMethod]
        public void CompleteWorkflow_MimicsShellExecCmdLineWithSite()
        {
            // Arrange - Simulate the exact workflow from ShellExecCmdLineWithSite
            var originalCommand = "notepad C:\\Temp\\test.txt";
            var defaultWorkingDir = "C:\\Users\\TestUser";

            // Step 1: Expand environment strings (simplified - we'll assume no env vars)
            var expandedCommand = originalCommand;

            // Step 2: Parse the command line to get file and args
            ParseCommandlineResult parseResult = RunHistory.ParseCommandline(expandedCommand, defaultWorkingDir);

            // Step 3: If parsing succeeded, qualify the working directory
            var finalWorkingDir = defaultWorkingDir;
            if (parseResult.Result == 0)
            {
                var qualifiedDir = RunHistory.QualifyCommandLineDirectory(
                    expandedCommand,
                    parseResult.FilePath,
                    defaultWorkingDir);

                finalWorkingDir = qualifiedDir;
            }

            // Assert
            Assert.AreEqual(0, parseResult.Result);
            Assert.IsFalse(parseResult.IsUri);
            StringAssert.Contains(parseResult.FilePath, "notepad", StringComparison.OrdinalIgnoreCase);
            StringAssert.Contains(parseResult.Arguments, "C:\\Temp\\test.txt");

            var expectedWorkingDir = System.IO.Path.GetDirectoryName(parseResult.FilePath);
            Assert.AreEqual(expectedWorkingDir, finalWorkingDir, true, CultureInfo.InvariantCulture);
        }
    }
}
