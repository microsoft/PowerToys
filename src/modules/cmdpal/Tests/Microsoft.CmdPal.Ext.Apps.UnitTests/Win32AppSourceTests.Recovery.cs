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
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Catalog.Sources;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

public partial class Win32AppSourceTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [DoNotParallelize]
    public async Task Recovery_PromotedRetryReReadsCachedRejectionWithoutReReadingOtherCandidates(bool pathSource)
    {
        var root = CreateTemporaryDirectory("cmdpal-promoted-retry");
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var rejected = Path.Combine(root, "Rejected.lnk");
            var unchanged = Path.Combine(root, "Unchanged.lnk");
            File.WriteAllText(rejected, "shortcut fixture");
            File.WriteAllText(unchanged, "shortcut fixture");
            Environment.SetEnvironmentVariable("PATH", root);
            var settingsPath = Path.Combine(root, "settings.json");
            File.WriteAllText(settingsPath, "{\"apps.EnablePathEnvironmentVariableSource\":\"true\",\"apps.EnableRegistrySource\":\"true\"}");
            var settings = new AllAppsSettings(settingsPath);
            IWin32ProgramSource origin = pathSource
                ? new PathEnvironmentAppSource(settings)
                : new RegistryAppSource(settings, _ => [("rejected.exe", rejected), ("unchanged.exe", unchanged)]);
            var rejectedReads = 0;
            var unchangedReads = 0;
            var installed = false;
            using var source = new Win32AppSource(
                origin,
                (path, _) =>
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(path, rejected))
                    {
                        Interlocked.Increment(ref rejectedReads);
                        return installed
                            ? TestDataHelper.CreateTestWin32Metadata("Installed", Path.ChangeExtension(path, ".exe"))
                            : new Win32AppMetadata { Valid = false };
                    }

                    Interlocked.Increment(ref unchangedReads);
                    return TestDataHelper.CreateTestWin32Metadata("Unchanged", Path.ChangeExtension(path, ".exe"));
                },
                createWatchers: false);
            var initial = (AppSourceScanResult)await source.LoadAsync(CancellationToken.None);
            Assert.AreEqual(1, initial.Count);
            Assert.AreEqual(rejected, initial.RetryPaths.Single());
            await source.LoadAsync(CancellationToken.None, background: true);
            Assert.AreEqual(1, rejectedReads);

            var retry = (AppSourceScanResult)await source.ApplyChangesAsync(
                initial,
                [new AppSourcePathChange(WatcherChangeTypes.Changed, rejected.ToUpperInvariant())],
                CancellationToken.None,
                background: true);
            Assert.IsTrue(retry.IsFullScan);
            Assert.AreEqual(2, rejectedReads, "A promoted full scan must actually perform the requested retry.");
            Assert.AreEqual(1, unchangedReads, "Unrelated unchanged candidates must remain cached.");
            Assert.AreEqual(rejected, retry.RetryPaths.Single());
            Assert.AreEqual(0, retry.ReusedRejectedPaths.Count);

            installed = true;
            var recovered = (AppSourceScanResult)await source.ApplyChangesAsync(
                retry,
                [new AppSourcePathChange(WatcherChangeTypes.Changed, rejected)],
                CancellationToken.None,
                background: true);
            Assert.AreEqual(3, rejectedReads);
            Assert.AreEqual(1, unchangedReads);
            Assert.AreEqual(2, recovered.Count);
            Assert.AreEqual(0, recovered.RetryPaths.Count);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [DoNotParallelize]
    public async Task Recovery_QuotedPathEntriesRemainDiscoverableAndCacheable(bool includeMalformed)
    {
        var root = CreateTemporaryDirectory("cmdpal-quoted-path");
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var tools = Directory.CreateDirectory(Path.Combine(root, "Tools with spaces")).FullName;
            var executable = Path.Combine(tools, "App.exe");
            File.WriteAllText(executable, "fixture");
            var pathEntries = $"  \"{tools}\"  ;\"\";{Path.Combine(root, "Missing")}";
            if (includeMalformed)
            {
                pathEntries += $";{Path.Combine(root, "Invalid|Name")}";
            }

            Environment.SetEnvironmentVariable("PATH", pathEntries);
            var settingsPath = Path.Combine(root, "settings.json");
            File.WriteAllText(settingsPath, "{\"apps.EnablePathEnvironmentVariableSource\":\"true\"}");
            using var source = new Win32AppSource(
                new PathEnvironmentAppSource(new AllAppsSettings(settingsPath)),
                (path, _) => TestDataHelper.CreateTestWin32Metadata(Path.GetFileNameWithoutExtension(path), path),
                createWatchers: false);

            var scan = (AppSourceScanResult)await source.LoadAsync(CancellationToken.None);
            Assert.IsTrue(scan.IsComplete);
            Assert.AreEqual(executable, ((Win32AppPayload)scan.Single().Payload).TargetPath);
            Assert.AreEqual(0, scan.FailedPaths!.Count);
            Assert.AreEqual(0, scan.RetryPaths.Count);

            var cachePath = Path.Combine(root, "cache.json");
            var cache = new AppCatalogCache(cachePath);
            var context = AppCatalogCacheContext.Create([source], DateTimeOffset.UtcNow);
            await cache.SaveAsync(
                new Dictionary<string, IReadOnlyList<AppCatalogItem>> { [source.Id] = scan },
                [source.Id],
                context,
                CancellationToken.None);
            var cached = await new AppCatalogCache(cachePath).LoadAsync(context, CancellationToken.None);
            Assert.IsNotNull(cached);
            Assert.AreEqual(executable, ((Win32AppPayload)cached.Sources.Single().Items.Single().Payload).TargetPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task Recovery_PathRetriesPreserveOfflineAppsAndDiscoverRecovery()
    {
        var root = CreateTemporaryDirectory("cmdpal-offline-path");
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        var originalFile = Win32FileEnumerator.FileWrapper;
        try
        {
            var local = Directory.CreateDirectory(Path.Combine(root, "Local")).FullName;
            var network = Directory.CreateDirectory(Path.Combine(root, "Network")).FullName;
            var localApp = Path.Combine(local, "Local.exe");
            var networkApp = Path.Combine(network, "Network.exe");
            File.WriteAllText(localApp, "fixture");
            File.WriteAllText(networkApp, "fixture");
            Environment.SetEnvironmentVariable("PATH", $"{local};{network}");
            var offline = false;
            var file = new Mock<IFile>();
            file.Setup(value => value.GetAttributes(It.IsAny<string>()))
                .Returns((string path) => offline && path == network
                    ? throw new IOException("Network share unavailable", unchecked((int)0x80070040))
                    : File.GetAttributes(path));
            Win32FileEnumerator.FileWrapper = file.Object;
            var settingsPath = Path.Combine(root, "settings.json");
            File.WriteAllText(settingsPath, "{\"apps.EnablePathEnvironmentVariableSource\":\"true\"}");
            using var source = new Win32AppSource(
                new PathEnvironmentAppSource(new AllAppsSettings(settingsPath)),
                (path, _) => TestDataHelper.CreateTestWin32Metadata(Path.GetFileNameWithoutExtension(path), path),
                createWatchers: false);
            var initial = await source.LoadAsync(CancellationToken.None);
            var networkItem = initial.Single(item => ((Win32AppPayload)item.Payload).TargetPath == networkApp);
            offline = true;

            var scan = (AppSourceScanResult)await source.LoadAsync(CancellationToken.None, background: true);
            Assert.IsFalse(scan.IsComplete);
            Assert.IsTrue(scan.IsFullScan);
            Assert.AreEqual(localApp, ((Win32AppPayload)scan.Single().Payload).TargetPath);
            CollectionAssert.AreEqual(new[] { network }, scan.FailedPaths!.ToArray());
            Assert.AreEqual(0, scan.RetryPaths.Count, "Offline shares should wait for periodic or manual reconciliation.");
            Assert.IsNotNull(scan.GetRetainedItem(networkItem));

            var retry = (AppSourceScanResult)await source.ApplyChangesAsync(
                [.. scan, networkItem],
                [new AppSourcePathChange(WatcherChangeTypes.Changed, network)],
                CancellationToken.None,
                background: true);
            Assert.IsFalse(retry.IsComplete);
            Assert.IsTrue(retry.IsFullScan);
            Assert.IsNotNull(retry.GetRetainedItem(networkItem), "A queued retry must not discard the offline application's last known row.");
            Assert.AreEqual(0, retry.RetryPaths.Count);

            offline = false;
            var recovered = (AppSourceScanResult)await source.ApplyChangesAsync(
                [.. retry, networkItem],
                [new AppSourcePathChange(WatcherChangeTypes.Changed, network)],
                CancellationToken.None,
                background: true);
            Assert.IsTrue(recovered.IsComplete);
            Assert.AreEqual(2, recovered.Count);
            Assert.IsTrue(recovered.Any(item => ((Win32AppPayload)item.Payload).TargetPath == networkApp));

            File.Delete(networkApp);
            var removed = (AppSourceScanResult)await source.ApplyChangesAsync(
                recovered,
                [new AppSourcePathChange(WatcherChangeTypes.Changed, network)],
                CancellationToken.None,
                background: true);
            Assert.IsTrue(removed.IsComplete);
            Assert.AreEqual(localApp, ((Win32AppPayload)removed.Single().Payload).TargetPath);
            Assert.IsNull(removed.GetRetainedItem(networkItem));
        }
        finally
        {
            Win32FileEnumerator.FileWrapper = originalFile;
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [DoNotParallelize]
    public async Task Recovery_UnreadableFolderOnFirstLoadStillPublishesReadableSiblings(bool pathSource)
    {
        var root = CreateTemporaryDirectory("cmdpal-scoped-folder");
        var originalDirectory = Win32FileEnumerator.DirectoryWrapper;
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var readable = Directory.CreateDirectory(Path.Combine(root, "Readable")).FullName;
            var blocked = Directory.CreateDirectory(Path.Combine(root, "Blocked")).FullName;
            var extension = pathSource ? "exe" : "lnk";
            File.WriteAllText(Path.Combine(readable, $"App.{extension}"), "fixture");
            File.WriteAllText(Path.Combine(blocked, $"Blocked.{extension}"), "fixture");
            var directory = new Mock<IDirectory>();
            directory.Setup(value => value.EnumerateFiles(It.IsAny<string>(), It.IsAny<string>(), SearchOption.TopDirectoryOnly))
                .Returns((string path, string pattern, SearchOption options) => path == blocked
                    ? throw new UnauthorizedAccessException("Fixture access denial")
                    : Directory.EnumerateFiles(path, pattern, options));
            directory.Setup(value => value.EnumerateDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EnumerationOptions>()))
                .Returns((string path, string pattern, EnumerationOptions options) => path == blocked
                    ? throw new UnauthorizedAccessException("Fixture access denial")
                    : Directory.EnumerateDirectories(path, pattern, options));
            Win32FileEnumerator.DirectoryWrapper = directory.Object;
            Environment.SetEnvironmentVariable("PATH", $"{readable};{blocked}");
            var settingsPath = Path.Combine(root, "settings.json");
            File.WriteAllText(settingsPath, "{\"apps.EnablePathEnvironmentVariableSource\":\"true\"}");
            IWin32ProgramSource origin = pathSource
                ? new PathEnvironmentAppSource(new AllAppsSettings(settingsPath))
                : CreateShortcutDirectorySource(root);
            Assert.IsTrue(origin.IsEnabled);
            using var source = new Win32AppSource(
                origin,
                (path, _) => TestDataHelper.CreateTestWin32Metadata(Path.GetFileNameWithoutExtension(path), Path.ChangeExtension(path, ".exe")),
                createWatchers: false);

            var scan = (AppSourceScanResult)await source.LoadAsync(CancellationToken.None);
            Assert.IsFalse(scan.IsComplete);
            Assert.AreEqual("App", ((Win32AppPayload)scan.Single().Payload).Name);
            CollectionAssert.AreEqual(new[] { blocked }, scan.FailedPaths!.ToArray());
            Assert.AreEqual(0, scan.RetryPaths.Count, "Access denial is not a short-lived read failure.");
        }
        finally
        {
            Win32FileEnumerator.DirectoryWrapper = originalDirectory;
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Recovery_UnreadableCandidateDoesNotRestoreDeletedOrRetargetedShortcuts(bool incremental)
    {
        var root = CreateTemporaryDirectory("cmdpal-scoped-candidates");
        try
        {
            var locked = Path.Combine(root, "Locked.lnk");
            var deleted = Path.Combine(root, "Deleted.lnk");
            var retargeted = Path.Combine(root, "Retargeted.lnk");
            foreach (var path in new[] { locked, deleted, retargeted })
            {
                File.WriteAllText(path, "fixture");
            }

            var failRead = false;
            var target = Path.Combine(root, "Old.exe");
            using var source = new Win32AppSource(
                CreateShortcutDirectorySource(root),
                (path, _) =>
                {
                    if (failRead && path == locked)
                    {
                        return new Win32AppMetadata { Valid = false, Unreadable = true, RetryableReadFailure = true };
                    }

                    var program = TestDataHelper.CreateTestWin32Metadata(Path.GetFileNameWithoutExtension(path), path == retargeted ? target : Path.ChangeExtension(path, ".exe"));
                    program.LnkFilePath = path;
                    return program;
                },
                createWatchers: false);
            var initial = await source.LoadAsync(CancellationToken.None);
            File.Delete(deleted);
            target = Path.Combine(root, "New.exe");
            failRead = true;
            var scan = (AppSourceScanResult)(incremental
                ? await source.ApplyChangesAsync(initial, [new AppSourcePathChange(WatcherChangeTypes.Changed, root)], CancellationToken.None)
                : await source.LoadAsync(CancellationToken.None));
            Assert.IsFalse(scan.IsComplete);
            string[] expected = ["Locked", "Retargeted"];
            CollectionAssert.AreEquivalent(expected, scan.Select(item => ((Win32AppPayload)item.Payload).Name).ToArray());
            Assert.AreEqual(target, ((Win32AppPayload)scan.Single(item => ((Win32AppPayload)item.Payload).Name == "Retargeted").Payload).TargetPath);
            CollectionAssert.AreEqual(new[] { locked }, scan.FailedPaths!.ToArray());
            CollectionAssert.AreEqual(new[] { locked }, scan.RetryPaths.ToArray());
            Assert.IsTrue(scan.CheckedPaths.Contains(retargeted));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(WatcherChangeTypes.Deleted, false)]
    [DataRow(WatcherChangeTypes.Deleted, true)]
    [DataRow(WatcherChangeTypes.Renamed, false)]
    [DoNotParallelize]
    public async Task Recovery_ConfirmedRemovalOverridesUnreadableParent(WatcherChangeTypes kind, bool directoryRemoved)
    {
        var root = CreateTemporaryDirectory("cmdpal-confirmed-removal");
        var originalDirectory = Win32FileEnumerator.DirectoryWrapper;
        try
        {
            var restricted = Directory.CreateDirectory(Path.Combine(root, "Restricted")).FullName;
            var removedPath = directoryRemoved
                ? Directory.CreateDirectory(Path.Combine(restricted, "Removed")).FullName
                : Path.Combine(restricted, "Removed.lnk");
            var removedShortcut = directoryRemoved ? Path.Combine(removedPath, "Removed.lnk") : removedPath;
            File.WriteAllText(removedShortcut, "fixture");
            File.WriteAllText(Path.Combine(restricted, "Unknown.lnk"), "fixture");
            using var source = new Win32AppSource(
                CreateShortcutDirectorySource(root),
                (path, _) =>
                {
                    var program = TestDataHelper.CreateTestWin32Metadata(Path.GetFileNameWithoutExtension(path), Path.ChangeExtension(path, ".exe"));
                    program.LnkFilePath = path;
                    return program;
                },
                createWatchers: false);
            var initial = await source.LoadAsync(CancellationToken.None);
            var removed = initial.Single(item => ((Win32AppPayload)item.Payload).Name == "Removed");
            var unknown = initial.Single(item => ((Win32AppPayload)item.Payload).Name == "Unknown");
            var renamedPath = Path.Combine(root, "Renamed.lnk");
            if (directoryRemoved)
            {
                Directory.Delete(removedPath, recursive: true);
            }
            else if (kind == WatcherChangeTypes.Renamed)
            {
                File.Move(removedPath, renamedPath);
            }
            else
            {
                File.Delete(removedPath);
            }

            var directory = new Mock<IDirectory>();
            directory.Setup(value => value.EnumerateFiles(It.IsAny<string>(), It.IsAny<string>(), SearchOption.TopDirectoryOnly))
                .Returns((string path, string pattern, SearchOption options) => path == restricted
                    ? throw new UnauthorizedAccessException("Fixture access denial")
                    : Directory.EnumerateFiles(path, pattern, options));
            directory.Setup(value => value.EnumerateDirectories(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EnumerationOptions>()))
                .Returns((string path, string pattern, EnumerationOptions options) => Directory.EnumerateDirectories(path, pattern, options));
            Win32FileEnumerator.DirectoryWrapper = directory.Object;
            var removal = kind == WatcherChangeTypes.Renamed
                ? new AppSourcePathChange(kind, renamedPath, removedPath)
                : new AppSourcePathChange(kind, removedPath);
            var scan = (AppSourceScanResult)await source.ApplyChangesAsync(
                initial,
                [new AppSourcePathChange(WatcherChangeTypes.Changed, restricted), removal],
                CancellationToken.None);

            Assert.IsFalse(scan.IsComplete);
            CollectionAssert.AreEqual(new[] { restricted }, scan.FailedPaths!.ToArray());
            Assert.AreEqual(0, scan.RetryPaths.Count);
            Assert.IsNull(scan.GetRetainedItem(removed), "An enclosing read failure must not restore a confirmed removal.");
            Assert.IsNotNull(scan.GetRetainedItem(unknown), "Uncertainty must still preserve unreadable representations.");
            Assert.AreEqual(kind == WatcherChangeTypes.Renamed ? 1 : 0, scan.Count);
        }
        finally
        {
            Win32FileEnumerator.DirectoryWrapper = originalDirectory;
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public void Recovery_DirectoryEnumerationHonorsCancellationBetweenFiles()
    {
        var root = CreateTemporaryDirectory("cmdpal-folder-cancellation");
        var originalDirectory = Win32FileEnumerator.DirectoryWrapper;
        using var cancellation = new CancellationTokenSource();
        try
        {
            var directory = new Mock<IDirectory>();
            directory.Setup(value => value.EnumerateFiles(It.IsAny<string>(), It.IsAny<string>(), SearchOption.TopDirectoryOnly))
                .Returns(() => EnumerateThenCancel(root, cancellation));
            Win32FileEnumerator.DirectoryWrapper = directory.Object;
            Assert.ThrowsExactly<OperationCanceledException>(() => _ = Win32FileEnumerator.EnumerateFiles(root, ["lnk"], cancellationToken: cancellation.Token).ToArray());
        }
        finally
        {
            Win32FileEnumerator.DirectoryWrapper = originalDirectory;
            Directory.Delete(root, recursive: true);
        }
    }

    private static IEnumerable<string> EnumerateThenCancel(string root, CancellationTokenSource cancellation)
    {
        yield return Path.Combine(root, "First.lnk");
        cancellation.Cancel();
        yield return Path.Combine(root, "Second.lnk");
    }
}
