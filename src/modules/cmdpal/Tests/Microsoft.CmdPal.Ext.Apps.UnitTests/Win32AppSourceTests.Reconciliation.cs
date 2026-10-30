// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Catalog.Sources;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

public partial class Win32AppSourceTests
{
    [TestMethod]
    [DataRow(500)]
    [DataRow(1000)]
    public async Task BackgroundLoad_UnchangedCatalogDoesNotReparseCandidates(int count)
    {
        var root = CreateTemporaryDirectory("cmdpal-metadata-probe");
        try
        {
            var paths = Enumerable.Range(0, count).Select(index => Path.Combine(root, $"App{index}.exe")).ToArray();
            foreach (var path in paths)
            {
                File.WriteAllText(path, "fixture");
            }

            var loads = 0;
            var origin = new TestProgramSource("probe", 0, Win32ProgramSourceProfile.IncludeRawExecutables, paths);
            using var source = new Win32AppSource(
                origin,
                (path, _) =>
                {
                    Interlocked.Increment(ref loads);
                    return TestDataHelper.CreateTestWin32Metadata(Path.GetFileNameWithoutExtension(path), path);
                },
                createWatchers: false);
            var initial = await source.LoadAsync(CancellationToken.None);
            Assert.AreEqual(count, loads);

            var elapsed = Stopwatch.StartNew();
            var unchanged = await source.LoadAsync(CancellationToken.None, background: true);
            elapsed.Stop();
            Console.WriteLine($"Metadata check for {count} files: {elapsed.Elapsed.TotalMilliseconds:F1} ms, {loads - count} parser calls.");
            Assert.AreEqual(count, loads, "Unchanged files must not be opened for metadata extraction.");
            Assert.IsTrue(AppCatalogItem.HaveSamePersistedContent(initial, unchanged));

            File.AppendAllText(paths[0], "changed");
            await source.LoadAsync(CancellationToken.None, background: true);
            Assert.AreEqual(count + 1, loads, "Only the changed executable should be reparsed.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task BackgroundLoad_DetectsTargetChangesWithoutShortcutChanges()
    {
        var root = CreateTemporaryDirectory("cmdpal-target-probe");
        var shortcut = Path.Combine(root, "Portable.lnk");
        var target = Path.Combine(root, "Portable.exe");
        try
        {
            File.WriteAllText(shortcut, "shortcut fixture");
            File.WriteAllText(target, "Before");
            var loads = 0;
            using var source = new Win32AppSource(
                CreateShortcutDirectorySource(root),
                (_, _) =>
                {
                    Interlocked.Increment(ref loads);
                    return File.Exists(target)
                        ? TestDataHelper.CreateTestWin32Metadata(File.ReadAllText(target), target)
                        : new Win32AppMetadata { Valid = false, TargetPath = target };
                },
                createWatchers: false);
            await source.LoadAsync(CancellationToken.None);
            var shortcutStamp = File.GetLastWriteTimeUtc(shortcut);

            File.WriteAllText(target, "After replacement");
            var updated = await source.LoadAsync(CancellationToken.None, background: true);
            Assert.AreEqual("After replacement", ((Win32AppPayload)updated.Single().Payload).Name);
            Assert.AreEqual(2, loads);
            Assert.AreEqual(shortcutStamp, File.GetLastWriteTimeUtc(shortcut));

            File.Delete(target);
            var missing = (AppSourceScanResult)await source.LoadAsync(CancellationToken.None, background: true);
            Assert.AreEqual(0, missing.Count);
            CollectionAssert.AreEqual(new[] { shortcut }, missing.RetryPaths.ToArray());

            File.WriteAllText(target, "Reinstalled");
            var restored = await source.LoadAsync(CancellationToken.None, background: true);
            Assert.AreEqual("Reinstalled", ((Win32AppPayload)restored.Single().Payload).Name);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task BackgroundLoad_ReusesRejectedCandidatesButHonorsExplicitReadsAndChangedTargets(bool knownTarget)
    {
        var root = CreateTemporaryDirectory("cmdpal-rejected-probe");
        var shortcut = Path.Combine(root, "App.lnk");
        var target = Path.Combine(root, "App.exe");
        try
        {
            File.WriteAllText(shortcut, "shortcut fixture");
            var loads = 0;
            using var source = new Win32AppSource(
                CreateShortcutDirectorySource(root),
                (_, _) =>
                {
                    Interlocked.Increment(ref loads);
                    return File.Exists(target)
                        ? TestDataHelper.CreateTestWin32Metadata("Installed", target)
                        : new Win32AppMetadata { Valid = false, TargetPath = knownTarget ? target : string.Empty };
                },
                createWatchers: false);
            var initial = (AppSourceScanResult)await source.LoadAsync(CancellationToken.None);
            Assert.AreEqual(1, initial.RetryPaths.Count);

            var unchanged = (AppSourceScanResult)await source.LoadAsync(CancellationToken.None, background: true);
            Assert.AreEqual(1, loads, "Periodic reconciliation must reuse an unchanged rejection.");
            Assert.AreEqual(0, unchanged.Count);
            Assert.AreEqual(0, unchanged.RetryPaths.Count, "An unchanged rejection must not renew its retry budget.");
            CollectionAssert.AreEqual(new[] { shortcut }, unchanged.ReusedRejectedPaths.ToArray());

            await source.LoadAsync(CancellationToken.None);
            Assert.AreEqual(2, loads, "Manual refresh must re-read a rejected shortcut.");
            await source.ApplyChangesAsync(
                initial,
                [new AppSourcePathChange(WatcherChangeTypes.Changed, shortcut)],
                CancellationToken.None,
                background: true);
            Assert.AreEqual(3, loads, "Explicit recovery must re-read a rejected shortcut.");

            File.WriteAllText(target, "Installed");
            if (!knownTarget)
            {
                File.AppendAllText(shortcut, "changed");
            }

            var restored = await source.LoadAsync(CancellationToken.None, background: true);
            Assert.AreEqual(4, loads);
            Assert.AreEqual("Installed", ((Win32AppPayload)restored.Single().Payload).Name);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task BackgroundLoad_ReusesExcludedNonApplications()
    {
        var root = CreateTemporaryDirectory("cmdpal-nonapp-probe");
        var shortcut = Path.Combine(root, "Document.lnk");
        var target = Path.Combine(root, "Document.txt");
        try
        {
            File.WriteAllText(shortcut, "shortcut fixture");
            File.WriteAllText(target, "document");
            var loads = 0;
            using var source = new Win32AppSource(
                new TestProgramSource("probe", 0, includeNonApps: false, shortcut),
                (_, _) =>
                {
                    Interlocked.Increment(ref loads);
                    var program = TestDataHelper.CreateTestWin32Metadata("Document", target);
                    program.AppType = Win32AppType.GenericFile;
                    return program;
                },
                createWatchers: false);
            Assert.AreEqual(0, (await source.LoadAsync(CancellationToken.None)).Count);
            Assert.AreEqual(0, (await source.LoadAsync(CancellationToken.None, background: true)).Count);
            Assert.AreEqual(1, loads);

            File.AppendAllText(target, "changed");
            Assert.AreEqual(0, (await source.LoadAsync(CancellationToken.None, background: true)).Count);
            Assert.AreEqual(2, loads);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task BackgroundLoad_DoesNotCacheUnreadableCandidates(bool retryable)
    {
        var root = CreateTemporaryDirectory("cmdpal-unreadable-probe");
        try
        {
            var shortcut = Path.Combine(root, "App.lnk");
            File.WriteAllText(shortcut, "shortcut fixture");
            var loads = 0;
            using var source = new Win32AppSource(
                CreateShortcutDirectorySource(root),
                (_, _) =>
                {
                    Interlocked.Increment(ref loads);
                    return new Win32AppMetadata { Valid = false, Unreadable = true, RetryableReadFailure = retryable };
                },
                createWatchers: false);
            await source.LoadAsync(CancellationToken.None);
            var unchanged = (AppSourceScanResult)await source.LoadAsync(CancellationToken.None, background: true);
            Assert.AreEqual(2, loads);
            Assert.IsFalse(unchanged.IsComplete);
            Assert.AreEqual(retryable ? 1 : 0, unchanged.RetryPaths.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task BackgroundLoad_ReconcilesNewAndDeletedNestedCandidates()
    {
        var root = CreateTemporaryDirectory("cmdpal-nested-probe");
        try
        {
            var nested = Directory.CreateDirectory(Path.Combine(root, "Nested")).FullName;
            var first = Path.Combine(nested, "First.exe");
            File.WriteAllText(first, "first");
            var loads = 0;
            var origin = new CustomDirectoryAppSource(
                "probe",
                root,
                ["exe"],
                Win32ProgramSourceProfile.IncludeRawExecutables | Win32ProgramSourceProfile.RecurseSubdirectories,
                int.MaxValue);
            using var source = new Win32AppSource(
                origin,
                (path, _) =>
                {
                    Interlocked.Increment(ref loads);
                    return TestDataHelper.CreateTestWin32Metadata(Path.GetFileNameWithoutExtension(path), path);
                },
                createWatchers: false);
            await source.LoadAsync(CancellationToken.None);

            var second = Path.Combine(nested, "Second.exe");
            File.WriteAllText(second, "second");
            var added = await source.LoadAsync(CancellationToken.None, background: true);
            Assert.AreEqual(2, added.Count);
            Assert.AreEqual(2, loads);

            File.Delete(first);
            var removed = await source.LoadAsync(CancellationToken.None, background: true);
            Assert.AreEqual("Second", ((Win32AppPayload)removed.Single().Payload).Name);
            Assert.AreEqual(2, loads, "Removing a path must not reparse unchanged candidates.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task BackgroundLoad_DetectsChangedCommandNamesForAnUnchangedTarget()
    {
        var root = CreateTemporaryDirectory("cmdpal-command-term-probe");
        var path = Path.Combine(root, "app.exe");
        try
        {
            File.WriteAllText(path, "fixture");
            var loads = 0;
            var origin = new TermProgramSource(path) { Terms = ["old-command.exe"] };
            using var source = new Win32AppSource(
                origin,
                (candidate, _) =>
                {
                    Interlocked.Increment(ref loads);
                    return TestDataHelper.CreateTestWin32Metadata("App", candidate);
                },
                createWatchers: false);
            await source.LoadAsync(CancellationToken.None);

            origin.Terms = ["new-command.exe"];
            var changed = await source.LoadAsync(CancellationToken.None, background: true);
            Assert.AreEqual(2, loads);
            CollectionAssert.Contains(changed.Single().MatchTerms.ToArray(), "new-command.exe");
            CollectionAssert.DoesNotContain(changed.Single().MatchTerms.ToArray(), "old-command.exe");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task BackgroundLoad_RechecksExecutionAliasOwnershipWithoutAStampChange()
    {
        var root = CreateTemporaryDirectory("cmdpal-execution-alias-probe");
        var path = Path.Combine(root, "wt.exe");
        var target = Path.Combine(root, "terminal.exe");
        try
        {
            File.WriteAllText(path, "alias fixture");
            File.WriteAllText(target, "target fixture");
            var selected = "Terminal";
            var loads = 0;
            var origin = new TestProgramSource("alias", 0, Win32ProgramSourceProfile.IncludeRawExecutables, path);
            using var source = new Win32AppSource(
                origin,
                (_, _) =>
                {
                    Interlocked.Increment(ref loads);
                    var program = TestDataHelper.CreateTestWin32Metadata(selected, path);
                    program.AppExecutionAlias = new ReparsePoint.AppExecutionAliasInfo
                    {
                        Aumid = $"Contoso.{selected}_123!App",
                        TargetPath = target,
                    };
                    return program;
                },
                createWatchers: false);
            await source.LoadAsync(CancellationToken.None);

            selected = "Preview";
            var changed = await source.LoadAsync(CancellationToken.None, background: true);
            Assert.AreEqual(2, loads);
            Assert.AreEqual("Contoso.Preview_123!App", ((Win32AppPayload)changed.Single().Payload).PackagedAppUserModelId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task BackgroundRetry_ReadsAnExplicitDirtyPathEvenWhenItsStampMatches()
    {
        var root = CreateTemporaryDirectory("cmdpal-forced-retry");
        var shortcut = Path.Combine(root, "App.lnk");
        try
        {
            File.WriteAllText(shortcut, "fixture");
            var loads = 0;
            using var source = new Win32AppSource(
                CreateShortcutDirectorySource(root),
                (_, _) =>
                {
                    Interlocked.Increment(ref loads);
                    return TestDataHelper.CreateTestWin32Metadata("App", Path.Combine(root, "App.exe"));
                },
                createWatchers: false);
            var initial = await source.LoadAsync(CancellationToken.None);

            await source.ApplyChangesAsync(
                initial,
                [new AppSourcePathChange(WatcherChangeTypes.Changed, shortcut)],
                CancellationToken.None,
                background: true);
            Assert.AreEqual(2, loads);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TermProgramSource : IWin32ProgramSource
    {
        private readonly string _path;

        public string Id => "terms";

        public int Priority => 0;

        public bool IsEnabled => true;

        public Win32ProgramSourceProfile Profile => Win32ProgramSourceProfile.IncludeRawExecutables;

        public string CacheKey => Id;

        public string ConfigurationKey => Id;

        public IReadOnlyList<string> WatchPaths => [];

        public IReadOnlyList<string> Terms { get; set; } = [];

        public TermProgramSource(string path)
        {
            _path = path;
        }

        public IEnumerable<string> GetPaths()
        {
            yield return _path;
        }

        public IEnumerable<Win32ProgramCandidate> GetCandidates(Action<string, Exception>? onError = null, CancellationToken cancellationToken = default)
        {
            yield return new Win32ProgramCandidate(_path, Terms);
        }

        public bool IsRelevantPath(string path)
        {
            return false;
        }
    }
}
