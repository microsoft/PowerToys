// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AppExecutionAliasProjectionTests : AppsTestBase
{
    [TestMethod]
    [Timeout(10_000)]
    public async Task OwnershipPublication_ReranksOpenQueryWithoutReplacingRowsOrCommands()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timeProvider = new TestTimeProvider();
        var owner = "TerminalPreview_123!App";
        var reads = 0;
        TestWatcher watcher = null;
        using var cache = new AppExecutionAliasCache(
            () =>
            {
                reads++;
                entered.TrySetResult();
                release.Task.GetAwaiter().GetResult();
                return new Dictionary<string, string> { ["wt.exe"] = owner };
            },
            timeProvider,
            TimeSpan.FromSeconds(2),
            createWatcher: (changed, _) => watcher = new TestWatcher(changed),
            debounceInterval: TimeSpan.Zero);
        using var catalog = new MockAppCatalog();
        catalog.AddWin32Program(CreateTerminal("Terminal", "Terminal_123!App"));
        catalog.AddWin32Program(CreateTerminal("Terminal Preview", "TerminalPreview_123!App"));
        using var source = new AppListItemSource(catalog, Settings, NullLogger<AppListItemSource>.Instance, cache);
        using var page = new AllAppsPage(source, TestDataHelper.CreateFuzzyMatcherProvider());
        using var provider = new AllAppsCommandProvider(page, source, Settings);
        await WaitForPageInitializationAsync(page);
        var original = source.GetSnapshot();
        var rows = original.VisibleItems.ToDictionary(item => item.Command!.Id);
        var sourceNotifications = 0;
        var providerNotifications = 0;
        source.Changed += (_, _) => sourceNotifications++;
        provider.ItemsChanged += (_, _) => providerNotifications++;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual(string.Empty, page.SearchText, "Source construction primes ownership before searching.");
            page.SearchText = "wt";
            Assert.AreEqual("Terminal", page.GetItems().First().Title);
            Assert.IsFalse(page.IsLoading, "Cold alias lookup must not delay startup or add a loading banner.");
            Assert.IsTrue(source.GetSnapshot().ExecutionAliasOwners.IsEmpty);
            Assert.AreEqual(1, reads);
        }
        finally
        {
            release.TrySetResult();
        }

        await cache.PendingRefresh.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual("Terminal Preview", page.GetItems().First().Title);
        Assert.AreEqual("wt", page.SearchText);
        Assert.AreEqual(1, sourceNotifications);
        var previewSnapshot = source.GetSnapshot();
        Assert.AreEqual(owner, previewSnapshot.GetExecutionAliasOwner("WT.EXE"));
        Assert.IsNull(original.GetExecutionAliasOwner("wt"), "Published snapshots remain unchanged.");

        owner = "Terminal_123!App";
        watcher.NotifyChanged();
        await cache.PendingRefresh.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual("Terminal", page.GetItems().First().Title);
        Assert.AreEqual(2, reads);
        Assert.AreEqual(2, sourceNotifications);
        Assert.AreEqual("TerminalPreview_123!App", previewSnapshot.GetExecutionAliasOwner("wt"));
        Assert.AreSame(original.VisibleItems, source.GetSnapshot().VisibleItems);
        Assert.AreSame(original.HiddenItems, source.GetSnapshot().HiddenItems);
        Assert.AreSame(original.PatternHiddenItems, source.GetSnapshot().PatternHiddenItems);
        foreach (var row in source.GetSnapshot().VisibleItems)
        {
            Assert.AreSame(rows[row.Command!.Id], row);
            Assert.AreSame(rows[row.Command.Id].Command, source.GetSnapshot().GetCommandItem(row.Command.Id)!.Command);
        }

        Assert.AreEqual(0, providerNotifications, "Ownership preference must not reload pins or dock commands.");
        Assert.AreEqual(0, catalog.RefreshCallCount);
        Assert.AreEqual(1, catalog.InitializeCallCount);
    }

    [TestMethod]
    public async Task SourceConstruction_PrimesOwnershipForFirstSearch()
    {
        var reads = 0;
        using var cache = new AppExecutionAliasCache(
            () =>
            {
                reads++;
                return new Dictionary<string, string> { ["wt.exe"] = "TerminalPreview_123!App" };
            },
            TimeProvider.System,
            TimeSpan.FromHours(1));
        using var catalog = new MockAppCatalog();
        catalog.AddWin32Program(CreateTerminal("Terminal", "Terminal_123!App"));
        catalog.AddWin32Program(CreateTerminal("Terminal Preview", "TerminalPreview_123!App"));
        using var source = new AppListItemSource(catalog, Settings, NullLogger<AppListItemSource>.Instance, cache);
        await cache.PendingRefresh.WaitAsync(TimeSpan.FromSeconds(5));
        using var page = new AllAppsPage(source, TestDataHelper.CreateFuzzyMatcherProvider());
        page.SearchText = "wt";

        Assert.AreEqual("Terminal Preview", page.GetItems().First().Title);
        Assert.AreEqual(1, reads, "The first search reuses the background-primed ownership map.");
    }

    [TestMethod]
    public async Task SourceConstruction_CapturesAlreadyPublishedOwnersWithoutStartingARead()
    {
        var reads = 0;
        using var cache = new AppExecutionAliasCache(
            () =>
            {
                reads++;
                return new Dictionary<string, string> { ["wt.exe"] = "Terminal_123!App" };
            },
            TimeProvider.System,
            TimeSpan.FromHours(1));
        cache.RequestRefresh();
        await cache.PendingRefresh.WaitAsync(TimeSpan.FromSeconds(5));
        using var catalog = new MockAppCatalog();
        using var source = new AppListItemSource(catalog, Settings, NullLogger<AppListItemSource>.Instance, cache);

        Assert.AreEqual("Terminal_123!App", source.GetSnapshot().GetExecutionAliasOwner("wt"));
        Assert.AreEqual(1, reads);
    }

    private static Win32Program CreateTerminal(string name, string aumid)
    {
        var program = TestDataHelper.CreateTestWin32Program(name, $@"C:\{name}\wt.exe");
        program.PackagedAppUserModelId = aumid;
        program.ExecutableName = "wt.exe";
        return program;
    }

    private sealed class TestWatcher(Action changed) : IDisposable
    {
        public void NotifyChanged()
        {
            changed();
        }

        public void Dispose()
        {
        }
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }
    }
}
