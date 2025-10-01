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

[TestClass]
public partial class BookmarkResolverTests
{
    [DataTestMethod]
    [DynamicData(nameof(CommonClassificationData.CommonCases), typeof(CommonClassificationData), DynamicDataDisplayName = nameof(FromCase))]
    public async Task Common_ValidateCommonClassification(PlaceholderClassificationCase c) => await RunShared(c);

    [DataTestMethod]
    [DynamicData(nameof(CommonClassificationData.UwpAumidCases), typeof(CommonClassificationData), DynamicDataDisplayName = nameof(FromCase))]
    public async Task Common_ValidateUwpAumidClassification(PlaceholderClassificationCase c) => await RunShared(c);

    [DataTestMethod]
    [DynamicData(dynamicDataSourceName: nameof(CommonClassificationData.UnquotedRelativePaths), dynamicDataDeclaringType: typeof(CommonClassificationData), DynamicDataDisplayName = nameof(FromCase))]
    public async Task Common_ValidateUnquotedRelativePathScenarios(PlaceholderClassificationCase c) => await RunShared(c: c);

    [DataTestMethod]
    [DynamicData(dynamicDataSourceName: nameof(CommonClassificationData.UnquotedShellProtocol), dynamicDataDeclaringType: typeof(CommonClassificationData), DynamicDataDisplayName = nameof(FromCase))]
    public async Task Common_ValidateUnquotedShellProtocolScenarios(PlaceholderClassificationCase c) => await RunShared(c: c);

    private static class CommonClassificationData
    {
        public static IEnumerable<object[]> CommonCases()
        {
            return
            [
                [
                    new PlaceholderClassificationCase(
                        Name: "HTTPS URL",
                        Input: "https://microsoft.com",
                        ExpectSuccess: true,
                        ExpectedKind: CommandKind.WebUrl,
                        ExpectedTarget: "https://microsoft.com",
                        ExpectedLaunch: LaunchMethod.ShellExecute,
                        ExpectedIsPlaceholder: false)
                ],
                [
                    new PlaceholderClassificationCase(
                        Name: "WWW URL without scheme",
                        Input: "www.example.com",
                        ExpectSuccess: true,
                        ExpectedKind: CommandKind.WebUrl,
                        ExpectedTarget: "https://www.example.com",
                        ExpectedLaunch: LaunchMethod.ShellExecute,
                        ExpectedIsPlaceholder: false),
                ],
                [
                    new PlaceholderClassificationCase(
                        Name: "HTTP URL with query",
                        Input: "http://yahoo.com?p=search",
                        ExpectSuccess: true,
                        ExpectedKind: CommandKind.WebUrl,
                        ExpectedTarget: "http://yahoo.com?p=search",
                        ExpectedLaunch: LaunchMethod.ShellExecute,
                        ExpectedIsPlaceholder: false),
                ],
                [
                    new PlaceholderClassificationCase(
                        Name: "Mailto protocol",
                        Input: "mailto:user@example.com",
                        ExpectSuccess: true,
                        ExpectedKind: CommandKind.Protocol,
                        ExpectedTarget: "mailto:user@example.com",
                        ExpectedLaunch: LaunchMethod.ShellExecute,
                        ExpectedIsPlaceholder: false),
                ],
                [
                    new PlaceholderClassificationCase(
                        Name: "MS-Settings protocol",
                        Input: "ms-settings:display",
                        ExpectSuccess: true,
                        ExpectedKind: CommandKind.Protocol,
                        ExpectedTarget: "ms-settings:display",
                        ExpectedLaunch: LaunchMethod.ShellExecute,
                        ExpectedIsPlaceholder: false),
                ],
                [
                    new PlaceholderClassificationCase(
                        Name: "Custom protocol",
                        Input: "myapp:doit",
                        ExpectSuccess: true,
                        ExpectedKind: CommandKind.Protocol,
                        ExpectedTarget: "myapp:doit",
                        ExpectedLaunch: LaunchMethod.ShellExecute,
                        ExpectedIsPlaceholder: false),
                ],
                [
                    new PlaceholderClassificationCase(
                        Name: "Not really a valid protocol",
                        Input: "this is not really a protocol myapp: doit",
                        ExpectSuccess: true,
                        ExpectedKind: CommandKind.Unknown,
                        ExpectedTarget: "this",
                        ExpectedArguments: "is not really a protocol myapp: doit",
                        ExpectedLaunch: LaunchMethod.ShellExecute,
                        ExpectedIsPlaceholder: false),
                ],
                [
                    new PlaceholderClassificationCase(
                        Name: "Drive",
                        Input: "C:",
                        ExpectSuccess: true,
                        ExpectedKind: CommandKind.Directory,
                        ExpectedTarget: "C:\\",
                        ExpectedLaunch: LaunchMethod.ExplorerOpen,
                        ExpectedIsPlaceholder: false),
                ],
                [
                    new PlaceholderClassificationCase(
                        Name: "Non-existing path with extension",
                        Input: "C:\\this-folder-should-not-exist-12345\\file.txt",
                        ExpectSuccess: true,
                        ExpectedKind: CommandKind.FileDocument,
                        ExpectedTarget: "C:\\this-folder-should-not-exist-12345\\file.txt",
                        ExpectedLaunch: LaunchMethod.ShellExecute,
                        ExpectedIsPlaceholder: false),
                ],
                [
                    new PlaceholderClassificationCase(
                        Name: "Unknown fallback",
                        Input: "some_unlikely_command_name_12345",
                        ExpectSuccess: true,
                        ExpectedKind: CommandKind.Unknown,
                        ExpectedTarget: "some_unlikely_command_name_12345",
                        ExpectedLaunch: LaunchMethod.ShellExecute,
                        ExpectedIsPlaceholder: false),
                ],

                [new PlaceholderClassificationCase(
                        Name: "Simple unquoted executable path",
                        Input: "C:\\Windows\\System32\\notepad.exe",
                        ExpectSuccess: true,
                        ExpectedKind: CommandKind.FileExecutable,
                        ExpectedTarget: "C:\\Windows\\System32\\notepad.exe",
                        ExpectedArguments: string.Empty,
                        ExpectedLaunch: LaunchMethod.ShellExecute,
                        ExpectedIsPlaceholder: false),
                ],
                [
                    new PlaceholderClassificationCase(
                        Name: "Unquoted document path (non existed file)",
                        Input: "C:\\Users\\John\\Documents\\file.txt",
                        ExpectSuccess: true,
                        ExpectedKind: CommandKind.FileDocument,
                        ExpectedTarget: "C:\\Users\\John\\Documents\\file.txt",
                        ExpectedArguments: string.Empty,
                        ExpectedLaunch: LaunchMethod.ShellExecute,
                        ExpectedIsPlaceholder: false),
                ]
            ];
        }

        public static IEnumerable<object[]> UwpAumidCases() =>
        [
            [
                new PlaceholderClassificationCase(
                    Name: "UWP AUMID with AppsFolder prefix",
                    Input: "shell:AppsFolder\\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Aumid,
                    ExpectedTarget: "shell:AppsFolder\\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
                    ExpectedLaunch: LaunchMethod.ActivateAppId,
                    ExpectedIsPlaceholder: false),
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "UWP AUMID with AppsFolder prefix and argument (Trap)",
                    Input: "shell:AppsFolder\\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App --maximized",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Aumid,
                    ExpectedTarget: "shell:AppsFolder\\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App --maximized",
                    ExpectedArguments: string.Empty,
                    ExpectedLaunch: LaunchMethod.ActivateAppId,
                    ExpectedIsPlaceholder: false),
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "UWP AUMID via AppsFolder",
                    Input: "shell:AppsFolder\\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Aumid,
                    ExpectedTarget: "shell:AppsFolder\\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App",
                    ExpectedLaunch: LaunchMethod.ActivateAppId,
                    ExpectedIsPlaceholder: false),
            ],
        ];

        public static IEnumerable<object[]> UnquotedShellProtocol() =>
        [
            [
                new PlaceholderClassificationCase(
                    Name: "Shell for This PC (shell:::{20D04FE0-3AEA-1069-A2D8-08002B30309D})",
                    Input: "shell:::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.VirtualShellItem,
                    ExpectedTarget: "shell:::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false),
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Shell for This PC (::{20D04FE0-3AEA-1069-A2D8-08002B30309D})",
                    Input: "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.VirtualShellItem,
                    ExpectedTarget: "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false),
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Shell protocol for My Documents (shell:::{450D8FBA-AD25-11D0-98A8-0800361B1103}",
                    Input: "shell:::{450D8FBA-AD25-11D0-98A8-0800361B1103}",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Directory,
                    ExpectedTarget: Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    ExpectedLaunch: LaunchMethod.ExplorerOpen,
                    ExpectedIsPlaceholder: false),
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Shell protocol for My Documents (::{450D8FBA-AD25-11D0-98A8-0800361B1103}",
                    Input: "::{450D8FBA-AD25-11D0-98A8-0800361B1103}",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Directory,
                    ExpectedTarget: Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    ExpectedLaunch: LaunchMethod.ExplorerOpen,
                    ExpectedIsPlaceholder: false),
            ],
            [
                new PlaceholderClassificationCase(
                    Name: "Shell protocol for AppData (shell:appdata)",
                    Input: "shell:appdata",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Directory,
                    ExpectedTarget: Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    ExpectedLaunch: LaunchMethod.ExplorerOpen,
                    ExpectedIsPlaceholder: false),
            ],
            [

                // let's pray this works on all systems
                new PlaceholderClassificationCase(
                    Name: "Shell protocol for AppData + subpath (shell:appdata\\microsoft)",
                    Input: "shell:appdata\\microsoft",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Directory,
                    ExpectedTarget: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft"),
                    ExpectedLaunch: LaunchMethod.ExplorerOpen,
                    ExpectedIsPlaceholder: false),
            ],
        ];

        public static IEnumerable<object[]> UnquotedRelativePaths() =>
        [
            [
                new PlaceholderClassificationCase(
                    Name: "Unquoted relative current path",
                    Input: ".\\file.txt",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileDocument,
                    ExpectedTarget: Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "file.txt")),
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
#if CMDPAL_ENABLE_UNSAFE_TESTS
            It's not really a good idea blindly write to directory out of user profile
            [
                new PlaceholderClassificationCase(
                    Name: "Unquoted relative parent path",
                    Input: "..\\parent folder\\file.txt",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileDocument,
                    ExpectedTarget: Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "..", "parent folder", "file.txt")),
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ],
#endif // CMDPAL_ENABLE_UNSAFE_TESTS
            [
                new PlaceholderClassificationCase(
                    Name: "Unquoted relative home folder",
                    Input: $"~\\{_testDirName}\\app.exe",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileExecutable,
                    ExpectedTarget: Path.Combine(_testDirPath, "app.exe"),
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ]
        ];
    }
}
