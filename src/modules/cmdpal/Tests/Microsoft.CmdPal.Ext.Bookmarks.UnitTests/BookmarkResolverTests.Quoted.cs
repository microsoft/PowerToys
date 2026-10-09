// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

public partial class BookmarkResolverTests
{
    [DataTestMethod]
    [DynamicData(dynamicDataSourceName: nameof(QuotedClassificationData.MixedQuotesScenarios), dynamicDataDeclaringType: typeof(QuotedClassificationData), DynamicDataDisplayName = nameof(FromCase))]
    public async Task Quoted_ValidateMixedQuotesScenarios(PlaceholderClassificationCase c) => await RunShared(c: c);

    [DataTestMethod]
    [DynamicData(dynamicDataSourceName: nameof(QuotedClassificationData.EscapedQuotes), dynamicDataDeclaringType: typeof(QuotedClassificationData), DynamicDataDisplayName = nameof(FromCase))]
    public async Task Quoted_ValidateEscapedQuotes(PlaceholderClassificationCase c) => await RunShared(c: c);

    [DataTestMethod]
    [DynamicData(dynamicDataSourceName: nameof(QuotedClassificationData.PartialMalformedQuotes), dynamicDataDeclaringType: typeof(QuotedClassificationData), DynamicDataDisplayName = nameof(FromCase))]
    public async Task Quoted_ValidatePartialMalformedQuotes(PlaceholderClassificationCase c) => await RunShared(c: c);

    [DataTestMethod]
    [DynamicData(dynamicDataSourceName: nameof(QuotedClassificationData.EnvironmentVariablesWithQuotes), dynamicDataDeclaringType: typeof(QuotedClassificationData), DynamicDataDisplayName = nameof(FromCase))]
    public async Task Quoted_ValidateEnvironmentVariablesWithQuotes(PlaceholderClassificationCase c) => await RunShared(c: c);

    [DataTestMethod]
    [DynamicData(dynamicDataSourceName: nameof(QuotedClassificationData.ShellProtocolPathsWithQuotes), dynamicDataDeclaringType: typeof(QuotedClassificationData), DynamicDataDisplayName = nameof(FromCase))]
    public async Task Quoted_ValidateShellProtocolPathsWithQuotes(PlaceholderClassificationCase c) => await RunShared(c: c);

    [DataTestMethod]
    [DynamicData(dynamicDataSourceName: nameof(QuotedClassificationData.CommandFlagsAndOptions), dynamicDataDeclaringType: typeof(QuotedClassificationData), DynamicDataDisplayName = nameof(FromCase))]
    public async Task Quoted_ValidateCommandFlagsAndOptions(PlaceholderClassificationCase c) => await RunShared(c: c);

    [DataTestMethod]
    [DynamicData(dynamicDataSourceName: nameof(QuotedClassificationData.NetworkPathsUnc), dynamicDataDeclaringType: typeof(QuotedClassificationData), DynamicDataDisplayName = nameof(FromCase))]
    public async Task Quoted_ValidateNetworkPathsUnc(PlaceholderClassificationCase c) => await RunShared(c: c);

    [DataTestMethod]
    [DynamicData(dynamicDataSourceName: nameof(QuotedClassificationData.RelativePathsWithQuotes), dynamicDataDeclaringType: typeof(QuotedClassificationData), DynamicDataDisplayName = nameof(FromCase))]
    public async Task Quoted_ValidateRelativePathsWithQuotes(PlaceholderClassificationCase c) => await RunShared(c: c);

    [DataTestMethod]
    [DynamicData(dynamicDataSourceName: nameof(QuotedClassificationData.EmptyAndWhitespaceCases), dynamicDataDeclaringType: typeof(QuotedClassificationData), DynamicDataDisplayName = nameof(FromCase))]
    public async Task Quoted_ValidateEmptyAndWhitespaceCases(PlaceholderClassificationCase c) => await RunShared(c: c);

    [DataTestMethod]
    [DynamicData(dynamicDataSourceName: nameof(QuotedClassificationData.RealWorldCommandScenarios), dynamicDataDeclaringType: typeof(QuotedClassificationData), DynamicDataDisplayName = nameof(FromCase))]
    public async Task Quoted_ValidateRealWorldCommandScenarios(PlaceholderClassificationCase c) => await RunShared(c: c);

    [DataTestMethod]
    [DynamicData(dynamicDataSourceName: nameof(QuotedClassificationData.SpecialCharactersInPaths), dynamicDataDeclaringType: typeof(QuotedClassificationData), DynamicDataDisplayName = nameof(FromCase))]
    public async Task Quoted_ValidateSpecialCharactersInPaths(PlaceholderClassificationCase c) => await RunShared(c: c);

    [DataTestMethod]
    [DynamicData(dynamicDataSourceName: nameof(QuotedClassificationData.QuotedPathsCurrentlyBroken), dynamicDataDeclaringType: typeof(QuotedClassificationData), DynamicDataDisplayName = nameof(FromCase))]
    public async Task Quoted_ValidateQuotedPathsCurrentlyBroken(PlaceholderClassificationCase c) => await RunShared(c: c);

    [DataTestMethod]
    [DynamicData(dynamicDataSourceName: nameof(QuotedClassificationData.QuotedPathsInCommands), dynamicDataDeclaringType: typeof(QuotedClassificationData), DynamicDataDisplayName = nameof(FromCase))]
    public async Task Quoted_ValidateQuotedPathsInCommands(PlaceholderClassificationCase c) => await RunShared(c: c);

    [DataTestMethod]
    [DynamicData(dynamicDataSourceName: nameof(QuotedClassificationData.QuotedAumid), dynamicDataDeclaringType: typeof(QuotedClassificationData), DynamicDataDisplayName = nameof(FromCase))]
    public async Task Quoted_ValidateQuotedUwpAppAumidCommands(PlaceholderClassificationCase c) => await RunShared(c: c);

    public static class QuotedClassificationData
    {
        public static IEnumerable<object[]> MixedQuotesScenarios() =>
        [
            [
                new PlaceholderClassificationCase(
                    Name: "Executable with quoted argument",
                    Input: "C:\\Windows\\notepad.exe \"C:\\my file.txt\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileExecutable,
                    ExpectedTarget: "C:\\Windows\\notepad.exe",
                    ExpectedArguments: "\"C:\\my file.txt\"",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "App with quoted argument containing spaces",
                    Input: "app.exe \"argument with spaces\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown,
                    ExpectedTarget: "app.exe",
                    ExpectedArguments: "\"argument with spaces\"",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Tool with input flag and quoted file",
                    Input: "C:\\tool.exe -input \"data file.txt\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileExecutable,
                    ExpectedTarget: "C:\\tool.exe",
                    ExpectedArguments: "-input \"data file.txt\"",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Multiple quoted arguments after path",
                    Input: "\"C:\\Program Files\\app.exe\" -file \"C:\\data\\input.txt\" -output \"C:\\results\\output.txt\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileExecutable,
                    ExpectedTarget: "C:\\Program Files\\app.exe",
                    ExpectedArguments: "-file \"C:\\data\\input.txt\" -output \"C:\\results\\output.txt\"",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Command with two quoted paths",
                    Input: "cmd /c \"C:\\First Path\\tool.exe\" \"C:\\Second Path\\file.txt\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.PathCommand,
                    ExpectedTarget: "C:\\Windows\\system32\\cmd.EXE",
                    ExpectedArguments: "/c \"C:\\First Path\\tool.exe\" \"C:\\Second Path\\file.txt\"",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ]
        ];

        public static IEnumerable<object[]> EscapedQuotes() =>
        [
            [
                new PlaceholderClassificationCase(
                    Name: "Path with escaped quotes in folder name",
                    Input: "C:\\Windows\\\\\\\"System32\\\\\\\"CatRoot\\\\",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown,
                    ExpectedTarget: "C:\\Windows\\\\\\\"System32\\\\\\\"CatRoot\\\\",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted path with trailing escaped quote",
                    Input: "\"C:\\Windows\\\\\\\"\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Directory,
                    ExpectedTarget: "C:\\Windows\\",
                    ExpectedLaunch: LaunchMethod.ExplorerOpen,
                    ExpectedIsPlaceholder: false)
            ]
        ];

        public static IEnumerable<object[]> PartialMalformedQuotes() =>
        [
            [
                new PlaceholderClassificationCase(
                    Name: "Unclosed quote at start",
                    Input: "\"C:\\Program Files\\app.exe",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileExecutable,
                    ExpectedTarget: "C:\\Program Files\\app.exe",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Quote in middle of unquoted path",
                    Input: "C:\\Some\\\"Path\\file.txt",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileDocument,
                    ExpectedTarget: "C:\\Some\\\"Path\\file.txt",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Unclosed quote - never ends",
                    Input: "\"Starts quoted but never ends",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown,
                    ExpectedTarget: "Starts quoted but never ends",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ]
        ];

        public static IEnumerable<object[]> EnvironmentVariablesWithQuotes() =>
        [
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted environment variable path with spaces",
                    Input: "\"%ProgramFiles%\\MyApp\\app.exe\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileExecutable,
                    ExpectedTarget: Path.Combine(path1: Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MyApp", "app.exe"),
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted USERPROFILE with document path",
                    Input: "\"%USERPROFILE%\\Documents\\file with spaces.txt\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileDocument,
                    ExpectedTarget: Path.Combine(path1: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "file with spaces.txt"),
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Environment variable with trailing args",
                    Input: "\"%ProgramFiles%\\App\" with args",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown,
                    ExpectedTarget: Path.Combine(path1: Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "App"),
                    ExpectedArguments: "with args",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Environment variable with trailing args",
                    Input: "%ProgramFiles%\\App with args",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown,
                    ExpectedTarget: Path.Combine(path1: Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "App"),
                    ExpectedArguments: "with args",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
        ];

        public static IEnumerable<object[]> ShellProtocolPathsWithQuotes() =>
        [
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted shell:Downloads",
                    Input: "\"shell:Downloads\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Directory,
                    ExpectedTarget: Path.Combine(path1: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                    ExpectedLaunch: LaunchMethod.ExplorerOpen,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted shell:Downloads with subpath",
                    Input: "\"shell:Downloads\\file.txt\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileDocument,
                    ExpectedTarget: Path.Combine(path1: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "file.txt"),
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Shell Desktop with subpath",
                    Input: "shell:Desktop\\file.txt",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileDocument,
                    ExpectedTarget: Path.Combine(path1: Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "file.txt"),
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted shell path with trailing text",
                    Input: "\"shell:Programs\" extra",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Directory,
                    ExpectedTarget: Path.Combine(paths: Environment.GetFolderPath(Environment.SpecialFolder.Programs)),
                    ExpectedLaunch: LaunchMethod.ExplorerOpen,
                    ExpectedIsPlaceholder: false)
            ]
        ];

        public static IEnumerable<object[]> CommandFlagsAndOptions() =>
        [
            [
                new PlaceholderClassificationCase(
                    Name: "Path followed by flag with quoted value",
                    Input: "C:\\app.exe -flag \"value\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileExecutable,
                    ExpectedTarget: "C:\\app.exe",
                    ExpectedArguments: "-flag \"value\"",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted tool with equals-style flag",
                    Input: "\"C:\\Program Files\\tool.exe\" --input=file.txt",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileExecutable,
                    ExpectedTarget: "C:\\Program Files\\tool.exe",
                    ExpectedArguments: "--input=file.txt",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Path with slash option and quoted value",
                    Input: "C:\\tool.exe /option \"quoted value\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileExecutable,
                    ExpectedTarget: "C:\\tool.exe",
                    ExpectedArguments: "/option \"quoted value\"",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Flag before quoted path",
                    Input: "--path \"C:\\Program Files\\app.exe\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown,
                    ExpectedTarget: "--path",
                    ExpectedArguments: "\"C:\\Program Files\\app.exe\"",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ]
        ];

        public static IEnumerable<object[]> NetworkPathsUnc() =>
        [
            [
                new PlaceholderClassificationCase(
                    Name: "UNC path unquoted",
                    Input: "\\\\server\\share\\folder\\file.txt",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileDocument,
                    ExpectedTarget: "\\\\server\\share\\folder\\file.txt",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted UNC path with spaces",
                    Input: "\"\\\\server\\share with spaces\\file.txt\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileDocument,
                    ExpectedTarget: "\\\\server\\share with spaces\\file.txt",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "UNC path with trailing args",
                    Input: "\"\\\\server\\share\\\" with args",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown,
                    ExpectedTarget: "\\\\server\\share\\",
                    ExpectedArguments: "with args",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted UNC app with flag",
                    Input: "\"\\\\server\\My Share\\app.exe\" --flag",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileExecutable,
                    ExpectedTarget: "\\\\server\\My Share\\app.exe",
                    ExpectedArguments: "--flag",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ]
        ];

        public static IEnumerable<object[]> RelativePathsWithQuotes() =>
        [
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted relative current path",
                    Input: "\".\\file.txt\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileDocument,
                    ExpectedTarget: Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "file.txt")),
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted relative parent path",
                    Input: "\"..\\parent folder\\file.txt\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileDocument,
                    ExpectedTarget: Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "..", "parent folder", "file.txt")),
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted relative home folder",
                    Input: "\"~\\current folder\\app.exe\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileExecutable,
                    ExpectedTarget: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "current folder\\app.exe"),
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ]
        ];

        public static IEnumerable<object[]> EmptyAndWhitespaceCases() =>
        [
            [
                new PlaceholderClassificationCase(
                    Name: "Empty string",
                    Input: string.Empty,
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown,
                    ExpectedTarget: string.Empty,
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Only whitespace",
                    Input: "   ",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown,
                    ExpectedTarget: "   ",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Just empty quotes",
                    Input: "\"\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown,
                    ExpectedTarget: string.Empty,
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted single space",
                    Input: "\" \"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown,
                    ExpectedTarget: " ",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ]
        ];

        public static IEnumerable<object[]> RealWorldCommandScenarios() =>
        [
#if CMDPAL_ENABLE_UNSAFE_TESTS
            [
                new PlaceholderClassificationCase(
                    Name: "Git clone command with full exe path with quoted path",
                    Input: "\"C:\\Program Files\\Git\\bin\\git.exe\" clone repo",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileExecutable,
                    ExpectedTarget: "C:\\Program Files\\Git\\bin\\git.exe",
                    ExpectedArguments: "clone repo",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Git clone command with quoted path",
                    Input: "git clone repo",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.PathCommand,
                    ExpectedTarget: "C:\\Program Files\\Git\\cmd\\git.EXE",
                    ExpectedArguments: "clone repo",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Visual Studio devenv with solution",
                    Input: "\"C:\\Program Files\\Microsoft Visual Studio\\2022\\Preview\\Common7\\IDE\\devenv.exe\" solution.sln",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileExecutable,
                    ExpectedTarget: "C:\\Program Files\\Microsoft Visual Studio\\2022\\Preview\\Common7\\IDE\\devenv.exe",
                    ExpectedArguments: "solution.sln",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Double-quoted Windows cmd pattern",
                    Input: "cmd /c \"\"C:\\Program Files\\app.exe\" arg1 arg2\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.PathCommand,
                    ExpectedTarget: "C:\\Windows\\system32\\cmd.EXE",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedArguments: "/c \"\"C:\\Program Files\\app.exe\" arg1 arg2\"",
                    ExpectedIsPlaceholder: false)
            ],
#endif
            [
                new PlaceholderClassificationCase(
                    Name: "PowerShell script with execution policy",
                    Input: "powershell -ExecutionPolicy Bypass -File \"C:\\Scripts\\My Script.ps1\" -param \"value\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.PathCommand,
                    ExpectedTarget: "C:\\WINDOWS\\system32\\WindowsPowerShell\\v1.0\\PowerShell.exe",
                    ExpectedArguments: "-ExecutionPolicy Bypass -File \"C:\\Scripts\\My Script.ps1\" -param \"value\"",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
        ];

        public static IEnumerable<object[]> SpecialCharactersInPaths() =>
        [
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted path with square brackets",
                    Input: "\"C:\\Path\\file[1].txt\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileDocument,
                    ExpectedTarget: "C:\\Path\\file[1].txt",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted path with parentheses",
                    Input: "\"C:\\Folder (2)\\app.exe\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileExecutable,
                    ExpectedTarget: "C:\\Folder (2)\\app.exe",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted path with hyphens and underscores",
                    Input: "\"C:\\Path\\file_name-123.txt\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileDocument,
                    ExpectedTarget: "C:\\Path\\file_name-123.txt",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ]
        ];

        public static IEnumerable<object[]> QuotedPathsCurrentlyBroken() =>
        [
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted path with spaces - complete path",
                    Input: "\"C:\\Program Files\\MyApp\\app.exe\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileExecutable,
                    ExpectedTarget: "C:\\Program Files\\MyApp\\app.exe",
                    ExpectedArguments: string.Empty,
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted path with spaces in user folder",
                    Input: "\"C:\\Users\\John Doe\\Documents\\file.txt\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileDocument,
                    ExpectedTarget: "C:\\Users\\John Doe\\Documents\\file.txt",
                    ExpectedArguments: string.Empty,
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted path with trailing arguments",
                    Input: "\"C:\\Program Files\\app.exe\" --flag",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileExecutable,
                    ExpectedTarget: "C:\\Program Files\\app.exe",
                    ExpectedArguments: "--flag",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted path with multiple arguments",
                    Input: "\"C:\\My Documents\\file.txt\" -output result.txt",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileDocument,
                    ExpectedTarget: "C:\\My Documents\\file.txt",
                    ExpectedArguments: "-output result.txt",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted path with trailing flag and value",
                    Input: "\"C:\\Tools\\converter.exe\" input.txt output.txt",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileExecutable,
                    ExpectedTarget: "C:\\Tools\\converter.exe",
                    ExpectedArguments: "input.txt output.txt",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ]
        ];

        public static IEnumerable<object[]> QuotedPathsInCommands() =>
        [
            [
                new PlaceholderClassificationCase(
                    Name: "cmd /c with quoted path",
                    Input: "cmd /c \"C:\\Program Files\\tool.exe\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.PathCommand,
                    ExpectedTarget: "C:\\Windows\\system32\\cmd.exe",
                    ExpectedArguments: "/c \"C:\\Program Files\\tool.exe\"",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "PowerShell with quoted script path",
                    Input: "powershell -File \"C:\\Scripts\\my script.ps1\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.PathCommand,
                    ExpectedTarget: Path.Combine(path1: Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", path4: "powershell.exe"),
                    ExpectedArguments: "-File \"C:\\Scripts\\my script.ps1\"",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "runas with quoted executable",
                    Input: "runas /user:admin \"C:\\Windows\\System32\\cmd.exe\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.PathCommand,
                    ExpectedTarget: "C:\\Windows\\system32\\runas.exe",
                    ExpectedArguments: "/user:admin \"C:\\Windows\\System32\\cmd.exe\"",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ]
        ];

        public static IEnumerable<object[]> QuotedAumid() =>
        [
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted UWP AUMID via AppsFolder",
                    Input: "\"shell:AppsFolder\\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App\"",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Aumid,
                    ExpectedTarget: "shell:AppsFolder\\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App",
                    ExpectedLaunch: LaunchMethod.ActivateAppId,
                    ExpectedIsPlaceholder: false),
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted UWP AUMID with AppsFolder prefix and argument",
                    Input: "\"shell:AppsFolder\\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App\" --maximized",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Aumid,
                    ExpectedTarget: "shell:AppsFolder\\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App",
                    ExpectedArguments: "--maximized",
                    ExpectedLaunch: LaunchMethod.ActivateAppId,
                    ExpectedIsPlaceholder: false),
            ],
        ];
    }
}
