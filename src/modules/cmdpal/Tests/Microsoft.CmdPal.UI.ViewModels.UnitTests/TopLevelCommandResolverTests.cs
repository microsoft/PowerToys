// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class TopLevelCommandResolverTests
{
    [TestMethod]
    public void Resolve_UsesPinOrderAndSkipsUnavailableOrIneligibleCommands()
    {
        var pins = new[]
        {
            new PinnedCommandSettings("provider-b", "second"),
            new PinnedCommandSettings("missing", "command"),
            new PinnedCommandSettings("provider-a", "first"),
            new PinnedCommandSettings("provider-a", "hidden"),
        };
        var commands = new[]
        {
            new TestCommand("provider-a", "first", IsEligible: true),
            new TestCommand("provider-a", "hidden", IsEligible: false),
            new TestCommand("provider-b", "second", IsEligible: true),
        };

        var sections = TopLevelCommandResolver.Resolve(
            pins,
            [],
            commands,
            static command => command.ProviderId,
            static command => command.CommandId,
            static command => command.IsEligible);

        CollectionAssert.AreEqual(new[] { commands[2], commands[0] }, sections.Pinned.ToArray());
        Assert.AreEqual(0, sections.Recent.Count);
        Assert.AreEqual(0, sections.Regular.Count);
    }

    [TestMethod]
    public void Resolve_PinnedLimitCountsResolvedPinsAndLetsDroppedPinsAppearInRecent()
    {
        var pins = new[]
        {
            new PinnedCommandSettings("missing", "missing"),
            new PinnedCommandSettings("provider-a", "first"),
            new PinnedCommandSettings("provider-b", "second"),
        };
        var commands = new[]
        {
            new TestCommand("provider-a", "first", IsEligible: true),
            new TestCommand("provider-b", "second", IsEligible: true),
        };

        var sections = TopLevelCommandResolver.Resolve(
            pins,
            ["second", "first"],
            commands,
            static command => command.ProviderId,
            static command => command.CommandId,
            static command => command.IsEligible,
            pinnedCommandLimit: 1,
            recentCommandLimit: 2);

        CollectionAssert.AreEqual(new[] { commands[0] }, sections.Pinned.ToArray());
        CollectionAssert.AreEqual(new[] { commands[1] }, sections.Recent.ToArray());
        Assert.AreEqual(0, sections.Regular.Count);
    }

    [TestMethod]
    public void Resolve_RecentFirstExcludesRecentItemsFromPinsAndBackfillsPinnedLimit()
    {
        var pins = new[]
        {
            new PinnedCommandSettings("provider-a", "recent-pin"),
            new PinnedCommandSettings("provider-b", "second"),
            new PinnedCommandSettings("provider-c", "third"),
        };
        var commands = new[]
        {
            new TestCommand("provider-a", "recent-pin", IsEligible: true),
            new TestCommand("provider-b", "second", IsEligible: true),
            new TestCommand("provider-c", "third", IsEligible: true),
            new TestCommand("provider-d", "recent-only", IsEligible: true),
        };

        var sections = TopLevelCommandResolver.Resolve(
            pins,
            ["recent-pin", "recent-only"],
            commands,
            static command => command.ProviderId,
            static command => command.CommandId,
            static command => command.IsEligible,
            pinnedCommandLimit: 2,
            recentCommandLimit: 2,
            recentCommandsFirst: true);

        CollectionAssert.AreEqual(new[] { commands[0], commands[3] }, sections.Recent.ToArray());
        CollectionAssert.AreEqual(new[] { commands[1], commands[2] }, sections.Pinned.ToArray());
        Assert.AreEqual(0, sections.Regular.Count);
    }

    [TestMethod]
    public void Resolve_RecentCommandsFollowHistoryAndExcludePinsAndMissingCommands()
    {
        var commands = new[]
        {
            new TestCommand("provider-e", "pinned", IsEligible: true),
            new TestCommand("provider-a", "pinned", IsEligible: true),
            new TestCommand("provider-b", "older", IsEligible: true),
            new TestCommand("provider-c", "newer", IsEligible: true),
            new TestCommand("provider-d", "regular", IsEligible: true),
        };

        var sections = TopLevelCommandResolver.Resolve(
            [new PinnedCommandSettings("provider-a", "pinned")],
            ["pinned", "missing", "newer", "older", "regular"],
            commands,
            static command => command.ProviderId,
            static command => command.CommandId,
            static command => command.IsEligible,
            recentCommandLimit: 2);

        CollectionAssert.AreEqual(new[] { commands[1] }, sections.Pinned.ToArray());
        CollectionAssert.AreEqual(new[] { commands[3], commands[2] }, sections.Recent.ToArray());
        CollectionAssert.AreEqual(new[] { commands[0], commands[4] }, sections.Regular.ToArray());
    }

    [TestMethod]
    public void Resolve_UsesAdditionalResolverForRecentItemsWithoutAddingThemToRegularCommands()
    {
        var regular = new TestCommand("provider-a", "regular", IsEligible: true);
        var recentApp = new TestCommand("AllApps", "recent-app", IsEligible: true);

        var sections = TopLevelCommandResolver.Resolve(
            [],
            ["missing", "recent-app"],
            [regular],
            static command => command.ProviderId,
            static command => command.CommandId,
            static command => command.IsEligible,
            commandId => commandId == recentApp.CommandId ? recentApp : null);

        CollectionAssert.AreEqual(new[] { recentApp }, sections.Recent.ToArray());
        CollectionAssert.AreEqual(new[] { regular }, sections.Regular.ToArray());
    }

    [TestMethod]
    public void Resolve_UsesFirstPassKeyForRegularCommands()
    {
        var command = new TestCommand("provider-a", "command", IsEligible: true);
        var providerIdReads = 0;

        var sections = TopLevelCommandResolver.Resolve(
            [new PinnedCommandSettings("provider-a", "command")],
            [],
            [command],
            _ => ++providerIdReads == 1 ? "provider-a" : "provider-b",
            static command => command.CommandId,
            static command => command.IsEligible);

        Assert.AreEqual(1, providerIdReads);
        CollectionAssert.AreEqual(new[] { command }, sections.Pinned.ToArray());
        Assert.AreEqual(0, sections.Regular.Count);
    }

    [DataTestMethod]
    [DataRow(false, "Command", true)]
    [DataRow(true, "Command", false)]
    [DataRow(false, "", false)]
    [DataRow(false, null, false)]
    public void IsEligibleForHome_ExcludesFallbacksAndUntitledCommands(bool isFallback, string? title, bool expected)
    {
        Assert.AreEqual(expected, TopLevelCommandEligibility.IsEligibleForHome(isFallback, title));
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, false, false)]
    [DataRow(true, true, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, true)]
    [DataRow(true, false, true)]
    [DataRow(true, true, true)]
    public void ResolveSnapshot_EquivalentPinsAndHistoryKeepFirstPinWrapper(bool recentFirst, bool legacyFirst, bool hidden)
    {
        const string legacyId = "Legacy Editor_123";
        var app = CreateApp("Editor");
        var canonicalId = app.Command!.Id;
        var snapshot = new AppListItemSnapshot(hidden ? [] : [app], hidden ? [app] : [], commandAliases: new Dictionary<string, string> { [legacyId] = canonicalId });
        using var services = CreateServices();
        var canonical = CreateTopLevelCommand(canonicalId, services);
        var legacy = CreateTopLevelCommand(legacyId, services);
        var firstId = legacyFirst ? legacyId : canonicalId;
        var secondId = legacyFirst ? canonicalId : legacyId;
        var pins = new[]
        {
            new PinnedCommandSettings(AllAppsCommandProvider.WellKnownId, firstId),
            new PinnedCommandSettings(AllAppsCommandProvider.WellKnownId, secondId),
        };
        try
        {
            var sections = TopLevelCommandResolver.Resolve(
                pins,
                hidden ? [legacyId] : [legacyId, canonicalId],
                [canonical, legacy],
                snapshot,
                includeApps: true,
                recentCommandsFirst: recentFirst);

            var first = legacyFirst ? legacy : canonical;
            Assert.AreEqual(recentFirst ? 0 : 1, sections.Pinned.Count);
            Assert.AreEqual(recentFirst ? 1 : 0, sections.Recent.Count);
            Assert.AreSame(first, recentFirst ? sections.Recent.Single() : sections.Pinned.Single());
            Assert.AreEqual(firstId, first.Id, "Pin operations must retain the selected persisted ID.");
            Assert.AreEqual(firstId, pins[0].CommandId);
            Assert.AreEqual(secondId, pins[1].CommandId);
            Assert.AreEqual(0, sections.Regular.Count);
        }
        finally
        {
            canonical.Cleanup();
            legacy.Cleanup();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ResolveSnapshot_RecentAliasesResolveOnceAndBackfillTheLimit(bool savedAlias)
    {
        const string legacyId = "Legacy Editor_123";
        var app = CreateApp("Editor");
        var other = CreateApp("Other");
        app.App.CommandIds = savedAlias ? [] : [legacyId];
        var snapshot = new AppListItemSnapshot(
            [app, other],
            [],
            commandAliases: savedAlias ? new Dictionary<string, string> { [legacyId] = app.Command!.Id } : null);

        var sections = TopLevelCommandResolver.Resolve(
            [],
            [legacyId, app.Command!.Id, other.Command!.Id],
            [],
            snapshot,
            includeApps: true,
            recentCommandLimit: 2);

        CollectionAssert.AreEqual(new IListItem[] { app, other }, sections.Recent.ToArray());
        Assert.AreEqual(0, sections.Pinned.Count);
        Assert.AreEqual(0, sections.Regular.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ResolveSnapshot_RetainsHiddenPinsButExcludesDisabledAndAmbiguousApps(bool includeApps)
    {
        const string legacyId = "Legacy Editor_123";
        const string ambiguousId = "Shared Editor_456";
        const string pageId = "browse-apps";
        var app = CreateApp("Editor");
        var other = CreateApp("Other");
        var hidden = CreateApp("Hidden");
        var excluded = CreateApp("Excluded");
        var snapshot = new AppListItemSnapshot(
            [app, other],
            [hidden],
            [excluded],
            new Dictionary<string, string> { [legacyId] = app.Command!.Id, [ambiguousId] = string.Empty });
        using var services = CreateServices();
        var visiblePin = CreateTopLevelCommand(legacyId, services);
        var hiddenPin = CreateTopLevelCommand(hidden.Command!.Id, services);
        var excludedPin = CreateTopLevelCommand(excluded.Command!.Id, services);
        var ambiguousPin = CreateTopLevelCommand(ambiguousId, services);
        var appsPage = CreateTopLevelCommand(pageId, services, isPage: true);
        var regular = CreateTopLevelCommand("regular", services, providerId: "other-provider");
        var commands = new[] { hiddenPin, excludedPin, ambiguousPin, visiblePin, appsPage, regular };
        try
        {
            var sections = TopLevelCommandResolver.Resolve(
                commands.Take(5).Select(command => new PinnedCommandSettings(AllAppsCommandProvider.WellKnownId, command.Id)),
                [hidden.Command!.Id, excluded.Command!.Id, ambiguousId, legacyId, other.Command!.Id, pageId],
                commands,
                snapshot,
                includeApps);

            CollectionAssert.AreEqual(includeApps ? new IListItem[] { hiddenPin, excludedPin, visiblePin, appsPage } : [], sections.Pinned.ToArray());
            CollectionAssert.AreEqual(includeApps ? new IListItem[] { other } : [], sections.Recent.ToArray());
            CollectionAssert.AreEqual(new IListItem[] { regular }, sections.Regular.ToArray());
            Assert.AreEqual(legacyId, visiblePin.Id);
            Assert.IsNull(snapshot.GetVisibleApp(ambiguousId));

            var unpinned = TopLevelCommandResolver.Resolve(
                [],
                [hidden.Command!.Id, excluded.Command!.Id, ambiguousId],
                [hiddenPin, excludedPin, ambiguousPin],
                snapshot,
                includeApps);
            Assert.AreEqual(0, unpinned.Pinned.Count);
            Assert.AreEqual(0, unpinned.Recent.Count);
            Assert.AreEqual(0, unpinned.Regular.Count);
        }
        finally
        {
            foreach (var command in commands)
            {
                command.Cleanup();
            }
        }
    }

    private static ServiceProvider CreateServices()
    {
        var settings = new Mock<ISettingsService>();
        settings.SetupGet(service => service.Settings).Returns(new SettingsModel());
        return new ServiceCollection()
            .AddSingleton(TaskScheduler.Default)
            .AddSingleton(settings.Object)
            .BuildServiceProvider();
    }

    private static AppListItem CreateApp(string name)
    {
        return new AppListItem(new AppItem { Name = name, CatalogId = $"packaged:Contoso.{name}!App" });
    }

    private static TopLevelViewModel CreateTopLevelCommand(
        string commandId,
        IServiceProvider services,
        bool isPage = false,
        string providerId = AllAppsCommandProvider.WellKnownId)
    {
        ICommand command = isPage ? new ListPage { Id = commandId, Name = "Apps" } : new NoOpCommand { Id = commandId, Name = "Editor" };
        var model = new CommandItem(command) { Title = "Editor" };
        var context = new TestPageContext(providerId);
        var item = new CommandItemViewModel(new(model), new(context), DefaultContextMenuFactory.Instance);
        var topLevel = new TopLevelViewModel(
            item,
            TopLevelType.Normal,
            CommandPaletteHost.Instance,
            context.ProviderContext,
            new ProviderSettings(),
            services,
            model,
            DefaultContextMenuFactory.Instance);
        topLevel.InitializeProperties();
        return topLevel;
    }

    private sealed class TestPageContext(string providerId) : IPageContext
    {
        public TaskScheduler Scheduler => TaskScheduler.Default;

        public ICommandProviderContext ProviderContext { get; } = new TestProviderContext(providerId);

        public void ShowException(Exception ex, string? extensionHint = null)
        {
            throw new AssertFailedException($"Unexpected exception from view model: {ex}");
        }
    }

    private sealed record TestProviderContext(string ProviderId) : ICommandProviderContext
    {
        public bool SupportsPinning => true;
    }

    private sealed record TestCommand(string ProviderId, string CommandId, bool IsEligible);
}
