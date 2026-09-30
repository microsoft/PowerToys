// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed partial class QuickAccessShelfAppSourceTests
{
    [TestMethod]
    public async Task SourcePublication_RebuildsRecentAppsDuringLoadingAndUnsubscribesOnDispose()
    {
        var first = CreateApp("First");
        var second = CreateApp("Second");
        var history = new RecentCommandsManager().WithHistoryItem(first.Command!.Id).WithHistoryItem(second.Command!.Id);
        using var fixture = new ShelfFixture(new AppListItemSnapshot([first], [second]), history);
        await fixture.Manager.LoadExternalProvidersAsync();
        using var shelf = fixture.CreateShelf(RecentCommandsPlacement.AfterPinned);
        var initial = await RebuildAsync(
            shelf,
            () => shelf.SetItemConfiguration(RecentCommandsPlacement.AfterPinned, 8, 8, forceRebuild: true),
            items => items.Count == 1 && items[0].CommandId == first.Command!.Id);
        Assert.AreEqual(first.Command!.Id, initial.Single().CommandId);

        var updated = await RebuildAsync(
            shelf,
            () => fixture.Publish(new AppListItemSnapshot([second], [first]), isLoading: true),
            items => items.Count == 1 && items[0].CommandId == second.Command!.Id);

        Assert.AreEqual(second.Command!.Id, updated.Single().CommandId);
        Assert.AreSame(history, fixture.State.RecentCommands);
        fixture.Source.Verify(source => source.RefreshAsync(), Times.Never);
        shelf.Dispose();
        fixture.Source.VerifyRemove(source => source.Changed -= It.IsAny<EventHandler>(), Times.Once);
        fixture.Source.Verify(source => source.Dispose(), Times.Never);
    }

    [TestMethod]
    [DataRow(false, RecentCommandsPlacement.Hidden)]
    [DataRow(true, RecentCommandsPlacement.Hidden)]
    [DataRow(false, RecentCommandsPlacement.BeforePinned)]
    [DataRow(true, RecentCommandsPlacement.BeforePinned)]
    [DataRow(false, RecentCommandsPlacement.AfterPinned)]
    [DataRow(true, RecentCommandsPlacement.AfterPinned)]
    public async Task SourcePublication_KeepsLegacyPinsWhenAppsAreHidden(bool patternHidden, RecentCommandsPlacement placement)
    {
        const string legacyId = "Legacy Editor_123";
        var app = CreateApp("Editor");
        var aliases = new Dictionary<string, string> { [legacyId] = app.Command!.Id };
        var history = new RecentCommandsManager().WithHistoryItem(legacyId);
        using var fixture = new ShelfFixture(new AppListItemSnapshot([app], [], commandAliases: aliases), history, legacyId);
        await fixture.Manager.LoadExternalProvidersAsync();
        using var shelf = fixture.CreateShelf(placement);
        var initial = await RebuildAsync(
            shelf,
            () => shelf.SetItemConfiguration(placement, 8, 8, forceRebuild: true),
            items => items.Count == 1 && items[0].CommandId == legacyId);
        Assert.AreEqual(placement != RecentCommandsPlacement.BeforePinned, initial.Single().IsPinned);

        var hidden = await RebuildAsync(
            shelf,
            () => fixture.Publish(new AppListItemSnapshot([], patternHidden ? [] : [app], patternHidden ? [app] : [], commandAliases: aliases)),
            items => items.Count == 1 && items[0].CommandId == legacyId);
        Assert.AreEqual(placement != RecentCommandsPlacement.BeforePinned, hidden.Single().IsPinned);
        Assert.AreEqual(app.Title, hidden.Single().Title);

        var restored = await RebuildAsync(
            shelf,
            () => fixture.Publish(new AppListItemSnapshot([app], [], commandAliases: aliases)),
            items => items.Count == 1 && items[0].CommandId == legacyId);
        Assert.AreEqual(placement != RecentCommandsPlacement.BeforePinned, restored.Single().IsPinned);
        Assert.AreSame(history, fixture.State.RecentCommands);
        fixture.Source.Verify(source => source.RefreshAsync(), Times.Never);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task TryRemoveItem_RemovesCanonicalAndLegacyHistoryWithoutRemovingOtherApps(bool hideBeforeRemoval, bool patternHidden)
    {
        const string legacyId = "Legacy Editor_123";
        var app = CreateApp("Editor");
        var other = CreateApp("Other");
        var snapshot = new AppListItemSnapshot(
            [app, other],
            [],
            commandAliases: new Dictionary<string, string> { [legacyId] = app.Command!.Id });
        var history = new RecentCommandsManager()
            .WithHistoryItem(other.Command!.Id)
            .WithHistoryItem(legacyId)
            .WithHistoryItem(app.Command!.Id);
        using var fixture = new ShelfFixture(snapshot, history);
        await fixture.Manager.LoadExternalProvidersAsync();
        using var shelf = fixture.CreateShelf(RecentCommandsPlacement.AfterPinned);
        var initial = await RebuildAsync(
            shelf,
            () => shelf.SetItemConfiguration(RecentCommandsPlacement.AfterPinned, 8, 8, forceRebuild: true),
            items => items.Count == 2);
        var appItem = initial.Single(item => item.CommandId == app.Command!.Id);
        var removed = false;

        var updated = await RebuildAsync(
            shelf,
            () =>
            {
                if (hideBeforeRemoval)
                {
                    fixture.Publish(new AppListItemSnapshot(
                        [other],
                        patternHidden ? [] : [app],
                        patternHidden ? [app] : [],
                        commandAliases: new Dictionary<string, string> { [legacyId] = app.Command!.Id }));
                }

                removed = shelf.TryRemoveItem(appItem);
            },
            items => items.Count == 1 && items[0].CommandId == other.Command!.Id);

        Assert.IsTrue(removed);
        Assert.AreEqual(other.Command!.Id, updated.Single().CommandId);
        CollectionAssert.AreEqual(new[] { other.Command!.Id }, fixture.State.RecentCommands.EnumerateRecentCommandIds().ToArray());
        Assert.AreEqual(3, history.History.Count);
        Assert.AreSame(history.History.Single(item => item.CommandId == other.Command!.Id), fixture.State.RecentCommands.History.Single());
    }

    private static AppListItem CreateApp(string name)
    {
        return new AppListItem(new AppItem { Name = name, CatalogId = $"packaged:Contoso.{name}!App" }, useThumbnails: false);
    }

    private static async Task<QuickAccessShelfItem[]> RebuildAsync(
        QuickAccessShelfViewModel shelf,
        Action request,
        Func<IReadOnlyList<QuickAccessShelfItem>, bool> matches)
    {
        var completed = new TaskCompletionSource<QuickAccessShelfItem[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        shelf.RebuildCompleted += OnRebuildCompleted;
        try
        {
            request();
            return await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            shelf.RebuildCompleted -= OnRebuildCompleted;
        }

        void OnRebuildCompleted(object? sender, bool succeeded)
        {
            if (!succeeded)
            {
                completed.TrySetException(new AssertFailedException("The shelf rebuild failed."));
                return;
            }

            var items = shelf.Items.ToArray();
            if (matches(items))
            {
                completed.TrySetResult(items);
            }
        }
    }

    private sealed class ShelfFixture : IDisposable
    {
        private readonly ServiceProvider _services;
        private readonly Mock<IAppStateService> _stateService;
        private readonly Mock<ISettingsService> _settingsService;

        public Mock<IAppListItemSource> Source { get; } = new();

        public AppListItemSnapshot Snapshot { get; private set; }

        public AppStateModel State { get; private set; }

        public TopLevelCommandManager Manager { get; }

        public ShelfFixture(AppListItemSnapshot snapshot, RecentCommandsManager history, string? pinnedId = null)
        {
            Snapshot = snapshot;
            State = new AppStateModel { RecentCommands = history };
            Source.Setup(source => source.GetSnapshot()).Returns(() => Snapshot);
            Source.SetupGet(source => source.TopLevelResultLimit).Returns(8);
            _stateService = new Mock<IAppStateService>();
            _stateService.SetupGet(service => service.State).Returns(() => State);
            _stateService.Setup(service => service.UpdateState(It.IsAny<Func<AppStateModel, AppStateModel>>()))
                .Callback<Func<AppStateModel, AppStateModel>>(update =>
                {
                    State = update(State);
                    _stateService.Raise(service => service.StateChanged += null, _stateService.Object, State);
                });
            var settings = new SettingsModel();
            if (pinnedId is not null)
            {
                settings = settings.TryPinCommand(AllAppsCommandProvider.WellKnownId, pinnedId);
            }

            _settingsService = new Mock<ISettingsService>();
            _settingsService.SetupGet(service => service.Settings).Returns(settings);
            _services = new ServiceCollection()
                .AddSingleton(TaskScheduler.Default)
                .AddSingleton(_settingsService.Object)
                .BuildServiceProvider();
            var wrapper = new CommandProviderWrapper(new AppsProvider(() => Snapshot), TaskScheduler.Default);
            var extensionService = new Mock<IExtensionService>();
            extensionService.Setup(service => service.LoadProvidersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([wrapper]);
            Manager = new TopLevelCommandManager(_services, [extensionService.Object]);
        }

        public QuickAccessShelfViewModel CreateShelf(RecentCommandsPlacement placement)
        {
            return new QuickAccessShelfViewModel(Manager, _stateService.Object, _settingsService.Object, placement, 8, 8, TaskScheduler.Default, Source.Object);
        }

        public void Publish(AppListItemSnapshot snapshot, bool isLoading = false)
        {
            Snapshot = snapshot;
            Source.SetupGet(source => source.IsLoading).Returns(isLoading);
            Source.Raise(source => source.Changed += null, Source.Object, EventArgs.Empty);
        }

        public void Dispose()
        {
            Manager.Dispose();
            _services.Dispose();
        }
    }

    private sealed partial class AppsProvider : CommandProvider
    {
        private readonly Func<AppListItemSnapshot> _snapshot;

        public AppsProvider(Func<AppListItemSnapshot> snapshot)
        {
            _snapshot = snapshot;
            Id = AllAppsCommandProvider.WellKnownId;
            DisplayName = "Apps";
        }

        public override ICommandItem[] TopLevelCommands()
        {
            return [];
        }

        public override ICommandItem? GetCommandItem(string id)
        {
            return _snapshot().GetCommandItem(id);
        }
    }
}
