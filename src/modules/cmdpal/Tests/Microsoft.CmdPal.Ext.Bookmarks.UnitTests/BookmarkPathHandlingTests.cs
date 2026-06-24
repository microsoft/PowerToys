// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Bookmarks.Commands;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;
using Microsoft.CmdPal.Ext.Bookmarks.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public class BookmarkPathHandlingTests
{

    [TestMethod]
    public async Task Accented_NonAscii_Path_Is_Classified_As_Directory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "CmdPalBookmarkTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var accentedName = "Éfolder";
        var dir = Path.Combine(tempRoot, accentedName);
        Directory.CreateDirectory(dir);

        try
        {
            IBookmarkResolver resolver = new BookmarkResolver(new PlaceholderParser());
            var classification = await resolver.TryClassifyAsync(dir, CancellationToken.None);

            Assert.IsTrue(classification.Success, "Classification should succeed for existing accented dir.");
            Assert.AreEqual(CommandKind.Directory, classification.Result.Kind);
            Assert.AreEqual(Path.GetFullPath(dir), classification.Result.Target, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [TestMethod]
    public async Task Spaces_And_Punctuation_In_Path_Are_Handled()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "CmdPalBookmarkTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var folderName = "My Folder, (Test)";
        var dir = Path.Combine(tempRoot, folderName);
        Directory.CreateDirectory(dir);

        try
        {
            IBookmarkResolver resolver = new BookmarkResolver(new PlaceholderParser());
            var classification = await resolver.TryClassifyAsync(dir, CancellationToken.None);

            Assert.IsTrue(classification.Success, "Classification should succeed for existing folder with spaces/punctuation.");
            Assert.AreEqual(CommandKind.Directory, classification.Result.Kind);
            Assert.AreEqual(Path.GetFullPath(dir), classification.Result.Target, StringComparer.OrdinalIgnoreCase);

            // Also verify that percent-encoded input for the same folder (conservative decoding) resolves to same directory if decoder present
            var encoded = Path.Combine(Path.GetDirectoryName(dir) ?? string.Empty, folderName.Replace(" ", "%20"));
            var classificationEncoded = await resolver.TryClassifyAsync(encoded, CancellationToken.None);

            // either decoding is supported and classification succeeds, or it remains unknown; in either case, do not start external processes in this test
            if (classificationEncoded.Success)
            {
                Assert.AreEqual(CommandKind.Directory, classificationEncoded.Result.Kind);
                Assert.AreEqual(Path.GetFullPath(dir), classificationEncoded.Result.Target, StringComparer.OrdinalIgnoreCase);
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [TestMethod]
    public async Task PercentEncoded_Accented_Input_Decodes_To_Existing_Accented_Path_When_Present()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "CmdPalBookmarkTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var accentedName = "Éfolder";
        var dir = Path.Combine(tempRoot, accentedName);
        Directory.CreateDirectory(dir);

        try
        {
            IBookmarkResolver resolver = new BookmarkResolver(new PlaceholderParser());

            // Percent-encode the UTF8 bytes of the accented name (capital E with acute)
            var utf8 = System.Text.Encoding.UTF8.GetBytes(accentedName);
            var encodedName = string.Concat(Array.ConvertAll(utf8, b => $"%{b:X2}"));

            var encodedPath = Path.Combine(tempRoot, encodedName);
            var classification = await resolver.TryClassifyAsync(encodedPath, CancellationToken.None);

            // Conservative behavior: if resolver decodes percent-encoding it should resolve to the real folder
            if (classification.Success)
            {
                Assert.AreEqual(CommandKind.Directory, classification.Result.Kind);
                Assert.AreEqual(Path.GetFullPath(dir), classification.Result.Target, StringComparer.OrdinalIgnoreCase);
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [TestMethod]
    public async Task LaunchBookmark_FallsBack_To_Nearest_Existing_Parent_Directory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "CmdPalBookmarkTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var parent = Path.Combine(tempRoot, "parent");
        Directory.CreateDirectory(parent);

        var child = Path.Combine(parent, "child", "file.txt"); // child does not exist

        try
        {
            var bookmark = new BookmarkData("TestBookmark", child);
            IBookmarkResolver resolver = new BookmarkResolver(new PlaceholderParser());
            var classification = resolver.ClassifyOrUnknown(bookmark.Bookmark);

            // Use a mock process launcher to capture what gets launched
            var mockLauncher = new MockProcessLauncher();
            var launchCmd = new LaunchBookmarkCommand(bookmark, classification, new TestBookmarkIconLocator(), resolver, mockLauncher);
            var result = launchCmd.Invoke(sender: this);

            // Should have launched a classification targeting the existing parent directory
            Assert.IsNotNull(mockLauncher.LastLaunchedClassification, "Expected a launch to be captured");
            Assert.AreEqual(CommandKind.Directory, mockLauncher.LastLaunchedClassification.Kind);
            Assert.IsTrue(Directory.Exists(parent));
            Assert.AreEqual(Path.GetFullPath(parent), mockLauncher.LastLaunchedClassification.Target, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    private sealed class MockProcessLauncher : IProcessLauncher
    {
        public Classification? LastLaunchedClassification { get; private set; }

        public bool Launch(Classification classification, bool runAsAdmin = false)
        {
            LastLaunchedClassification = classification;
            return true;
        }
    }

    private sealed class TestBookmarkIconLocator : IBookmarkIconLocator
    {
        public Task<IIconInfo> GetIconForPath(Classification classification, CancellationToken cancellationToken = default)
        {
            // Return some default icon quickly; tests don't rely on icon semantics
            return Task.FromResult<IIconInfo>(Icons.Reloading);
        }
    }
}
