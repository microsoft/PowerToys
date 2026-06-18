// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;
using Microsoft.CmdPal.Ext.Bookmarks.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public class BookmarkResolverPathCharactersTests
{
    [TestMethod]
    public async Task Classification_And_Launch_With_Accents_Spaces_Punctuation()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), "CmdPalBookmarkPathChars" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var folderName = "Folder Éñü,;(') Test";
            var fileName = "file Éñü,;(').txt";
            var folderPath = Path.Combine(tempRoot, folderName);
            Directory.CreateDirectory(folderPath);

            var filePath = Path.Combine(folderPath, fileName);
            File.WriteAllText(filePath, "hello world");

            var resolver = new BookmarkResolver(new PlaceholderParser());

            // Act - classify existing file
            var classification = resolver.ClassifyOrUnknown(filePath);

            // Assert - classification identifies the file
            Assert.AreEqual(CommandKind.FileDocument, classification.Kind);
            Assert.AreEqual(filePath, classification.Target, ignoreCase: true);

            // Arrange - intercept launches
            Classification? launched = null;
            CommandLauncher.TestLaunchOverride = c => { launched = c; return true; };

            var bookmarkData = new BookmarkData("Test Bookmark", filePath);
            var iconLocator = new TestIconLocator();

            var cmd = new LaunchBookmarkCommand(bookmarkData, classification, iconLocator, resolver);

            // Act - invoke (should re-classify and launch the file)
            var result = cmd.Invoke(sender: this);

            // Assert - launch was attempted for the exact file
            Assert.IsNotNull(launched);
            Assert.AreEqual(filePath, launched!.Target, ignoreCase: true);

            // Cleanup override
            CommandLauncher.TestLaunchOverride = null;
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }

    [TestMethod]
    public void Fallback_To_Existing_Parent_When_Target_Missing()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), "CmdPalBookmarkPathChars" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var folderName = "Parent Éñü (fallback), Test";
            var folderPath = Path.Combine(tempRoot, folderName);
            Directory.CreateDirectory(folderPath);

            var missingFilePath = Path.Combine(folderPath, "this-file-does-not-exist-çüñ.txt");

            var resolver = new BookmarkResolver(new PlaceholderParser());

            // Act - classify non-existing file path
            var classification = resolver.ClassifyOrUnknown(missingFilePath);

            // Assert - classification should resolve to Directory (the nearest existing parent)
            Assert.AreEqual(CommandKind.Directory, classification.Kind);
            Assert.AreEqual(folderPath, classification.Target, ignoreCase: true);

            // Arrange - intercept launches
            Classification? launched = null;
            CommandLauncher.TestLaunchOverride = c => { launched = c; return true; };

            var bookmarkData = new BookmarkData("Missing Bookmark", missingFilePath);
            var iconLocator = new TestIconLocator();

            var cmd = new LaunchBookmarkCommand(bookmarkData, classification, iconLocator, resolver);

            // Act - invoke (should re-classify and launch the parent directory)
            var result = cmd.Invoke(sender: this);

            // Assert - launch was attempted for the parent folder
            Assert.IsNotNull(launched);
            Assert.AreEqual(folderPath, launched!.Target, ignoreCase: true);

            // Cleanup override
            CommandLauncher.TestLaunchOverride = null;
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }

    private sealed class TestIconLocator : IBookmarkIconLocator
    {
        public Task<IIconInfo> GetIconForPath(Classification classification, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Icons.Reloading);
        }
    }
}
