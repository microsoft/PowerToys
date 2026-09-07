// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class MainListPageCommandCatalogTests
{
    [TestMethod]
    public async Task ResolvedPinnedTitle_EntersUnchangedSearch()
    {
        using var fixture = new CatalogFixture();
        var existing = fixture.Add("Office editor");
        var pin = fixture.Add("Display profile (#7)");
        fixture.Search("office");
        CollectionAssert.AreEqual(new[] { existing.ViewModel }, fixture.Matches);

        // The changed item was excluded from the cached subset. The production event chain
        // must request another pass over the full catalog even though the query did not change.
        fixture.RefreshOnChange = true;
        pin.Model.Title = "Office (#7)";
        await WaitForAsync(() => fixture.Matches.Contains(pin.ViewModel));

        Assert.AreEqual("office", fixture.Query);
        Assert.AreEqual(2, fixture.Matches.Length);
    }

    [TestMethod]
    public async Task ExtendedQuery_AfterMetadataChange_DoesNotReuseOldMatchedSubset()
    {
        using var fixture = new CatalogFixture();
        fixture.Add("Office editor");
        var pin = fixture.Add("Display profile (#7)");
        fixture.Search("off");
        var oldGeneration = fixture.Generation;

        // Hold off the refresh to model another keystroke arriving before the background pass.
        pin.Model.Title = "Office (#7)";
        await WaitForAsync(() => !fixture.Catalog.IsCurrent(oldGeneration));
        fixture.Search("office");

        Assert.IsTrue(fixture.Matches.Contains(pin.ViewModel));
        Assert.AreEqual(2, fixture.Matches.Length);
    }

    [TestMethod]
    public async Task SubtitleAndCommandReplacement_RequeryCurrentCatalog()
    {
        using var fixture = new CatalogFixture();
        fixture.Add("Office editor");
        var pin = fixture.Add("Display profile (#7)");
        fixture.Search("office");
        fixture.RefreshOnChange = true;

        pin.Model.Subtitle = "Office display configuration";
        await WaitForAsync(() => fixture.Matches.Contains(pin.ViewModel));

        pin.Model.Subtitle = string.Empty;
        await WaitForAsync(() => !fixture.Matches.Contains(pin.ViewModel));
        pin.Model.Title = string.Empty;
        pin.Model.Command = new NoOpCommand { Id = "restored.pin", Name = "Office (#7)" };
        await WaitForAsync(() => fixture.Matches.Contains(pin.ViewModel));

        Assert.AreEqual("restored.pin", pin.ViewModel.Id);
    }

    [TestMethod]
    public async Task CommandNameChange_RefreshesItemWithoutExplicitTitle()
    {
        using var fixture = new CatalogFixture();
        fixture.Add("Office editor");
        var pin = fixture.Add(string.Empty, commandName: "Display profile (#7)");
        fixture.Search("office");
        fixture.RefreshOnChange = true;

        ((NoOpCommand)pin.Model.Command!).Name = "Office (#7)";
        await WaitForAsync(() => fixture.Matches.Contains(pin.ViewModel));
    }

    [TestMethod]
    public async Task RemovedAndReplacedItems_AreExcludedAndUnsubscribed()
    {
        using var fixture = new CatalogFixture();
        var removed = fixture.Add("Office old");
        fixture.Search("office");
        fixture.RefreshOnChange = true;

        lock (fixture.Commands)
        {
            fixture.Commands.Remove(removed.ViewModel);
        }

        Assert.AreEqual(0, fixture.Matches.Length);
        var replacement = fixture.Add("Office replacement");
        var newItem = fixture.Create("Office new");
        lock (fixture.Commands)
        {
            fixture.Commands[0] = newItem.ViewModel;
        }

        CollectionAssert.AreEqual(new[] { newItem.ViewModel }, fixture.Matches);
        var generation = fixture.Generation;
        await ChangeTitleAsync(removed, "Office removed mutation");
        await ChangeTitleAsync(replacement, "Office replaced mutation");
        Assert.IsTrue(fixture.Catalog.IsCurrent(generation));
    }

    [TestMethod]
    public async Task ResetAndDispose_UnsubscribeAllItems()
    {
        using var fixture = new CatalogFixture();
        var removed = fixture.Add("Office old");
        fixture.Search("office");
        fixture.RefreshOnChange = true;
        lock (fixture.Commands)
        {
            fixture.Commands.Clear();
        }

        var generation = fixture.Generation;
        await ChangeTitleAsync(removed, "Office removed mutation");
        Assert.IsTrue(fixture.Catalog.IsCurrent(generation));
        Assert.AreEqual(0, fixture.Matches.Length);

        var item = fixture.Add("Office retained");
        var refreshes = fixture.RefreshCount;
        fixture.Catalog.Dispose();
        await ChangeTitleAsync(item, "Office disposed mutation");
        fixture.Add("Office added after disposal");
        Assert.AreEqual(refreshes, fixture.RefreshCount);
        Assert.IsFalse(fixture.Catalog.IsCurrent(fixture.Generation));
    }

    [TestMethod]
    public async Task FallbackMetadata_DoesNotInvalidateCatalogOrStartAnotherQuery()
    {
        using var fixture = new CatalogFixture();
        fixture.Add("Office editor");
        var fallback = fixture.Add("Office fallback", TopLevelType.Fallback);
        fixture.Search("office");
        var generation = fixture.Generation;
        var refreshes = fixture.RefreshCount;

        await ChangeTitleAsync(fallback, "Office resolved fallback");

        Assert.IsTrue(fixture.Catalog.IsCurrent(generation));
        Assert.AreEqual(refreshes, fixture.RefreshCount);
        Assert.AreEqual(1, fixture.Matches.Length);
    }

    [TestMethod]
    public async Task MetadataChange_InvalidatesSnapshotAlreadyBeingScored()
    {
        using var fixture = new CatalogFixture();
        var pin = fixture.Add("Office old");
        fixture.Search("office");
        var inFlight = fixture.Catalog.Snapshot(fixture.Matches, fixture.Generation, out var generation);

        pin.Model.Title = "Display profile (#7)";
        await WaitForAsync(() => !fixture.Catalog.IsCurrent(generation));

        // MainListPage uses this same generation check at its publish boundary.
        Assert.IsFalse(fixture.Catalog.IsCurrent(generation));
        Assert.AreEqual(1, inFlight.Count);
        fixture.Search("office");
        Assert.AreEqual(0, fixture.Matches.Length);
    }

    private static async Task ChangeTitleAsync(TestItem item, string title)
    {
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Changed(object sender, IPropChangedEventArgs args)
        {
            if (args.PropertyName == nameof(ICommandItem.Title) && item.ViewModel.Title == title)
            {
                observed.TrySetResult();
            }
        }

        item.ViewModel.PropChanged += Changed;
        try
        {
            item.Model.Title = title;
            await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            item.ViewModel.PropChanged -= Changed;
        }
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.IsTrue(condition(), "The command metadata change did not refresh the search catalog.");
    }

    private sealed record TestItem(CommandItem Model, TopLevelViewModel ViewModel);

    private sealed class CatalogFixture : IDisposable
    {
        private readonly ServiceProvider _services;
        private readonly List<TestItem> _items = [];
        private readonly TestPageContext _context = new();
        private readonly PrecomputedFuzzyMatcher _matcher = new(new PrecomputedFuzzyMatcherOptions());
        private readonly RecentCommandsManager _history = new();
        private TopLevelViewModel[] _matches = [];

        internal CatalogFixture()
        {
            var settings = new Mock<ISettingsService>();
            settings.SetupGet(service => service.Settings).Returns(new SettingsModel());
            _services = new ServiceCollection().AddSingleton(settings.Object).BuildServiceProvider();
            Catalog = new MainListPageCommandCatalog(Commands, () =>
            {
                RefreshCount++;
                if (RefreshOnChange)
                {
                    Search(Query);
                }
            });
        }

        internal ObservableCollection<TopLevelViewModel> Commands { get; } = [];

        internal MainListPageCommandCatalog Catalog { get; }

        internal bool RefreshOnChange { get; set; }

        internal int RefreshCount { get; private set; }

        internal string Query { get; private set; } = string.Empty;

        internal long Generation { get; private set; } = -1;

        internal TopLevelViewModel[] Matches
        {
            get
            {
                lock (Commands)
                {
                    return _matches;
                }
            }
        }

        internal TestItem Create(string title, TopLevelType type = TopLevelType.Normal, string commandName = "Apply")
        {
            var model = new CommandItem(new NoOpCommand { Id = Guid.NewGuid().ToString(), Name = commandName }) { Title = title };
            var itemViewModel = new CommandItemViewModel(new(model), new(_context), DefaultContextMenuFactory.Instance);
            itemViewModel.InitializeProperties();
            itemViewModel.ApplyPendingUpdates();
            var viewModel = new TopLevelViewModel(
                itemViewModel,
                type,
                CommandPaletteHost.Instance,
                CommandProviderContext.Empty,
                new ProviderSettings(),
                _services,
                model,
                DefaultContextMenuFactory.Instance);
            var item = new TestItem(model, viewModel);
            _items.Add(item);
            return item;
        }

        internal TestItem Add(string title, TopLevelType type = TopLevelType.Normal, string commandName = "Apply")
        {
            var item = Create(title, type, commandName);
            lock (Commands)
            {
                Commands.Add(item.ViewModel);
            }

            return item;
        }

        internal void Search(string query)
        {
            lock (Commands)
            {
                var source = Catalog.Snapshot(_matches, Generation, out var generation);
                var fuzzyQuery = _matcher.PrecomputeQuery(query);
                ScoringFunction<IListItem> scorer = (in FuzzyQuery q, IListItem item) =>
                    MainListPage.ScoreTopLevelItem(q, item, _history, _matcher);
                var scored = InternalListHelpers.FilterListWithScores(source, fuzzyQuery, scorer);
                if (Catalog.IsCurrent(generation))
                {
                    _matches = scored.Select(result => (TopLevelViewModel)result.Item).ToArray();
                    Generation = generation;
                    Query = query;
                }
            }
        }

        public void Dispose()
        {
            Catalog.Dispose();
            foreach (var item in _items)
            {
                item.ViewModel.Cleanup();
            }

            _services.Dispose();
        }
    }

    private sealed class TestPageContext : IPageContext
    {
        public TaskScheduler Scheduler => TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null)
            => throw new AssertFailedException($"Unexpected view-model exception: {ex}");
    }
}
