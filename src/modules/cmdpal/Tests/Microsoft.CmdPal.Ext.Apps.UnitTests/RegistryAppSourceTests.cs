// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps.AppList;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Catalog.Cache;
using Microsoft.CmdPal.Ext.Apps.Catalog.Payloads;
using Microsoft.CmdPal.Ext.Apps.Catalog.Sources;
using Microsoft.CmdPal.Ext.Apps.Commands;
using Microsoft.CmdPal.Ext.Apps.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class RegistryAppSourceTests
{
    [TestMethod]
    [DataRow("foo")]
    [DataRow("foo.exe")]
    [DataRow("FOO.EXE")]
    public async Task LoadAsync_CommandNameSurvivesPreferredShortcutMerge(string query)
    {
        using var fixture = new RegistryFixture();
        var registry = fixture.CreateSource(("foo.exe", @"C:\Tools\bar.exe"));
        using var source = new Win32AppSource(registry, LoadProgram, createWatchers: false);
        var registryItem = (await source.LoadAsync(CancellationToken.None)).Single();
        var shortcut = LoadProgram(@"C:\Tools\bar.exe", false);
        shortcut.Name = "Editor";
        shortcut.LnkFilePath = @"C:\Start Menu\Editor.lnk";
        var shortcutItem = new AppCatalogItem(
            registryItem.Identity,
            10,
            new AppCatalogSourceReference("start-menu", shortcut.LnkFilePath),
            [],
            Win32AppPayload.From(shortcut));
        var merged = shortcutItem.MergeProvenance(registryItem);
        var row = new AppListItem(merged.ToAppItem());
        var match = new AppSearch(query, new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.FilenameAndStem).Evaluate(row);

        Assert.AreEqual("Editor", row.Title);
        Assert.AreEqual(shortcut.LnkFilePath, row.App.LaunchTarget);
        Assert.AreEqual(shortcut.TargetPath, row.App.ResolvedTarget);
        Assert.AreEqual(new AppCommand(shortcutItem.ToAppItem()).Id, row.Command!.Id);
        Assert.IsTrue(match.HasMatch);
        Assert.IsTrue(match.IsExactMetadataMatch);
        Assert.IsFalse(match.IsExactExecutableMatch, "Registered command names are search metadata, not executable-priority targets.");
    }

    [TestMethod]
    public void GetCandidates_CombinesAllCommandNamesForSameTarget()
    {
        using var fixture = new RegistryFixture();
        var source = fixture.CreateSource(
            ("foo.exe", @"C:\Tools\bar.exe"),
            ("FOO.EXE", @"c:\tools\BAR.exe"),
            ("editor.exe", @"C:\Tools\bar.exe"));

        string[] expectedTerms = ["foo.exe", "foo", "editor.exe", "editor"];
        var candidate = source.GetCandidates().Single();
        Assert.AreEqual(@"C:\Tools\bar.exe", candidate.Path);
        CollectionAssert.AreEquivalent(expectedTerms, candidate.MatchTerms.ToArray());
        string[] expectedPaths = [candidate.Path];
        CollectionAssert.AreEqual(expectedPaths, source.GetPaths().ToArray());
    }

    [TestMethod]
    public void GetCandidates_LaterScanDoesNotChangeEarlierCandidates()
    {
        using var fixture = new RegistryFixture();
        (string CommandName, string TargetPath)[] programs = [("old.exe", @"C:\Tools\bar.exe")];
        var source = new RegistryAppSource(fixture.Settings, _ => programs);
        var original = source.GetCandidates().Single();
        programs = [("new.exe", @"C:\Tools\bar.exe")];

        var updated = source.GetCandidates().Single();

        string[] originalTerms = ["old.exe", "old"];
        string[] expectedTerms = ["new.exe", "new"];
        CollectionAssert.AreEquivalent(originalTerms, original.MatchTerms.ToArray());
        CollectionAssert.AreEquivalent(expectedTerms, updated.MatchTerms.ToArray());
        programs = [];
        Assert.AreEqual(0, source.GetCandidates().Count());
        CollectionAssert.AreEquivalent(originalTerms, original.MatchTerms.ToArray());
        CollectionAssert.AreEquivalent(expectedTerms, updated.MatchTerms.ToArray());
    }

    [TestMethod]
    public async Task LoadAsync_AnotherScanDuringIndexingDoesNotReplaceCandidateTerms()
    {
        using var fixture = new RegistryFixture();
        (string CommandName, string TargetPath)[] programs = [("old.exe", @"C:\Tools\bar.exe")];
        var registry = new RegistryAppSource(fixture.Settings, _ => programs);
        using var source = new Win32AppSource(
            registry,
            (path, asRunCommand) =>
            {
                programs = [("new.exe", path)];
                var nextScan = registry.GetCandidates().Single();
                CollectionAssert.Contains(nextScan.MatchTerms.ToArray(), "new.exe");
                return LoadProgram(path, asRunCommand);
            },
            createWatchers: false);

        var item = (await source.LoadAsync(CancellationToken.None)).Single();

        CollectionAssert.Contains(item.MatchTerms.ToArray(), "old.exe");
        CollectionAssert.Contains(item.MatchTerms.ToArray(), "old");
        Assert.IsFalse(item.MatchTerms.Contains("new.exe", StringComparer.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task LoadAsync_DisabledRegistrySourceDoesNotEnumerate()
    {
        using var fixture = new RegistryFixture(enabled: false);
        var registry = new RegistryAppSource(fixture.Settings, _ => throw new AssertFailedException("Disabled source was enumerated."));
        using var source = new Win32AppSource(registry, LoadProgram, createWatchers: false);

        Assert.AreEqual(0, (await source.LoadAsync(CancellationToken.None)).Count);
    }

    [TestMethod]
    public async Task LoadAsync_DuplicateCandidatePathsKeepAllTermsAndLoadOnce()
    {
        const string firstPath = @"C:\Tools\bar.exe";
        var loadCount = 0;
        using var source = new Win32AppSource(
            new CandidateSource(
                new Win32ProgramCandidate(firstPath, ["foo"]),
                new Win32ProgramCandidate(@"c:\tools\BAR.exe", ["editor"])),
            (path, asRunCommand) =>
            {
                Interlocked.Increment(ref loadCount);
                Assert.AreEqual(firstPath, path);
                return LoadProgram(path, asRunCommand);
            },
            createWatchers: false);

        var item = (await source.LoadAsync(CancellationToken.None)).Single();

        Assert.AreEqual(1, loadCount);
        CollectionAssert.Contains(item.MatchTerms.ToArray(), "foo");
        CollectionAssert.Contains(item.MatchTerms.ToArray(), "editor");
        Assert.AreEqual(firstPath, item.Provenance.References.Single().ItemId);
    }

    [TestMethod]
    public async Task SaveAndLoadAsync_CommandNamesRemainSearchable()
    {
        using var fixture = new RegistryFixture();
        using var source = new Win32AppSource(fixture.CreateSource(("foo.exe", @"C:\Tools\bar.exe")), LoadProgram, createWatchers: false);
        var items = await source.LoadAsync(CancellationToken.None);
        var cache = new AppCatalogCache(Path.Combine(fixture.Root, "catalog.json"));
        var context = new AppCatalogCacheContext
        {
            Language = "en-US",
            SourceKeys = new Dictionary<string, string> { [source.Id] = source.CacheKey },
            NowUtc = DateTimeOffset.UtcNow,
        };
        await cache.SaveAsync(
            new Dictionary<string, IReadOnlyList<AppCatalogItem>> { [source.Id] = items },
            [source.Id],
            context,
            CancellationToken.None);

        var loaded = await cache.LoadAsync(context, CancellationToken.None);

        Assert.IsNotNull(loaded);
        var cachedItem = loaded.Sources.Single().Items.Single();
        Assert.IsTrue(items.Single().HasSamePersistedContent(cachedItem));
        var row = new AppListItem(cachedItem.ToAppItem());
        Assert.IsTrue(new AppSearch("foo", new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.Disabled).Evaluate(row).IsExactMetadataMatch);
    }

    [TestMethod]
    public async Task ApplyChangesAsync_RetryReenumeratesRegistryTargetsAndCommandNames()
    {
        using var fixture = new RegistryFixture();
        const string path = @"C:\Tools\bar.exe";
        (string CommandName, string TargetPath)[] programs = [("old.exe", path)];
        using var source = new Win32AppSource(new RegistryAppSource(fixture.Settings, _ => programs), LoadProgram, createWatchers: false);
        var initial = await source.LoadAsync(CancellationToken.None);
        programs = [("new.exe", path)];

        var updated = (AppSourceScanResult)await source.ApplyChangesAsync(
            initial,
            [new AppSourcePathChange(WatcherChangeTypes.Changed, path)],
            CancellationToken.None,
            background: true);
        Assert.IsTrue(updated.IsComplete);
        Assert.IsTrue(updated.IsFullScan);
        var item = updated.Single();
        CollectionAssert.Contains(item.MatchTerms.ToArray(), "new.exe");
        Assert.IsFalse(item.MatchTerms.Contains("old.exe", StringComparer.OrdinalIgnoreCase));

        programs = [];
        var removed = await source.ApplyChangesAsync(
            updated,
            [new AppSourcePathChange(WatcherChangeTypes.Changed, path)],
            CancellationToken.None,
            background: true);
        Assert.AreEqual(0, removed.Count);
    }

    private static Win32AppMetadata LoadProgram(string path, bool asRunCommand)
    {
        var program = TestDataHelper.CreateTestWin32Metadata("Bar", path);
        program.SourceFilename = Path.GetFileName(path);
        program.ParentDirectory = Path.GetDirectoryName(path)!;
        return program;
    }

    private sealed class CandidateSource : IWin32ProgramSource
    {
        private readonly Win32ProgramCandidate[] _candidates;

        public string Id => "test-candidates";

        public int Priority => 0;

        public bool IsEnabled => true;

        public Win32ProgramSourceProfile Profile => Win32ProgramSourceProfile.IncludeRawExecutables;

        public string CacheKey => Id;

        public string ConfigurationKey => CacheKey;

        public IReadOnlyList<string> WatchPaths => [];

        public CandidateSource(params Win32ProgramCandidate[] candidates)
        {
            _candidates = candidates;
        }

        public IEnumerable<string> GetPaths()
        {
            throw new AssertFailedException("Candidate indexing must use the returned paths and terms together.");
        }

        public IEnumerable<Win32ProgramCandidate> GetCandidates(Action<string, Exception> onError = null, CancellationToken cancellationToken = default)
        {
            return _candidates;
        }

        public bool IsRelevantPath(string path)
        {
            return false;
        }
    }

    private sealed class RegistryFixture : IDisposable
    {
        public string Root { get; }

        public AllAppsSettings Settings { get; }

        public RegistryFixture(bool enabled = true)
        {
            Root = Path.Combine(Path.GetTempPath(), $"cmdpal-registry-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            var settingsPath = Path.Combine(Root, "settings.json");
            File.WriteAllText(settingsPath, enabled ? "{\"apps.EnableRegistrySource\":\"true\"}" : "{}");
            Settings = new AllAppsSettings(settingsPath);
            Assert.AreEqual(enabled, Settings.EnableRegistrySource);
        }

        public RegistryAppSource CreateSource(params (string CommandName, string TargetPath)[] programs)
        {
            return new RegistryAppSource(Settings, _ => programs);
        }

        public void Dispose()
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
