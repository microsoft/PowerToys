// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Catalog.Sources;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class Win32AppSourceTests
{
    [TestMethod]
    public async Task LoadAsync_IndexesOneOriginWithSourceScopedProvenance()
    {
        var startMenu = new TestProgramSource("start-menu", 10, includeNonApps: true, @"C:\Apps\app.lnk");
        var loadCount = 0;
        using var source = new Win32AppSource(
            startMenu,
            (path, asRunCommand) =>
            {
                Interlocked.Increment(ref loadCount);
                return TestDataHelper.CreateTestWin32Program("App", @"C:\Apps\app.exe");
            },
            createWatchers: false);

        var items = await source.LoadAsync(CancellationToken.None);

        Assert.AreEqual(1, loadCount);
        Assert.AreEqual(1, items.Count);
        Assert.AreEqual(10, items[0].Provenance.Priority);
        Assert.IsTrue(ContainsSourceReference(items[0], "start-menu", @"C:\Apps\app.lnk"));
    }

    [TestMethod]
    public async Task LoadAsync_NonAppIncludedBySource_RemainsVisible()
    {
        var custom = new TestProgramSource("custom:portable", 0, includeNonApps: true, @"C:\Links\document.lnk");
        using var source = new Win32AppSource(
            custom,
            (path, asRunCommand) => CreateNonApp(),
            createWatchers: false);

        var items = await source.LoadAsync(CancellationToken.None);

        Assert.AreEqual(1, items.Count);
        Assert.IsTrue(ContainsSourceReference(items[0], "custom:portable", @"C:\Links\document.lnk"));
    }

    [TestMethod]
    public async Task LoadAsync_NonAppExcludedByOnlySource_IsNotPublished()
    {
        var desktop = new TestProgramSource("desktop", 20, includeNonApps: false, @"C:\Links\document.lnk");
        using var source = new Win32AppSource(
            desktop,
            (path, asRunCommand) => CreateNonApp(),
            createWatchers: false);

        var items = await source.LoadAsync(CancellationToken.None);

        Assert.AreEqual(0, items.Count);
    }

    private static Win32Program CreateNonApp()
    {
        var program = TestDataHelper.CreateTestWin32Program("Document", @"C:\Documents\document.txt");
        program.AppType = Win32Program.ApplicationType.GenericFile;
        program.LnkFilePath = @"C:\Links\document.lnk";
        return program;
    }

    private static bool ContainsSourceReference(AppCatalogItem item, string sourceId, string itemId)
    {
        foreach (var reference in item.Provenance.References)
        {
            if (string.Equals(reference.SourceId, sourceId, StringComparison.Ordinal)
                && string.Equals(reference.ItemId, itemId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class TestProgramSource : IWin32ProgramSource
    {
        private readonly IReadOnlyList<string> _paths;

        public TestProgramSource(string id, int priority, bool includeNonApps, params string[] paths)
        {
            Id = id;
            Priority = priority;
            IncludeNonApps = includeNonApps;
            _paths = paths;
        }

        public string Id { get; }

        public int Priority { get; }

        public bool IsEnabled => true;

        public bool IncludeNonApps { get; }

        public bool AsRunCommand => false;

        public string CacheKey => Id;

        public string ConfigurationKey => Id;

        public IReadOnlyList<string> WatchPaths => [];

        public IEnumerable<string> GetPaths() => _paths;

        public bool IsRelevantPath(string path) => true;
    }
}
