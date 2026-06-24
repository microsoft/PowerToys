// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public class BookmarkResolverGreedyFindTests
{
    [TestMethod]
    public async Task Resolver_Handles_Different_Unicode_Normalization_Forms()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "CmdPalBookmarkTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var nfcName = "\u00C9folder"; // Éfolder (precomposed)
        var nfdName = "E\u0301folder"; // E + combining acute

        var dirNfc = Path.Combine(tempRoot, nfcName);
        Directory.CreateDirectory(dirNfc);

        try
        {
            IBookmarkResolver resolver = new BookmarkResolver(new PlaceholderParser());

            var inputNfc = dirNfc;
            var cNfc = await resolver.TryClassifyAsync(inputNfc, CancellationToken.None);
            Assert.IsTrue(cNfc.Success);
            Assert.AreEqual(CommandKind.Directory, cNfc.Result.Kind);
            Assert.AreEqual(Path.GetFullPath(dirNfc), cNfc.Result.Target, StringComparer.OrdinalIgnoreCase);

            var inputNfd = Path.Combine(tempRoot, nfdName);
            var cNfd = await resolver.TryClassifyAsync(inputNfd, CancellationToken.None);
            Assert.IsTrue(cNfd.Success, "Resolver should succeed for NFD-encoded path if equivalent NFC exists on disk");
            Assert.AreEqual(CommandKind.Directory, cNfd.Result.Kind);
            Assert.AreEqual(Path.GetFullPath(dirNfc), cNfd.Result.Target, StringComparer.OrdinalIgnoreCase);
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
    public async Task GreedyFind_Resolves_With_Mixed_Separators_And_Forward_Slashes()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "CmdPalBookmarkTests", Guid.NewGuid().ToString("N"));
        var a = Path.Combine(tempRoot, "A");
        var b = Path.Combine(a, "B");
        Directory.CreateDirectory(b);

        var file = Path.Combine(b, "file.txt");
        File.WriteAllText(file, "hello");

        try
        {
            IBookmarkResolver resolver = new BookmarkResolver(new PlaceholderParser());

            // Mixed separators
            var mixed = tempRoot + "\\A/B\\file.txt extra args";
            var rMixed = await resolver.TryClassifyAsync(mixed, CancellationToken.None);
            Assert.IsTrue(rMixed.Success);
            Assert.AreEqual(CommandKind.FileDocument, rMixed.Result.Kind);
            Assert.AreEqual(Path.GetFullPath(file), rMixed.Result.Target, StringComparer.OrdinalIgnoreCase);

            // Forward slashes only
            var forward = tempRoot + "/A/B/file.txt";
            var rForward = await resolver.TryClassifyAsync(forward, CancellationToken.None);
            Assert.IsTrue(rForward.Success);
            Assert.AreEqual(CommandKind.FileDocument, rForward.Result.Kind);
            Assert.AreEqual(Path.GetFullPath(file), rForward.Result.Target, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }
}
