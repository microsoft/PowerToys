// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed partial class TopLevelFallbackViewModelTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    // View models hold the page context weakly, so keep it alive for every test.
    private static readonly TestPageContext PageContext = new();

    [TestMethod]
    [DataRow("Search a", "Search ab", false, false)]
    [DataRow("", "Search a", false, true)]
    [DataRow("Search a", "", false, true)]
    [DataRow("Search a", "  ", false, true)]
    [DataRow("Search a", "Search a", false, false)]
    [DataRow("Search a", "Search ab", true, true)]
    [DataRow("", "Search a", true, true)]
    [DataRow("Search a", "Search a", true, false)]
    public void UpdateFallbackText_ReportsTextChangesOnlyForGlobalFallbacks(string oldTitle, string newTitle, bool reportTitleChanges, bool expected)
    {
        var fallback = new QueryTitleFallback("test.fallback");
        using var services = CreateServices();
        var topLevel = CreateTopLevel(fallback, new ProviderSettings(), services);

        try
        {
            topLevel.SafeUpdateFallbackTextSynchronous(oldTitle, reportTitleChanges: true, CancellationToken.None);
            Assert.AreEqual(oldTitle, topLevel.Title);

            Assert.AreEqual(expected, topLevel.SafeUpdateFallbackTextSynchronous(newTitle, reportTitleChanges, CancellationToken.None));
            Assert.AreEqual(newTitle, topLevel.Title);
        }
        finally
        {
            topLevel.Cleanup();
        }
    }

    [TestMethod]
    [DataRow(false, "Search a", "Search ab")]
    [DataRow(true, "Reload", "Reload")]
    public void UpdateFallbackText_ReportsChangeLostToCancelledUpdate(bool reportTitleChanges, string cancelledTitle, string latestTitle)
    {
        var fallback = new QueryTitleFallback("test.fallback");
        using var services = CreateServices();
        var topLevel = CreateTopLevel(fallback, new ProviderSettings(), services);
        using var superseded = new CancellationTokenSource();
        superseded.Cancel();

        try
        {
            topLevel.SafeUpdateFallbackTextSynchronous(string.Empty, reportTitleChanges, CancellationToken.None);

            // The superseded query shows the fallback, but its refresh is dropped.
            Assert.IsFalse(topLevel.SafeUpdateFallbackTextSynchronous(cancelledTitle, reportTitleChanges, superseded.Token));
            Assert.AreEqual(cancelledTitle, topLevel.Title);

            // The latest query must still refresh, or the list never shows the fallback.
            Assert.IsTrue(topLevel.SafeUpdateFallbackTextSynchronous(latestTitle, reportTitleChanges, CancellationToken.None));
            Assert.IsFalse(topLevel.SafeUpdateFallbackTextSynchronous(latestTitle, reportTitleChanges, CancellationToken.None));
        }
        finally
        {
            topLevel.Cleanup();
        }
    }

    [TestMethod]
    public void UpdateFallbackText_SkipsDisabledFallback()
    {
        var fallback = new QueryTitleFallback("test.fallback");
        var settings = new ProviderSettings
        {
            FallbackCommands = ImmutableDictionary<string, FallbackSettings>.Empty
                .Add(fallback.Id, new FallbackSettings(isEnabled: false, includeInGlobalResults: true)),
        };
        using var services = CreateServices();
        var topLevel = CreateTopLevel(fallback, settings, services);

        try
        {
            Assert.IsFalse(topLevel.SafeUpdateFallbackTextSynchronous("Search a", reportTitleChanges: true, CancellationToken.None));
            Assert.AreEqual(0, fallback.QueryCount);
        }
        finally
        {
            topLevel.Cleanup();
        }
    }

    [TestMethod]
    public void BeginUpdate_RefreshesOnlyForGlobalTitleChanges()
    {
        var global = new QueryTitleFallback("test.global");
        var common = new QueryTitleFallback("test.common");
        using var services = CreateServices();
        var globalTopLevel = CreateTopLevel(global, new ProviderSettings(), services);
        var commonTopLevel = CreateTopLevel(common, new ProviderSettings(), services);
        var refreshes = 0;
        var manager = new FallbackUpdateManager(() => Interlocked.Increment(ref refreshes));

        try
        {
            // Both fallbacks already show a title, so the next query only changes their text.
            globalTopLevel.SafeUpdateFallbackTextSynchronous("Search a", reportTitleChanges: true, CancellationToken.None);
            commonTopLevel.SafeUpdateFallbackTextSynchronous("Search a", reportTitleChanges: true, CancellationToken.None);
            using var queried = new CountdownEvent(2);
            global.Queried += () => queried.Signal();
            common.Queried += () => queried.Signal();

            manager.BeginUpdate("Search ab", [globalTopLevel, commonTopLevel], globalFallbackCount: 1, CancellationToken.None);

            Assert.IsTrue(queried.Wait(TestTimeout), "Both fallbacks should receive the query.");

            // Disposing drains the workers, so every refresh they would request has been requested.
            manager.Dispose();
            Assert.AreEqual(1, Volatile.Read(ref refreshes), "Only the global fallback's text change should refresh the list.");
            Assert.AreEqual("Search ab", commonTopLevel.Title);
        }
        finally
        {
            manager.Dispose();
            globalTopLevel.Cleanup();
            commonTopLevel.Cleanup();
        }
    }

    private static ServiceProvider CreateServices() => new ServiceCollection().AddSingleton(Mock.Of<ISettingsService>()).BuildServiceProvider();

    private static TopLevelFallbackViewModel CreateTopLevel(QueryTitleFallback fallback, ProviderSettings settings, IServiceProvider services)
    {
        var source = new CommandItemViewModel(new(fallback), new(PageContext), DefaultContextMenuFactory.Instance);
        source.InitializeProperties();
        return new TopLevelFallbackViewModel(source, CommandPaletteHost.Instance, CommandProviderContext.Empty, settings, services, fallback, DefaultContextMenuFactory.Instance);
    }

    // Mirrors fallbacks such as web search, whose title embeds the query.
    private sealed partial class QueryTitleFallback(string id) : FallbackCommandItem(new NoOpCommand(), "Query fallback", id)
    {
        private int _queryCount;

        public event Action? Queried;

        public int QueryCount => Volatile.Read(ref _queryCount);

        public override void UpdateQuery(string query)
        {
            Interlocked.Increment(ref _queryCount);
            Title = query;
            Queried?.Invoke();
        }
    }

    private sealed class TestPageContext : IPageContext
    {
        public TaskScheduler Scheduler => TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null)
        {
            throw new AssertFailedException($"Unexpected exception from view model: {ex}");
        }
    }
}
