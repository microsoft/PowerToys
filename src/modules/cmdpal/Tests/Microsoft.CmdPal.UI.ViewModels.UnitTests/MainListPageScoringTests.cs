// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed class MainListPageScoringTests
{
    private readonly IPageContext _pageContext = Mock.Of<IPageContext>(context => context.Scheduler == TaskScheduler.Default);
    private readonly List<TopLevelViewModel> _commands = [];

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var command in _commands)
        {
            command.Cleanup();
        }
    }

    [TestMethod]
    [DataRow("obsid", "Obsidian Tasks", "", "Obsidian Task Notes")]
    [DataRow("obsid", "Obsidian Tasks", "Obsidian task management", "Obsidian Task Notes")]
    [DataRow("github", "My issues", "GitHub issues", "GitHub")]
    [DataRow("a", "My issues", "za", "a")]
    public void TitleOrSubtitleMatch_DoesNotReceiveExtensionNameBonus(string search, string title, string subtitle, string extensionName)
    {
        var matcher = new PrecomputedFuzzyMatcher();
        var query = matcher.PrecomputeQuery(search);
        var history = new RecentCommandsManager();
        var command = CreateCommand(title, subtitle, extensionName);
        var itemWithoutExtension = new ListItem(new NoOpCommand()) { Title = title, Subtitle = subtitle };

        var expected = MainListPage.ScoreTopLevelItem(query, itemWithoutExtension, history, matcher);
        var actual = MainListPage.ScoreTopLevelItem(query, command, history, matcher);

        Assert.IsTrue(expected > 0, "The title or subtitle must match independently of the extension name.");
        Assert.AreEqual(expected, actual, "A matching extension name must not boost an existing title or subtitle match.");
    }

    [TestMethod]
    [DataRow("GitHub", true)]
    [DataRow("Unrelated provider", false)]
    public void WithoutTitleOrSubtitleMatch_ExtensionNameControlsDiscovery(string extensionName, bool shouldMatch)
    {
        var matcher = new PrecomputedFuzzyMatcher();
        var query = matcher.PrecomputeQuery("github");
        var history = new RecentCommandsManager();
        var command = CreateCommand("My issues", "View assigned issues", extensionName);
        var itemWithoutExtension = new ListItem(new NoOpCommand()) { Title = command.Title, Subtitle = command.Subtitle };

        Assert.AreEqual(0, MainListPage.ScoreTopLevelItem(query, itemWithoutExtension, history, matcher));

        var score = MainListPage.ScoreTopLevelItem(query, command, history, matcher);
        if (shouldMatch)
        {
            var extensionScore = matcher.Score(query, matcher.PrecomputeTarget(extensionName)) / 1.5;
            var expected = MainListRanker.Pack(
                RankTier.Fuzzy,
                MainListRanker.WithinTierScore(extensionScore, frecencyWeight: 0, aliasSubstringBonus: 0, providerBonus: 0));
            Assert.AreEqual(expected, score, "An extension-only match must retain its existing weight and fuzzy tier.");
        }
        else
        {
            Assert.AreEqual(0, score);
        }
    }

    [TestMethod]
    public void RecentlyUsedApp_OutranksCommandWithSameTitlePrefixAndMatchingExtension()
    {
        var matcher = new PrecomputedFuzzyMatcher();
        var query = matcher.PrecomputeQuery("obsid");
        var now = new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);
        var app = new AppListItem(new AppItem { Name = "Obsidian", Subtitle = "Obsidian" }, useThumbnails: false);
        var command = CreateCommand("Obsidian Tasks", "View and manage your tasks", "Obsidian Task Notes");
        Assert.IsNotNull(app.Command);
        var history = new RecentCommandsManager().WithHistoryItem(app.Command.Id, now);

        var appScore = MainListPage.ScoreTopLevelItem(query, app, history, matcher, now: now);
        var commandScore = MainListPage.ScoreTopLevelItem(query, command, history, matcher, now: now);

        Assert.AreEqual(RankTier.Prefix, MainListRanker.TierOf(appScore));
        Assert.AreEqual(RankTier.Prefix, MainListRanker.TierOf(commandScore));
        Assert.IsTrue(appScore > commandScore, "App usage must break the title-prefix tie without an additive extension-name bonus.");
    }

    private TopLevelViewModel CreateCommand(string title, string subtitle, string extensionName)
    {
        var extension = Mock.Of<IExtensionWrapper>(wrapper => wrapper.ExtensionDisplayName == extensionName);
        var services = Mock.Of<IServiceProvider>(service => service.GetService(typeof(ISettingsService)) == Mock.Of<ISettingsService>());
        var model = new CommandItem(new NoOpCommand { Id = "test.command", Name = title }) { Title = title, Subtitle = subtitle };
        var item = new CommandItemViewModel(new(model), new WeakReference<IPageContext>(_pageContext), DefaultContextMenuFactory.Instance);
        item.FastInitializeProperties();
        var command = new TopLevelViewModel(
            item,
            TopLevelType.Normal,
            new CommandPaletteHost(extension),
            CommandProviderContext.Empty,
            new ProviderSettings(),
            services,
            DefaultContextMenuFactory.Instance);
        _commands.Add(command);
        return command;
    }
}
