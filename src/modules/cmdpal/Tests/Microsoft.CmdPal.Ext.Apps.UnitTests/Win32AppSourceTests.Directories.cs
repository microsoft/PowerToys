// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Catalog.Sources;
using Microsoft.CmdPal.Ext.Apps.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

public partial class Win32AppSourceTests
{
    [TestMethod]
    [DataRow(FileAttributes.Hidden)]
    [DataRow(FileAttributes.System)]
    public void DirectoryAttributes_FullAndIncrementalScansSkipExcludedSubfolders(FileAttributes attributes)
    {
        var root = CreateTemporaryDirectory("cmdpal-excluded-subfolder");
        try
        {
            var excluded = Directory.CreateDirectory(Path.Combine(root, "Excluded")).FullName;
            var nested = Directory.CreateDirectory(Path.Combine(excluded, "Nested")).FullName;
            var visibleShortcut = Path.Combine(root, "Visible.lnk");
            var excludedShortcut = Path.Combine(excluded, "Excluded.lnk");
            var nestedShortcut = Path.Combine(nested, "Nested.lnk");
            foreach (var path in new[] { visibleShortcut, excludedShortcut, nestedShortcut })
            {
                File.WriteAllText(path, string.Empty);
            }

            File.SetAttributes(visibleShortcut, File.GetAttributes(visibleShortcut) | attributes);
            File.SetAttributes(excluded, File.GetAttributes(excluded) | attributes);
            var source = CreateShortcutDirectorySource(root);

            CollectionAssert.AreEquivalent(new[] { visibleShortcut }, source.GetPaths().ToArray());
            CollectionAssert.AreEquivalent(new[] { visibleShortcut }, source.GetPathsForChange(root).ToArray());
            foreach (var path in new[] { excluded, nested, excludedShortcut, nestedShortcut })
            {
                Assert.AreEqual(0, source.GetPathsForChange(path).Count(), path);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(FileAttributes.Hidden)]
    [DataRow(FileAttributes.System)]
    public void DirectoryAttributes_ExplicitRootsAndTheirAncestorsRemainScannable(FileAttributes attributes)
    {
        var ancestor = CreateTemporaryDirectory("cmdpal-explicit-root");
        try
        {
            var root = Directory.CreateDirectory(Path.Combine(ancestor, "Root")).FullName;
            var nested = Directory.CreateDirectory(Path.Combine(root, "Nested")).FullName;
            var shortcut = Path.Combine(root, "Direct.lnk");
            var nestedShortcut = Path.Combine(nested, "Nested.lnk");
            File.WriteAllText(shortcut, string.Empty);
            File.WriteAllText(nestedShortcut, string.Empty);
            File.SetAttributes(ancestor, File.GetAttributes(ancestor) | attributes);
            File.SetAttributes(root, File.GetAttributes(root) | attributes);
            var source = CreateShortcutDirectorySource(root);
            var expected = new[] { shortcut, nestedShortcut };

            CollectionAssert.AreEquivalent(expected, source.GetPaths().ToArray());
            CollectionAssert.AreEquivalent(expected, source.GetPathsForChange(ancestor).ToArray());
            CollectionAssert.AreEquivalent(expected, source.GetPathsForChange(root).ToArray());
            CollectionAssert.AreEquivalent(new[] { nestedShortcut }, source.GetPathsForChange(nested).ToArray());
            foreach (var path in expected)
            {
                CollectionAssert.AreEquivalent(new[] { path }, source.GetPathsForChange(path).ToArray());
            }
        }
        finally
        {
            Directory.Delete(ancestor, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(FileAttributes.Hidden)]
    [DataRow(FileAttributes.System)]
    public async Task DirectoryAttributes_ChangesRemoveAndRestorePreviouslyIndexedItems(FileAttributes attributes)
    {
        var root = CreateTemporaryDirectory("cmdpal-directory-attributes");
        try
        {
            var folder = Directory.CreateDirectory(Path.Combine(root, "Apps")).FullName;
            var shortcut = Path.Combine(folder, "Tool.lnk");
            File.WriteAllText(shortcut, string.Empty);
            var origin = CreateShortcutDirectorySource(root);
            using var source = new Win32AppSource(
                origin,
                (path, _) =>
                {
                    var app = TestDataHelper.CreateTestWin32Metadata("Tool", Path.Combine(root, "Tool.exe"));
                    app.AppType = Win32AppType.Win32Application;
                    app.LnkFilePath = path;
                    return app;
                },
                createWatchers: false);
            var items = await source.LoadAsync(CancellationToken.None);
            Assert.AreEqual(1, items.Count);
            var originalAttributes = File.GetAttributes(folder);

            File.SetAttributes(folder, originalAttributes | attributes);
            Assert.IsTrue(origin.IsRelevantPath(folder), "An excluded folder must still invalidate its previously indexed items.");
            items = await source.ApplyChangesAsync(items, [new AppSourcePathChange(WatcherChangeTypes.Changed, folder)], CancellationToken.None);
            Assert.AreEqual(0, items.Count);

            File.SetAttributes(folder, originalAttributes);
            items = await source.ApplyChangesAsync(items, [new AppSourcePathChange(WatcherChangeTypes.Changed, folder)], CancellationToken.None);
            Assert.AreEqual(1, items.Count);
            Assert.AreEqual(shortcut, items[0].ToAppItem().LaunchTarget);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [DoNotParallelize]
    public void DirectoryAttributes_ScansDistinguishDirectoryLinksFromCloudSubfolders(bool isDirectoryLink)
    {
        var root = CreateTemporaryDirectory("cmdpal-reparse-subfolder");
        var originalFile = Win32FileEnumerator.FileWrapper;
        var originalDirectory = Win32FileEnumerator.DirectoryWrapper;
        try
        {
            var reparseDirectory = Directory.CreateDirectory(Path.Combine(root, "Reparse")).FullName;
            var nested = Directory.CreateDirectory(Path.Combine(reparseDirectory, "Nested")).FullName;
            var shortcut = Path.Combine(reparseDirectory, "Direct.lnk");
            var nestedShortcut = Path.Combine(nested, "Nested.lnk");
            File.WriteAllText(shortcut, string.Empty);
            File.WriteAllText(nestedShortcut, string.Empty);

            // Simulated link targets and reparse attributes avoid requiring symlink privileges or OneDrive on CI.
            var file = new Mock<IFile>();
            file.Setup(value => value.GetAttributes(It.IsAny<string>()))
                .Returns((string path) => File.GetAttributes(path)
                    | (StringComparer.OrdinalIgnoreCase.Equals(path, reparseDirectory)
                        || StringComparer.OrdinalIgnoreCase.Equals(path, nested) ? FileAttributes.ReparsePoint : 0));
            var directory = new Mock<IDirectory>();
            directory.Setup(value => value.EnumerateFiles(It.IsAny<string>(), It.IsAny<string>(), SearchOption.TopDirectoryOnly))
                .Returns((string path, string pattern, SearchOption options) => originalDirectory.EnumerateFiles(path, pattern, options));
            directory.Setup(value => value.EnumerateDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EnumerationOptions>()))
                .Returns((string path, string pattern, EnumerationOptions options) => originalDirectory.EnumerateDirectories(path, pattern, options));
            directory.Setup(value => value.ResolveLinkTarget(It.IsAny<string>(), false))
                .Returns((string path, bool _) => isDirectoryLink && StringComparer.OrdinalIgnoreCase.Equals(path, reparseDirectory)
                    ? new FileSystem().DirectoryInfo.New(root) : null);
            Win32FileEnumerator.FileWrapper = file.Object;
            Win32FileEnumerator.DirectoryWrapper = directory.Object;
            var source = CreateShortcutDirectorySource(root);
            var expected = isDirectoryLink ? Array.Empty<string>() : new[] { shortcut, nestedShortcut };
            CollectionAssert.AreEquivalent(expected, source.GetPaths().ToArray());
            CollectionAssert.AreEquivalent(expected, source.GetPathsForChange(root).ToArray());
            CollectionAssert.AreEquivalent(expected, source.GetPathsForChange(reparseDirectory).ToArray());
            CollectionAssert.AreEquivalent(isDirectoryLink ? [] : new[] { nestedShortcut }, source.GetPathsForChange(nested).ToArray());
            foreach (var path in new[] { shortcut, nestedShortcut })
            {
                CollectionAssert.AreEquivalent(isDirectoryLink ? [] : new[] { path }, source.GetPathsForChange(path).ToArray(), path);
            }

            if (isDirectoryLink)
            {
                file.Verify(value => value.GetAttributes(nested), Times.Never);
            }

            var explicitSource = CreateShortcutDirectorySource(reparseDirectory);
            CollectionAssert.AreEquivalent(new[] { shortcut, nestedShortcut }, explicitSource.GetPaths().ToArray());
            CollectionAssert.AreEquivalent(new[] { shortcut, nestedShortcut }, explicitSource.GetPathsForChange(reparseDirectory).ToArray());
            CollectionAssert.AreEquivalent(new[] { nestedShortcut }, explicitSource.GetPathsForChange(nestedShortcut).ToArray());
        }
        finally
        {
            Win32FileEnumerator.FileWrapper = originalFile;
            Win32FileEnumerator.DirectoryWrapper = originalDirectory;
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public void DirectoryAttributes_UnreadableLinkReportsFailureAndKeepsReadableSiblings()
    {
        var root = CreateTemporaryDirectory("cmdpal-unreadable-link");
        var originalFile = Win32FileEnumerator.FileWrapper;
        var originalDirectory = Win32FileEnumerator.DirectoryWrapper;
        try
        {
            var blocked = Directory.CreateDirectory(Path.Combine(root, "Blocked")).FullName;
            var readable = Directory.CreateDirectory(Path.Combine(root, "Readable")).FullName;
            var shortcut = Path.Combine(readable, "Tool.lnk");
            File.WriteAllText(shortcut, string.Empty);
            var error = new IOException("Fixture link read failure");
            var file = new Mock<IFile>();
            file.Setup(value => value.GetAttributes(It.IsAny<string>()))
                .Returns((string path) => File.GetAttributes(path) | (path == blocked ? FileAttributes.ReparsePoint : 0));
            var directory = new Mock<IDirectory>();
            directory.Setup(value => value.EnumerateFiles(It.IsAny<string>(), It.IsAny<string>(), SearchOption.TopDirectoryOnly))
                .Returns((string path, string pattern, SearchOption options) => originalDirectory.EnumerateFiles(path, pattern, options));
            directory.Setup(value => value.EnumerateDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EnumerationOptions>()))
                .Returns((string path, string pattern, EnumerationOptions options) => originalDirectory.EnumerateDirectories(path, pattern, options));
            directory.Setup(value => value.ResolveLinkTarget(blocked, false)).Throws(error);
            Win32FileEnumerator.FileWrapper = file.Object;
            Win32FileEnumerator.DirectoryWrapper = directory.Object;
            var source = CreateShortcutDirectorySource(root);
            var failedPaths = new List<string>();
            void ReportFailure(string path, Exception exception)
            {
                Assert.AreSame(error, exception);
                failedPaths.Add(path);
            }

            CollectionAssert.AreEqual(new[] { shortcut }, source.GetCandidates(ReportFailure).Select(candidate => candidate.Path).ToArray());
            CollectionAssert.AreEqual(new[] { blocked }, failedPaths);
            failedPaths.Clear();
            Assert.AreEqual(0, source.GetPathsForChange(Path.Combine(blocked, "Nested", "App.lnk"), ReportFailure).Count());
            CollectionAssert.AreEqual(new[] { blocked }, failedPaths);
            failedPaths.Clear();
            Assert.AreEqual(0, source.GetPathsForChange(blocked, ReportFailure).Count());
            CollectionAssert.AreEqual(new[] { blocked }, failedPaths);
        }
        finally
        {
            Win32FileEnumerator.FileWrapper = originalFile;
            Win32FileEnumerator.DirectoryWrapper = originalDirectory;
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void DirectoryAttributes_IncrementalScansReportMissingAncestors()
    {
        var root = CreateTemporaryDirectory("cmdpal-missing-ancestor");
        try
        {
            var missing = Path.Combine(root, "Missing");
            var shortcut = Path.Combine(missing, "Nested", "Tool.lnk");
            var missingPaths = new List<string>();
            var source = CreateShortcutDirectorySource(root);

            Assert.AreEqual(0, source.GetPathsForChange(shortcut, onMissing: missingPaths.Add).Count());
            CollectionAssert.AreEqual(new[] { missing }, missingPaths);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public void DirectoryAttributes_IncrementalScansReportUnreadableAncestors()
    {
        var root = CreateTemporaryDirectory("cmdpal-unreadable-ancestor");
        var originalFile = Win32FileEnumerator.FileWrapper;
        try
        {
            var blocked = Directory.CreateDirectory(Path.Combine(root, "Blocked")).FullName;
            var nested = Directory.CreateDirectory(Path.Combine(blocked, "Nested")).FullName;
            var shortcut = Path.Combine(nested, "Tool.lnk");
            File.WriteAllText(shortcut, string.Empty);
            var error = new UnauthorizedAccessException("Fixture access denial");
            var file = new Mock<IFile>();
            file.Setup(value => value.GetAttributes(It.IsAny<string>())).Returns((string path) => File.GetAttributes(path));
            file.Setup(value => value.GetAttributes(blocked)).Throws(error);
            Win32FileEnumerator.FileWrapper = file.Object;
            var source = CreateShortcutDirectorySource(root);

            foreach (var path in new[] { nested, shortcut })
            {
                var failedPaths = new List<string>();
                var candidates = source.GetPathsForChange(path, (failedPath, exception) =>
                {
                    Assert.AreSame(error, exception);
                    failedPaths.Add(failedPath);
                });

                Assert.AreEqual(0, candidates.Count());
                CollectionAssert.AreEqual(new[] { blocked }, failedPaths);
            }
        }
        finally
        {
            Win32FileEnumerator.FileWrapper = originalFile;
            Directory.Delete(root, recursive: true);
        }
    }
}
