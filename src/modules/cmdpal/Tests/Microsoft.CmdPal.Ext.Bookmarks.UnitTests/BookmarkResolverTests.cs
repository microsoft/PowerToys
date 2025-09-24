﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public class BookmarkResolverTests
{
    [DataTestMethod]
    [DynamicData(nameof(PlaceholderClassificationData.PlaceholderCases), typeof(PlaceholderClassificationData), DynamicDataDisplayName = nameof(FromCase))]
    public async Task ValidatePlaceholderClassification(PlaceholderClassificationCase c)
    {
        // Arrange
        IBookmarkResolver resolver = new BookmarkResolver(new PlaceholderParser());

        // Act
        var classification = await resolver.TryClassifyAsync(c.Input, CancellationToken.None);

        // Assert
        Assert.IsNotNull(classification);
        Assert.AreEqual(c.ExpectSuccess, classification.Success, "Success flag mismatch.");

        if (c.ExpectSuccess)
        {
            Assert.IsNotNull(classification.Result, "Result should not be null for successful classification.");
            Assert.AreEqual(c.ExpectedKind, classification.Result.Kind, $"CommandKind mismatch for input: {c.Input}");
            Assert.AreEqual(c.ExpectedTarget, classification.Result.Target, $"Target mismatch for input: {c.Input}");
            Assert.AreEqual(c.ExpectedLaunch, classification.Result.Launch, $"LaunchMethod mismatch for input: {c.Input}");
            Assert.AreEqual(c.ExpectedArguments, classification.Result.Arguments, $"Arguments mismatch for input: {c.Input}");
            Assert.AreEqual(c.ExpectedIsPlaceholder, classification.Result.IsPlaceholder, $"IsPlaceholder mismatch for input: {c.Input}");

            if (c.ExpectedDisplayName != null)
            {
                Assert.AreEqual(c.ExpectedDisplayName, classification.Result.DisplayName, $"DisplayName mismatch for input: {c.Input}");
            }
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(PlaceholderClassificationData.EdgeCases), typeof(PlaceholderClassificationData), DynamicDataDisplayName = nameof(FromCase))]
    public async Task ValidatePlaceholderEdgeCases(PlaceholderClassificationCase c)
    {
        // Arrange
        IBookmarkResolver resolver = new BookmarkResolver(new PlaceholderParser());

        // Act & Assert - Should not throw exceptions
        var classification = await resolver.TryClassifyAsync(c.Input, CancellationToken.None);

        Assert.IsNotNull(classification);
        Assert.AreEqual(c.ExpectSuccess, classification.Success);

        if (c.ExpectSuccess && classification.Result != null)
        {
            Assert.AreEqual(c.ExpectedIsPlaceholder, classification.Result.IsPlaceholder);
            Assert.AreEqual(c.Input, classification.Result.Input, "OriginalInput should be preserved");
        }
    }

    public static string FromCase(MethodInfo method, object[] data)
        => data is [PlaceholderClassificationCase c]
            ? c.Name
            : $"{method.Name}({string.Join(", ", data.Select(row => row.ToString()))})";

    public sealed record PlaceholderClassificationCase(
        string Name,                       // Friendly name for Test Explorer
        string Input,                      // Input string passed to classifier
        bool ExpectSuccess,                // Expected Success flag
        CommandKind ExpectedKind,          // Expected Result.Kind
        string ExpectedTarget,             // Expected Result.Target (normalized)
        LaunchMethod ExpectedLaunch,       // Expected Result.Launch
        bool ExpectedIsPlaceholder,        // Expected Result.IsPlaceholder
        string ExpectedArguments = "",     // Expected Result.Arguments
        string? ExpectedDisplayName = null // Expected Result.DisplayName
    );

    private static class PlaceholderClassificationData
    {
        public static IEnumerable<object[]> PlaceholderCases()
        {
            // UWP/AUMID with placeholders
            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "UWP AUMID with package placeholder",
                    Input: "shell:AppsFolder\\{packageFamily}!{appId}",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.UwpAumid,
                    ExpectedTarget: "shell:AppsFolder\\{packageFamily}!{appId}",
                    ExpectedLaunch: LaunchMethod.UwpActivate,
                    ExpectedIsPlaceholder: true)
            ];

            yield return
            [

                // Expects no special handling
                new PlaceholderClassificationCase(
                    Name: "Bare UWP AUMID with placeholders",
                    Input: "{packageFamily}!{appId}",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown,
                    ExpectedTarget: "{packageFamily}!{appId}",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: true)
            ];

            // Web URLs with placeholders
            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "HTTPS URL with domain placeholder",
                    Input: "https://{domain}/path",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.WebUrl,
                    ExpectedTarget: "https://{domain}/path",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: true)
            ];

            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "WWW URL with site placeholder",
                    Input: "www.{site}.com",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.WebUrl,
                    ExpectedTarget: "https://www.{site}.com",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: true)
            ];

            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "WWW URL - Yahoo with Search",
                    Input: "http://yahoo.com?p={search}",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.WebUrl,
                    ExpectedTarget: "http://yahoo.com?p={search}",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: true)
            ];

            // Protocol URLs with placeholders
            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "Mailto protocol with email placeholder",
                    Input: "mailto:{email}",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Protocol,
                    ExpectedTarget: "mailto:{email}",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: true)
            ];

            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "MS-Settings protocol with category placeholder",
                    Input: "ms-settings:{category}",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Protocol,
                    ExpectedTarget: "ms-settings:{category}",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: true)
            ];

            // File executables with placeholders - These might classify as Unknown currently
            // due to nonexistent paths, but should preserve placeholder flag
            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "Executable with profile path placeholder",
                    Input: "{userProfile}\\Documents\\app.exe",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileExecutable, // May be Unknown if path doesn't exist
                    ExpectedTarget: "{userProfile}\\Documents\\app.exe",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: true)
            ];

            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "Executable with program files placeholder",
                    Input: "{programFiles}\\MyApp\\tool.exe",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileExecutable, // May be Unknown if path doesn't exist
                    ExpectedTarget: "{programFiles}\\MyApp\\tool.exe",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: true)
            ];

            // Commands with placeholders
            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "Command with placeholder and arguments",
                    Input: "{editor} {filename}",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown, // Likely Unknown since command won't be found in PATH
                    ExpectedTarget: "{editor} {filename}",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: true)
            ];

            // Directory paths with placeholders
            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "Directory with user profile placeholder",
                    Input: "{userProfile}\\Documents",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown, // May be Unknown if path doesn't exist during classification
                    ExpectedTarget: "{userProfile}\\Documents",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: true)
            ];

            // Complex quoted paths with placeholders
            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "Quoted executable path with placeholders and args",
                    Input: "\"{programFiles}\\{appName}\\{executable}.exe\" --verbose",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileExecutable, // Likely Unknown due to nonexistent path
                    ExpectedTarget: "{programFiles}\\{appName}\\{executable}.exe",
                    ExpectedArguments: "--verbose",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: true)
            ];

            // Shell paths with placeholders
            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "Shell folder with placeholder",
                    Input: "shell:{folder}\\{filename}",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown, // Depends on shell path resolution
                    ExpectedTarget: "shell:{folder}\\{filename}",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: true)
            ];

            // Shell paths with placeholders
            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "Shell folder with placeholder",
                    Input: "shell:knownFolder\\{filename}",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown, // Depends on shell path resolution
                    ExpectedTarget: "shell:knownFolder\\{filename}",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: true)
            ];

            // Commands with braces in arguments
            /*
             TODO: depends on system, shell resolution has to be mocked
            yield return
            [

                // code {param1}
                new PlaceholderClassificationCase(
                    Name: "Command with braces in arguments",
                    Input: "code {param1}",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.PathCommand,
                    ExpectedTarget: "code",
                    ExpectedArguments: "{param1}",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: true)
            ];
            */

            // Mixed literal and placeholder paths
            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "Mixed literal and placeholder path",
                    Input: "C:\\{folder}\\app.exe",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.FileExecutable, // Behavior depends on partial path resolution
                    ExpectedTarget: "C:\\{folder}\\app.exe",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: true)
            ];

            // Multiple placeholders
            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "Multiple placeholders in path",
                    Input: "{drive}\\{folder}\\{subfolder}\\{file}.{ext}",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown,
                    ExpectedTarget: "{drive}\\{folder}\\{subfolder}\\{file}.{ext}",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: true)
            ];
        }

        public static IEnumerable<object[]> EdgeCases()
        {
            // Empty and malformed placeholders
            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "Empty placeholder",
                    Input: "{} file.exe",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown,
                    ExpectedTarget: "{} file.exe",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ];

            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "Unclosed placeholder",
                    Input: "{unclosed file.exe",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown,
                    ExpectedTarget: "{unclosed file.exe",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ];

            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "Placeholder with spaces",
                    Input: "{with spaces}\\file.exe",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown,
                    ExpectedTarget: "{with spaces}\\file.exe",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ];

            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "Nested placeholders",
                    Input: "{outer{inner}}\\file.exe",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown,
                    ExpectedTarget: "{outer{inner}}\\file.exe",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ];

            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "Only closing brace",
                    Input: "file} something",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown,
                    ExpectedTarget: "file} something",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: false)
            ];

            // Very long placeholder names
            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "Very long placeholder name",
                    Input: "{thisIsVeryLongPlaceholderNameThatShouldStillWorkProperly}\\file.exe",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown,
                    ExpectedTarget: "{thisIsVeryLongPlaceholderNameThatShouldStillWorkProperly}\\file.exe",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: true)
            ];

            // Special characters in placeholders
            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "Placeholder with underscores",
                    Input: "{user_profile}\\file.exe",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown,
                    ExpectedTarget: "{user_profile}\\file.exe",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: true)
            ];

            yield return
            [
                new PlaceholderClassificationCase(
                    Name: "Placeholder with numbers",
                    Input: "{path123}\\file.exe",
                    ExpectSuccess: true,
                    ExpectedKind: CommandKind.Unknown,
                    ExpectedTarget: "{path123}\\file.exe",
                    ExpectedLaunch: LaunchMethod.ShellExecute,
                    ExpectedIsPlaceholder: true)
            ];
        }
    }
}
