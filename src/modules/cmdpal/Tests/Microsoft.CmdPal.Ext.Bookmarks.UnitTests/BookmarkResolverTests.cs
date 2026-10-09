// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

public partial class BookmarkResolverTests
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private static string _testDirPath;
    private static string _userHomeDirPath;
    private static string _testDirName;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    [ClassInitialize]
    public static void ClassSetup(TestContext context)
    {
        _userHomeDirPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _testDirName = "CmdPalBookmarkTests" + Guid.NewGuid().ToString("N");
        _testDirPath = Path.Combine(_userHomeDirPath, _testDirName);
        Directory.CreateDirectory(_testDirPath);

        // test files in user home
        File.WriteAllText(Path.Combine(_userHomeDirPath, "file.txt"), "This is a test text file.");

        // test files in test dir
        File.WriteAllText(Path.Combine(_testDirPath, "file.txt"), "This is a test text file.");
        File.WriteAllText(Path.Combine(_testDirPath, "app.exe"), "This is a test text file.");
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        if (Directory.Exists(_testDirPath))
        {
            Directory.Delete(_testDirPath, true);
        }

        if (File.Exists(Path.Combine(_userHomeDirPath, "file.txt")))
        {
            File.Delete(Path.Combine(_userHomeDirPath, "file.txt"));
        }
    }

    // must be public static to be used as DataTestMethod data source
    public static string FromCase(MethodInfo method, object[] data)
        => data is [PlaceholderClassificationCase c]
            ? c.Name
            : $"{method.Name}({string.Join(", ", data.Select(row => row.ToString()))})";

    private static async Task RunShared(PlaceholderClassificationCase c)
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
            Assert.AreEqual(c.ExpectedTarget, classification.Result.Target, StringComparer.OrdinalIgnoreCase, $"Target mismatch for input: {c.Input}");
            Assert.AreEqual(c.ExpectedLaunch, classification.Result.Launch, $"LaunchMethod mismatch for input: {c.Input}");
            Assert.AreEqual(c.ExpectedArguments, classification.Result.Arguments, $"Arguments mismatch for input: {c.Input}");
            Assert.AreEqual(c.ExpectedIsPlaceholder, classification.Result.IsPlaceholder, $"IsPlaceholder mismatch for input: {c.Input}");

            if (c.ExpectedDisplayName != null)
            {
                Assert.AreEqual(c.ExpectedDisplayName, classification.Result.DisplayName, $"DisplayName mismatch for input: {c.Input}");
            }
        }
    }

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
}
