// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class FallbackPrototypeTests
{
    [TestMethod]
    public void ScoreTopLevelItem_AppliesFallbackPenaltyToWrappedFallbackItems()
    {
        var matcher = new PrecomputedFuzzyMatcher(new PrecomputedFuzzyMatcherOptions());
        var query = matcher.PrecomputeQuery("calc");
        var history = new RecentCommandsManager();

        var direct = new MockListItem("calc");
        var fallback = new MockFallbackListItem(
            title: "calc",
            extensionName: "Web Search",
            fallbackSourceId: "com.microsoft.cmdpal.websearch",
            invocationArgs: new FallbackCommandInvocationArgs { Query = "calc", QueryId = "1" });

        var directScore = MainListPage.ScoreTopLevelItem(query, direct, history, matcher);
        var fallbackScore = MainListPage.ScoreTopLevelItem(query, fallback, history, matcher);

        Assert.IsTrue(directScore > fallbackScore, "Wrapped fallback items should still rank below direct matches.");
    }

    [TestMethod]
    public void PerformCommandMessage_CarriesFallbackInvocationArgsFromListContext()
    {
        var command = new NoOpCommand() { Id = "command" };
        var args = new FallbackCommandInvocationArgs { Query = "search term", QueryId = "query-7" };
        var item = new MockFallbackListItem(
            title: "Search web for \"search term\"",
            extensionName: "Web Search",
            fallbackSourceId: "com.microsoft.cmdpal.websearch",
            invocationArgs: args,
            command: command);

        var message = new PerformCommandMessage(
            new ExtensionObject<ICommand>(command),
            new ExtensionObject<IListItem>(item));

        Assert.IsNotNull(message.FallbackCommandInvocationArgs);
        Assert.AreEqual(args.Query, message.FallbackCommandInvocationArgs!.Query);
        Assert.AreEqual(args.QueryId, message.FallbackCommandInvocationArgs.QueryId);
    }

    [TestMethod]
    public void PerformCommandMessage_UsesExplicitFallbackInvocationArgs()
    {
        var command = new NoOpCommand() { Id = "command" };
        var args = new FallbackCommandInvocationArgs { Query = "fresh query", QueryId = "query-9" };
        var item = new MockListItem("Search web");

        var message = new PerformCommandMessage(
            new ExtensionObject<ICommand>(command),
            new ExtensionObject<IListItem>(item),
            args);

        Assert.IsNotNull(message.FallbackCommandInvocationArgs);
        Assert.AreEqual(args.Query, message.FallbackCommandInvocationArgs!.Query);
        Assert.AreEqual(args.QueryId, message.FallbackCommandInvocationArgs.QueryId);
    }

    [TestMethod]
    public void IsRegexMatch_RequiresFullMatch()
    {
        Assert.IsTrue(TopLevelViewModel.IsRegexMatch(@"foo\d+", "foo123"));
        Assert.IsFalse(TopLevelViewModel.IsRegexMatch(@"foo\d+", "prefix foo123 suffix"));
    }

    [TestMethod]
    public void IsRegexMatch_ReturnsFalseForInvalidPattern()
    {
        Assert.IsFalse(TopLevelViewModel.IsRegexMatch("(", "foo"));
    }

    [TestMethod]
    public void FallbackQueryResultItem_PreservesViewModelIconWithoutCasting()
    {
        var source = new MockListItem("Open URL");
        var icon = new IconInfoViewModel(new IconInfo("\uE71B"));
        source.Icon = icon;

        var wrapped = new FallbackQueryResultItem(
            "com.microsoft.cmdpal.websearch:open-url",
            source,
            new MockHost(),
            CommandProviderContext.Empty,
            "com.microsoft.cmdpal.websearch",
            "Web Search",
            string.Empty,
            hasAlias: false,
            new FallbackCommandInvocationArgs { Query = "https://example.com", QueryId = "query-1" },
            isCurrent: () => true);

        Assert.AreSame(icon, wrapped.Icon);
    }

    [TestMethod]
    public void FallbackQueryResultItem_UpdatesIconWhenSourceItemChanges()
    {
        var source = new MockListItem("Open file")
        {
            Icon = new IconInfo("\uE8A5"),
        };

        var wrapped = new FallbackQueryResultItem(
            "com.microsoft.cmdpal.indexer:file",
            source,
            new MockHost(),
            CommandProviderContext.Empty,
            "com.microsoft.cmdpal.indexer",
            "Indexer",
            string.Empty,
            hasAlias: false,
            new FallbackCommandInvocationArgs { Query = "file", QueryId = "query-2" },
            isCurrent: () => true,
            listenForSourceItemUpdates: true);

        var updatedIcon = new IconInfo("\uE8A5");
        source.Icon = updatedIcon;

        Assert.AreSame(updatedIcon, wrapped.Icon);
    }

    [TestMethod]
    public void ProviderSettings_GetSuggestedFallbackQueryDelayMilliseconds_PrefersApiSuggestion()
    {
        var fallback = new DefaultsFallbackListItem("com.microsoft.cmdpal.builtin.indexer.fallback")
        {
            SuggestedQueryDelayMilliseconds = new OptionalUInt32(true, 200),
        };

        Assert.AreEqual(200u, ProviderSettings.GetSuggestedFallbackQueryDelayMilliseconds(fallback.Id, fallback));
    }

    [TestMethod]
    public void ProviderSettings_GetSuggestedFallbackQueryDelayMilliseconds_FallsBackToHostDefault()
    {
        Assert.AreEqual(120u, ProviderSettings.GetSuggestedFallbackQueryDelayMilliseconds("com.microsoft.cmdpal.builtin.indexer.fallback"));
    }

    [TestMethod]
    public void ProviderSettings_GetSuggestedFallbackMinQueryLength_PrefersApiSuggestion()
    {
        var fallback = new DefaultsFallbackListItem("com.microsoft.cmdpal.builtin.indexer.fallback")
        {
            SuggestedMinQueryLength = new OptionalUInt32(true, 3),
        };

        Assert.AreEqual(3u, ProviderSettings.GetSuggestedFallbackMinQueryLength(fallback.Id, fallback));
    }

    [TestMethod]
    public void ProviderSettings_GetEffectiveFallbackExecutionPolicy_PrefersExplicitOverrides()
    {
        var fallback = new DefaultsFallbackListItem("com.microsoft.cmdpal.builtin.indexer.fallback")
        {
            SuggestedQueryDelayMilliseconds = new OptionalUInt32(true, 120),
            SuggestedMinQueryLength = new OptionalUInt32(true, 2),
        };

        var settings = new FallbackSettings
        {
            QueryDelayMilliseconds = 25,
            MinQueryLength = 4,
        };

        var policy = ProviderSettings.GetEffectiveFallbackExecutionPolicy(fallback.Id, settings, fallback);

        Assert.AreEqual(System.TimeSpan.FromMilliseconds(25), policy.Delay);
        Assert.AreEqual(4u, policy.MinQueryLength);
    }

    [TestMethod]
    public void ProviderSettings_GetEffectiveFallbackExecutionPolicy_UsesSuggestedDefaultsWhenOverridesAreMissing()
    {
        var fallback = new DefaultsFallbackListItem("com.microsoft.cmdpal.builtin.indexer.fallback")
        {
            SuggestedQueryDelayMilliseconds = new OptionalUInt32(true, 90),
            SuggestedMinQueryLength = new OptionalUInt32(true, 3),
        };

        var policy = ProviderSettings.GetEffectiveFallbackExecutionPolicy(fallback.Id, fallbackSettings: null, fallback);

        Assert.AreEqual(System.TimeSpan.FromMilliseconds(90), policy.Delay);
        Assert.AreEqual(3u, policy.MinQueryLength);
    }

    [TestMethod]
    public void FallbackExecutionState_SuppressesShortQueriesBeforeInvokingFallback()
    {
        var fallbackItem = new CountingFallbackListItem();
        var state = new FallbackExecutionState(
            fallbackItem,
            asyncFallbackHandler: null,
            formattedFallbackCommandItem: null,
            hostMatchedFallbackCommandItem: null,
            getPolicy: static () => new FallbackExecutionPolicy(System.TimeSpan.Zero, 2),
            materializeSnapshotItems: static (query, queryId, snapshotItems) => snapshotItems.Select(item => (IListItem)new MockListItem(item.TitleOverride ?? item.SourceItem.Title)).ToArray(),
            requestRefresh: static () => { });

        var shortQueryChanged = state.UpdateSynchronous("a", fallbackItem);
        var validQueryChanged = state.UpdateSynchronous("ab", fallbackItem);

        Assert.IsFalse(shortQueryChanged);
        Assert.AreEqual(1, fallbackItem.UpdateQueryCallCount);
        Assert.IsTrue(validQueryChanged);
        Assert.AreEqual(1, state.GetCurrentItems().Length);
    }

    [TestMethod]
    public void FallbackExecutionState_LegacyFallbackSnapshotsListenForSourceItemUpdates()
    {
        var fallbackItem = new CountingFallbackListItem();
        var listenedForSourceUpdates = false;
        var state = new FallbackExecutionState(
            fallbackItem,
            asyncFallbackHandler: null,
            formattedFallbackCommandItem: null,
            hostMatchedFallbackCommandItem: null,
            getPolicy: static () => FallbackExecutionPolicy.Empty,
            materializeSnapshotItems: (query, queryId, snapshotItems) =>
            {
                listenedForSourceUpdates = snapshotItems[0].ListenForSourceItemUpdates;
                return [new MockListItem(snapshotItems[0].TitleOverride ?? snapshotItems[0].SourceItem.Title)];
            },
            requestRefresh: static () => { });

        state.UpdateSynchronous("icon-test", fallbackItem);

        Assert.IsTrue(listenedForSourceUpdates);
    }

    [TestMethod]
    public void FallbackSnapshotItemCache_ReusesWrappedItemsForStableIdentity()
    {
        var cache = new FallbackSnapshotItemCache(new MockHost(), CommandProviderContext.Empty, "com.microsoft.cmdpal.indexer", "Indexer");
        var firstSource = new MockListItem("Open foo.txt", new NoOpCommand() { Id = "foo.txt" });
        var firstItems = cache.Materialize(
            [new FallbackSnapshotDefinition(firstSource, true, null, null)],
            string.Empty,
            hasAlias: false,
            new FallbackCommandInvocationArgs { Query = "foo", QueryId = "query-1" },
            () => true);

        var secondSource = new MockListItem("Open foo.txt (updated)", new NoOpCommand() { Id = "foo.txt" });
        var secondItems = cache.Materialize(
            [new FallbackSnapshotDefinition(secondSource, true, null, null)],
            string.Empty,
            hasAlias: false,
            new FallbackCommandInvocationArgs { Query = "foo", QueryId = "query-2" },
            () => true);

        Assert.AreSame(firstItems[0], secondItems[0]);
        Assert.AreEqual("Open foo.txt (updated)", secondItems[0].Title);
        Assert.AreEqual("query-2", ((IFallbackResultItem)secondItems[0]).InvocationArgs?.QueryId);
    }

    [TestMethod]
    public void FallbackSnapshotItemCache_ClearDetachesSourceItemUpdates()
    {
        var cache = new FallbackSnapshotItemCache(new MockHost(), CommandProviderContext.Empty, "com.microsoft.cmdpal.indexer", "Indexer");
        var source = new MockListItem("Open bar.txt", new NoOpCommand() { Id = "bar.txt" })
        {
            Icon = new IconInfo("\uE8A5"),
        };

        var wrapped = (FallbackQueryResultItem)cache.Materialize(
            [new FallbackSnapshotDefinition(source, true, null, null)],
            string.Empty,
            hasAlias: false,
            new FallbackCommandInvocationArgs { Query = "bar", QueryId = "query-3" },
            () => true)[0];

        var oldIcon = wrapped.Icon;
        cache.Clear();
        source.Icon = new IconInfo("\uE7C3");

        Assert.AreSame(oldIcon, wrapped.Icon);
    }

    private sealed partial class MockListItem : ListItem
    {
        public MockListItem(string title, ICommand? command = null)
            : base(command ?? new NoOpCommand())
        {
            Title = title;
        }
    }

    private sealed partial class MockFallbackListItem : ListItem, IFallbackResultItem
    {
        public MockFallbackListItem(
            string title,
            string extensionName,
            string fallbackSourceId,
            IFallbackCommandInvocationArgs invocationArgs,
            ICommand? command = null)
            : base(command ?? new NoOpCommand() { Id = fallbackSourceId })
        {
            Title = title;
            ExtensionName = extensionName;
            FallbackSourceId = fallbackSourceId;
            InvocationArgs = invocationArgs;
        }

        public string FallbackSourceId { get; }

        public string ExtensionName { get; }

        public bool HasAlias => false;

        public string AliasText => string.Empty;

        public AppExtensionHost ExtensionHost { get; } = new MockHost();

        public ICommandProviderContext ProviderContext { get; } = CommandProviderContext.Empty;

        public IFallbackCommandInvocationArgs? InvocationArgs { get; }

        public bool IsCurrent => true;
    }

    private sealed partial class CountingFallbackListItem : ListItem, IFallbackCommandItem, IFallbackCommandItem2, IFallbackHandler
    {
        public CountingFallbackListItem()
            : base(new NoOpCommand() { Id = "com.microsoft.cmdpal.test.fallback" })
        {
        }

        public int UpdateQueryCallCount { get; private set; }

        public IFallbackHandler? FallbackHandler => this;

        public string DisplayTitle => "Fallback";

        public string Id => "com.microsoft.cmdpal.test.fallback";

        public void UpdateQuery(string query)
        {
            UpdateQueryCallCount++;
            Title = $"Result {query}";
        }
    }

    private sealed partial class DefaultsFallbackListItem : FallbackCommandItem
    {
        public DefaultsFallbackListItem(string id)
            : base("Fallback", id)
        {
        }
    }

    private sealed partial class MockHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Mock Host";
    }
}
