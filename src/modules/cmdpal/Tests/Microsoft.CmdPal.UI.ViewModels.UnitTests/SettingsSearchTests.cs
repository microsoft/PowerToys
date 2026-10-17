// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class SettingsSearchTests
{
    private readonly IPrecomputedFuzzyMatcher _matcher = new FuzzyMatcherProvider(new PrecomputedFuzzyMatcherOptions()).Current;

    [TestMethod]
    public void Catalog_UsesExistingLabelsAndCoversSettingsDestinations()
    {
        var entries = SettingsSearchCatalog.CreateEntries(LoadResources());
        var indexedLinks = entries.Select(entry => entry.Destination!.SettingsLinkId).Where(id => id is not null).ToArray();

        // These are toolbar/list anchors on the already indexed Extensions page, not settings.
        var navigationOnlyLinks = new[] { SettingsLinkIds.Extensions.Search, SettingsLinkIds.Extensions.More, SettingsLinkIds.Extensions.Providers };
        var expectedLinks = SettingsLinkResolver.Destinations.ToArray()
            .Where(destination => !destination.RequiresExtensionProvider && !navigationOnlyLinks.Contains(destination.LinkId))
            .Select(destination => destination.LinkId).ToArray();
        CollectionAssert.AreEquivalent(
            expectedLinks,
            indexedLinks,
            $"Missing: {string.Join(", ", expectedLinks.Except(indexedLinks))}. Unexpected: {string.Join(", ", indexedLinks.Except(expectedLinks))}.");
        Assert.IsTrue(entries.All(entry => !string.IsNullOrWhiteSpace(entry.Title) && !string.IsNullOrWhiteSpace(entry.Breadcrumb)));
        Assert.IsTrue(entries.All(entry => !string.IsNullOrWhiteSpace(entry.IconGlyph)));
        Assert.IsTrue(entries.All(entry => !string.IsNullOrWhiteSpace(entry.GroupName)));
        Assert.IsTrue(entries.All(entry => !string.IsNullOrWhiteSpace(entry.Category)));
        Assert.AreEqual(1, entries.Count(entry => entry.Destination!.SettingsPageTag == SettingsPageTags.Gallery));

        var results = new SettingsSearchCatalog(entries).Search("DOCK theme", _matcher);
        Assert.AreEqual(SettingsLinkIds.Dock.Theme, results[0].Destination!.SettingsLinkId);
        Assert.AreEqual("Dock › Appearance", results[0].Breadcrumb);
        Assert.AreEqual("Dock", results[0].GroupName);
        Assert.AreEqual("Settings", results[0].Category);
        Assert.AreEqual("Settings", entries.Single(entry => entry.Destination!.SettingsLinkId == SettingsLinkIds.Appearance.Interaction).Category);
        Assert.AreEqual("Page", entries.Single(entry => entry.Destination!.SettingsLinkId == SettingsLinkIds.General.Page).Category);
        Assert.AreEqual("Page", entries.Single(entry => entry.Destination!.SettingsPageTag == SettingsPageTags.Gallery).Category);
        Assert.AreEqual("Personalization › Interaction", entries.Single(entry => entry.Destination!.SettingsLinkId == SettingsLinkIds.Appearance.ShowAppDetails).Breadcrumb);
        Assert.AreEqual("Personalization", entries.Single(entry => entry.Destination!.SettingsLinkId == SettingsLinkIds.Appearance.Interaction).Breadcrumb);
        Assert.AreEqual("Pages", entries.Single(entry => entry.Destination!.SettingsLinkId == SettingsLinkIds.General.Page).GroupName);
        Assert.IsTrue(new SettingsSearchCatalog(entries).Search("background image", _matcher).Any(entry => entry.Destination!.SettingsLinkId == SettingsLinkIds.Appearance.Background));
    }

    [TestMethod]
    [DataRow("preview", SettingsLinkIds.Appearance.ShowAppDetails)]
    [DataRow("preview pane", SettingsLinkIds.Appearance.ShowAppDetails)]
    [DataRow("wallpaper", SettingsLinkIds.Appearance.Background)]
    [DataRow("dock theme", SettingsLinkIds.Dock.Theme)]
    [DataRow("dock dark", SettingsLinkIds.Dock.Theme)]
    [DataRow("recent items", SettingsLinkIds.General.RecentItems)]
    [DataRow("quick access shelf", SettingsLinkIds.Appearance.QuickAccessShelf)]
    [DataRow("compact", SettingsLinkIds.Appearance.CompactMode)]
    [DataRow("compact mode", SettingsLinkIds.Appearance.CompactMode)]
    [DataRow("full mode", SettingsLinkIds.Appearance.CompactMode)]
    [DataRow("minimal", SettingsLinkIds.Appearance.CompactMode)]
    [DataRow("collapsed", SettingsLinkIds.Appearance.CompactMode)]
    [DataRow("vertical search box position", SettingsLinkIds.Appearance.CompactPosition)]
    [DataRow("offset", SettingsLinkIds.Appearance.CompactPosition)]
    [DataRow("alt+number shortcuts in lists", SettingsLinkIds.Appearance.ListItemAltNumberBehavior)]
    public void Search_RanksExpectedSettingFirst(string query, string expectedLink)
    {
        var entries = SettingsSearchCatalog.CreateEntries(LoadResources());

        Assert.AreEqual(expectedLink, new SettingsSearchCatalog(entries).Search(query, _matcher)[0].Destination!.SettingsLinkId);
    }

    [TestMethod]
    [DataRow("monitor")]
    [DataRow("cursor")]
    public void Search_KeepsLaunchPositionTermsOffCompactPosition(string query)
    {
        var results = new SettingsSearchCatalog(SettingsSearchCatalog.CreateEntries(LoadResources())).Search(query, _matcher);

        Assert.IsTrue(results.Any(entry => entry.Destination!.SettingsLinkId == SettingsLinkIds.Appearance.LaunchPosition));
        Assert.IsFalse(results.Any(entry => entry.Destination!.SettingsLinkId == SettingsLinkIds.Appearance.CompactPosition));
    }

    [TestMethod]
    [DataRow(SettingsLinkIds.General.RecentItems, "General")]
    [DataRow(SettingsLinkIds.Appearance.HomeRecentCommands, "General › Recent items")]
    [DataRow(SettingsLinkIds.Appearance.RecentCommandsDisplayLimit, "General › Recent items")]
    [DataRow(SettingsLinkIds.Appearance.ClearRecentCommands, "General › Recent items")]
    [DataRow(SettingsLinkIds.Appearance.QuickAccessShelf, "Personalization › Layout and positioning")]
    [DataRow(SettingsLinkIds.Appearance.CompactPosition, "Personalization › Layout and positioning")]
    [DataRow(SettingsLinkIds.Appearance.ListItemAltNumberBehavior, "Personalization › Interaction")]
    public void Catalog_UsesCurrentSettingsLocation(string linkId, string expectedBreadcrumb)
    {
        var entry = SettingsSearchCatalog.CreateEntries(LoadResources()).Single(entry => entry.Destination!.SettingsLinkId == linkId);

        Assert.AreEqual(expectedBreadcrumb, entry.Breadcrumb);
    }

    [TestMethod]
    [DataRow("preview", SettingsLinkIds.Appearance.ShowAppDetails)]
    [DataRow("APP PREVIEW", SettingsLinkIds.Appearance.ShowAppDetails)]
    [DataRow("preview pane", SettingsLinkIds.Appearance.ShowAppDetails)]
    [DataRow("wallpaper", SettingsLinkIds.Appearance.Background)]
    [DataRow("hotkey", SettingsLinkIds.General.ActivationKey)]
    [DataRow("clear history", SettingsLinkIds.Appearance.ClearRecentCommands)]
    [DataRow("dock dark", SettingsLinkIds.Dock.Theme)]
    [DataRow("plugins", SettingsLinkIds.Extensions.Page)]
    [DataRow("bckgrnd", SettingsLinkIds.Appearance.Background)]
    [DataRow("dck thm", SettingsLinkIds.Dock.Theme)]
    [DataRow("thm dck", SettingsLinkIds.Dock.Theme)]
    [DataRow("prvw", SettingsLinkIds.Appearance.ShowAppDetails)]
    public void Search_FindsLocalizedAlternativeTerms(string query, string expectedLink)
    {
        var entries = SettingsSearchCatalog.CreateEntries(LoadResources());

        Assert.IsTrue(new SettingsSearchCatalog(entries).Search(query, _matcher).Any(entry => entry.Destination!.SettingsLinkId == expectedLink));
    }

    [TestMethod]
    public void Suggestions_LimitMatchesAndOmitFooterWhenEmpty()
    {
        var entries = Enumerable.Range(0, 8)
            .Select(index => new SettingsSearchResult($"Setting {index}", "General", new(SettingsLinkId: SettingsLinkIds.General.Page)))
            .ToArray();
        var results = new SettingsSearchCatalog(entries).Search("setting", _matcher);
        var suggestions = SettingsSearchCatalog.CreateSuggestions(results, "Show all {0} results", "No results found");

        Assert.AreEqual(8, results.Length);
        Assert.AreEqual(6, suggestions.Length);
        CollectionAssert.AreEqual(results.Take(5).ToArray(), suggestions.Take(5).ToArray());
        Assert.IsTrue(suggestions[^1].IsShowAllResults);
        Assert.IsNull(suggestions[^1].Destination);
        Assert.AreEqual("Show all 8 results", suggestions[^1].ToString());

        var singleSuggestion = SettingsSearchCatalog.CreateSuggestions([results[0]], "Show {0} result", "No results found");
        Assert.AreEqual(2, singleSuggestion.Length);
        Assert.AreEqual("Show 1 result", singleSuggestion[^1].Title);

        var localizedSuggestions = SettingsSearchCatalog.CreateSuggestions(results, "Alle {0} Ergebnisse anzeigen", "Keine Ergebnisse");
        Assert.AreEqual("Alle 8 Ergebnisse anzeigen", localizedSuggestions[^1].Title);

        var emptySuggestions = SettingsSearchCatalog.CreateSuggestions([], "Show all {0} results", "No results found");
        Assert.AreEqual(1, emptySuggestions.Length);
        Assert.AreEqual("No results found", emptySuggestions[0].Title);
        Assert.IsNull(emptySuggestions[0].Destination);
        Assert.IsFalse(emptySuggestions[0].IsShowAllResults);
    }

    [TestMethod]
    public void Search_RanksTitlesBeforeBreadcrumbsAndKeepsProviderIdentity()
    {
        var provider = new SettingsSearchResult("Theme", "Extensions", new(SettingsLinkId: SettingsLinkIds.Extensions.Extension.Page, ExtensionProviderId: "com.example.provider"));
        SettingsSearchResult[] entries =
        [
            new("Colors", "Theme", new(SettingsLinkId: SettingsLinkIds.Appearance.Background)),
            new("App theme", "Personalization", new(SettingsLinkId: SettingsLinkIds.Appearance.Theme)),
            new("Theme mode", "Dock", new(SettingsLinkId: SettingsLinkIds.Dock.Theme)),
            provider,
            new("Alternate styling", "Appearance", new(SettingsLinkId: SettingsLinkIds.Appearance.Visuals), Keywords: "theme"),
            new("Thermal management", "General", new(SettingsLinkId: SettingsLinkIds.General.Page)),
            new("The menu", "General", new(SettingsLinkId: SettingsLinkIds.General.Page)),
        ];

        CollectionAssert.AreEqual(new[] { provider, entries[2], entries[1], entries[4], entries[0], entries[6], entries[5] }, new SettingsSearchCatalog(entries).Search("theme", _matcher));
        Assert.AreSame(provider, new SettingsSearchCatalog(entries).Search("theme extensions", _matcher).Single());
        Assert.AreEqual("com.example.provider", new SettingsSearchCatalog(entries).Search("theme", _matcher)[0].Destination!.ExtensionProviderId);
        Assert.AreEqual(0, new SettingsSearchCatalog(entries).Search(" \t\r\n", _matcher).Length);
        Assert.AreEqual(0, new SettingsSearchCatalog(entries).Search("no matching setting", _matcher).Length);
    }

    [TestMethod]
    [DataRow("preview")]
    [DataRow("dock preview")]
    [DataRow("preview dock")]
    public void Search_RanksExactAliasesBeforeIncidentalMatches(string query)
    {
        SettingsSearchResult[] entries =
        [
            new("A setting", "Dock › Preview", new(SettingsLinkId: SettingsLinkIds.Dock.AppearanceSection)),
            new("B setting", "Dock", new(SettingsLinkId: SettingsLinkIds.Dock.Background), Description: "preview"),
            new("C setting", "Dock", new(SettingsLinkId: SettingsLinkIds.Dock.Theme), Keywords: "previews"),
            new("Z setting", "Dock", new(SettingsLinkId: SettingsLinkIds.Dock.Size), Keywords: "pane PREVIEW details"),
        ];

        var results = new SettingsSearchCatalog(entries).Search(query, _matcher);
        Assert.AreSame(entries[3], results[0]);
        CollectionAssert.AreEquivalent(entries, results);
    }

    [TestMethod]
    public void Groups_KeepRankedOrderAndProviderDestinations()
    {
        SettingsSearchResult[] results =
        [
            new("Theme", "Dock › Appearance", new(SettingsLinkId: SettingsLinkIds.Dock.Theme), GroupName: "Dock"),
            new("Colors", "Extensions", new(SettingsLinkId: SettingsLinkIds.Extensions.Extension.Page, ExtensionProviderId: "first"), GroupName: "Extensions"),
            new("General", "Settings", new(SettingsLinkId: SettingsLinkIds.General.Page), GroupName: "Pages"),
            new("Background", "Dock › Appearance", new(SettingsLinkId: SettingsLinkIds.Dock.Background), GroupName: "Dock"),
            new("Colors", "Extensions", new(SettingsLinkId: SettingsLinkIds.Extensions.Extension.Page, ExtensionProviderId: "second"), GroupName: "Extensions"),
        ];

        var groups = SettingsSearchCatalog.GroupResults(results);
        string[] expectedGroups = ["Dock", "Extensions", "Pages"];
        CollectionAssert.AreEqual(expectedGroups, groups.Select(group => group.Title).ToArray());
        CollectionAssert.AreEqual(new[] { results[0], results[3] }, groups[0].Results);
        Assert.AreSame(results[1], groups[1].Results[0]);
        Assert.AreSame(results[4], groups[1].Results[1]);
        CollectionAssert.AreEquivalent(results, groups.SelectMany(group => group.Results).ToArray());
        Assert.AreEqual(0, SettingsSearchCatalog.GroupResults([]).Length);
    }

    [TestMethod]
    public void Search_DoesNotFuzzyMatchDescriptionsOrAcrossAliases()
    {
        var entry = new SettingsSearchResult("Zoom", "View", new(SettingsLinkId: SettingsLinkIds.General.Page), Description: "axbycz", Keywords: "apricot banana cherry");
        SettingsSearchResult[] entries = [entry, new("abc", "View", null)];

        Assert.AreEqual(0, new SettingsSearchCatalog(entries).Search("abc", _matcher).Length);
        Assert.AreSame(entry, new SettingsSearchCatalog(entries).Search("axbycz", _matcher).Single());
        Assert.AreSame(entry, new SettingsSearchCatalog(entries).Search("aprc axbycz", _matcher).Single());
        Assert.AreEqual(0, new SettingsSearchCatalog(entries).Search("aprc zqx", _matcher).Length);
    }

    [TestMethod]
    [DataRow("calc")]
    [DataRow("=")]
    [DataRow("math tool")]
    [DataRow("ctrl+alt+c")]
    [DataRow("Alt + Control + C")]
    [DataRow("ctrl alt c")]
    public void Search_FindsCommandByNameAliasOrAssignedHotkey(string query)
    {
        var destination = new OpenSettingsMessage(SettingsLinkId: SettingsLinkIds.Extensions.Extension.Commands, ExtensionProviderId: "calculator.provider") { CommandId = "calculator.command" };
        var command = new SettingsSearchResult("Calculator", "Extensions › Calculator › Commands", destination, Keywords: "= math tool", GroupName: "Commands", AssignedHotkey: "Ctrl + Alt + C");

        var result = new SettingsSearchCatalog([command]).Search(query, _matcher).Single();
        Assert.AreSame(command, result);
        Assert.AreSame(destination, result.Destination);
        Assert.AreEqual("calculator.command", result.Destination!.CommandId);
    }

    [TestMethod]
    public void Search_MatchesAllAssignedHotkeyKeysWithoutFuzzyMatching()
    {
        var destination = new OpenSettingsMessage(SettingsLinkId: SettingsLinkIds.Extensions.Extension.Commands, ExtensionProviderId: "provider") { CommandId = "command" };
        SettingsSearchResult[] entries =
        [
            new("Extra modifier", "Extensions", destination, AssignedHotkey: "Win + Ctrl + Shift + P"),
            new("Other key", "Extensions", destination, AssignedHotkey: "Win + Ctrl + O"),
            new("No shortcut", "Extensions", destination),
            new("Matching shortcut", "Extensions", destination, AssignedHotkey: "Win + Ctrl + P"),
        ];

        Assert.AreSame(entries[3], new SettingsSearchCatalog(entries).Search("Control + Windows + P", _matcher).Single());
        Assert.AreEqual(0, new SettingsSearchCatalog(entries).Search("ctl+win+p", _matcher).Length);
        Assert.AreEqual(0, new SettingsSearchCatalog(entries).Search("ctrl+p", _matcher).Length);
        Assert.AreEqual(0, new SettingsSearchCatalog([entries[3] with { AssignedHotkey = string.Empty }]).Search("win+ctrl+p", _matcher).Length);
    }

    [TestMethod]
    public void Search_UsesProvidedDiacriticsAndPinyinSettings()
    {
        var provider = new FuzzyMatcherProvider(new PrecomputedFuzzyMatcherOptions { RemoveDiacritics = false });
        var extension = new SettingsSearchResult("Café", "Extensions", new(SettingsLinkId: SettingsLinkIds.Extensions.Extension.Page, ExtensionProviderId: "com.example.cafe"));
        var localized = new SettingsSearchResult("北京", "Extensions", new(SettingsLinkId: SettingsLinkIds.General.Page));
        SettingsSearchResult[] entries = [extension, localized];
        var catalog = new SettingsSearchCatalog(entries);

        Assert.AreEqual(0, catalog.Search("cafe", provider.Current).Length);
        Assert.AreEqual(0, catalog.Search("beijing", provider.Current).Length);

        provider.UpdateSettings(new PrecomputedFuzzyMatcherOptions(), new PinyinFuzzyMatcherOptions { Mode = PinyinMode.On });

        Assert.AreSame(extension, catalog.Search("cafe", provider.Current).Single());
        Assert.AreSame(localized, catalog.Search("beijing", provider.Current).Single());
    }

    [TestMethod]
    [DataRow("Ctrl + +", "ctrl++", "ctrl")]
    [DataRow("Ctrl + Num +", "control num +", "ctrl num")]
    [DataRow("+", "+", "ctrl")]
    public void Search_PreservesPlusKeys(string hotkey, string matchingQuery, string missingKeyQuery)
    {
        var command = new SettingsSearchResult("Example", "Extensions", new(SettingsLinkId: SettingsLinkIds.General.Page), AssignedHotkey: hotkey);
        var catalog = new SettingsSearchCatalog([command]);

        Assert.AreSame(command, catalog.Search(matchingQuery, _matcher).Single());
        Assert.IsEmpty(catalog.Search(missingKeyQuery, _matcher));
    }

    [TestMethod]
    public void Search_ReusesStaticTargetsUntilMatcherSchemaChanges()
    {
        var provider = new FuzzyMatcherProvider(new PrecomputedFuzzyMatcherOptions { RemoveDiacritics = false });
        var matcher = new CountingMatcher(provider.Current);
        var catalog = new SettingsSearchCatalog(SettingsSearchCatalog.CreateEntries(LoadResources()));

        catalog.Search("zzzzz", matcher);
        var initialCount = matcher.TargetCount;
        Assert.IsTrue(initialCount > 0);
        catalog.Search("zzzzzx", matcher);
        Assert.AreEqual(initialCount, matcher.TargetCount);

        provider.UpdateSettings(new PrecomputedFuzzyMatcherOptions(), new PinyinFuzzyMatcherOptions { Mode = PinyinMode.On });
        matcher.Matcher = provider.Current;
        catalog.Search("zzzzzx", matcher);
        Assert.AreEqual(initialCount * 2, matcher.TargetCount);
    }

    [TestMethod]
    public void Search_RanksFreshDynamicEntriesTogetherWithStaticEntries()
    {
        var page = new SettingsSearchResult("Theme", "Settings", new(SettingsLinkId: SettingsLinkIds.Appearance.Page));
        var setting = new SettingsSearchResult("App theme", "Personalization", new(SettingsLinkId: SettingsLinkIds.Appearance.Theme));
        var extension = new SettingsSearchResult("Theme editor", "Extensions", new(SettingsLinkId: SettingsLinkIds.Extensions.Extension.Page, ExtensionProviderId: "theme.provider"));
        var catalog = new SettingsSearchCatalog([page, setting]);

        CollectionAssert.AreEqual(new[] { page, extension, setting }, catalog.Search("theme", _matcher, [extension]));

        var renamed = extension with { Title = "Theme" };
        CollectionAssert.AreEqual(new[] { renamed, page, setting }, catalog.Search("theme", _matcher, [renamed, renamed with { Destination = null }]));
        CollectionAssert.AreEqual(new[] { page, setting }, catalog.Search("theme", _matcher));
    }

    [TestMethod]
    public async Task Catalog_BuildsProviderAndCommandEntriesFromCurrentSettings()
    {
        var getString = LoadResources();
        var settings = new SettingsModel();
        var settingsService = new Mock<ISettingsService>();
        settingsService.SetupGet(service => service.Settings).Returns(() => settings);
        settingsService.Setup(service => service.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()))
            .Callback<Func<SettingsModel, SettingsModel>, bool>((update, _) => settings = update(settings));
        using var services = new ServiceCollection().AddSingleton(settingsService.Object).BuildServiceProvider();
        var item = new CommandItem(new NoOpCommand { Id = "calculator.command", Name = "Calculator" });
        string[] providerNames = ["Apps", "Bookmarks", "Calculator", "Clipboard", "DevTools"];
        var providers = providerNames
            .Select(name => new CommandProviderWrapper(new SearchTestProvider(name, name == "Calculator" ? [item] : []), TaskScheduler.Default)).ToArray();
        foreach (var provider in providers)
        {
            await provider.LoadTopLevelCommands(services);
        }

        try
        {
            var hotkey = new HotkeySettings(false, true, true, false, 'C');
            settings = settings with
            {
                Aliases = settings.Aliases.Add("=", new("=", "calculator.command")).Add("math", new("math tool", "calculator.command")),
                CommandHotkeys = [new(hotkey, "calculator.command")],
            };
            var dynamicEntries = SettingsSearchCatalog.CreateExtensionEntries(providers, settings, getString);
            var catalog = new SettingsSearchCatalog(SettingsSearchCatalog.CreateEntries(getString));
            Assert.IsTrue(catalog.Search("plugins", _matcher, dynamicEntries).Take(5).Any(result => result.Destination!.SettingsLinkId == SettingsLinkIds.Extensions.Page));
            Assert.AreEqual(5, dynamicEntries.Count(result => result.Category == "Extension"));
            Assert.IsTrue(dynamicEntries.Where(result => result.Category == "Extension").All(result => result.Keywords.Length == 0 && result.GroupName == "Extensions"));
            var command = dynamicEntries.Single(result => result.Category == "Command");
            Assert.AreEqual("Extensions › Calculator › Commands", command.Breadcrumb);
            Assert.AreEqual("Commands", command.GroupName);
            Assert.AreSame(providers[2].TopLevelItems[0].IconViewModel, command.Icon);
            Assert.AreEqual("Calculator", command.Destination!.ExtensionProviderId);
            Assert.AreEqual("calculator.command", command.Destination.CommandId);
            Assert.AreEqual(SettingsLinkIds.Extensions.Extension.Commands, command.Destination.SettingsLinkId);
            foreach (var query in new[] { "=", "math tool", hotkey.ToString() })
            {
                Assert.AreSame(command, catalog.Search(query, _matcher, dynamicEntries)[0]);
            }

            settings = settings with
            {
                Aliases = settings.Aliases.Clear().Add("new", new("new alias", "calculator.command")),
                CommandHotkeys = [new(hotkey, "another.command")],
            };
            var refreshedEntries = SettingsSearchCatalog.CreateExtensionEntries(providers, settings, getString);
            var refreshed = refreshedEntries.Single(result => result.Category == "Command");
            Assert.AreEqual("new alias", refreshed.Keywords);
            Assert.AreEqual(string.Empty, refreshed.AssignedHotkey);
            Assert.IsFalse(catalog.Search("math tool", _matcher, refreshedEntries).Contains(refreshed));
            Assert.IsFalse(catalog.Search(hotkey.ToString(), _matcher, refreshedEntries).Contains(refreshed));
            Assert.AreSame(refreshed, catalog.Search("new alias", _matcher, refreshedEntries)[0]);

            var renamed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var commandViewModel = providers[2].TopLevelItems[0];
            void OnRenamed(object sender, IPropChangedEventArgs args)
            {
                if (args.PropertyName == nameof(TopLevelViewModel.Title))
                {
                    renamed.TrySetResult();
                }
            }

            commandViewModel.PropChanged += OnRenamed;
            try
            {
                item.Title = "Renamed command";
                await renamed.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var renamedEntries = SettingsSearchCatalog.CreateExtensionEntries(providers, settings, getString);
                var renamedEntry = renamedEntries.Single(result => result.Category == "Command");
                Assert.AreEqual("Renamed command", renamedEntry.Title);
                Assert.AreEqual(command.Destination, renamedEntry.Destination);
                Assert.IsTrue(catalog.Search("rnmd", _matcher, renamedEntries).Contains(renamedEntry));
            }
            finally
            {
                commandViewModel.PropChanged -= OnRenamed;
            }
        }
        finally
        {
            foreach (var command in providers.SelectMany(provider => provider.TopLevelItems))
            {
                command.Cleanup();
            }
        }
    }

    private static Func<string, string> LoadResources()
    {
        var resources = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "SettingsPages", "Resources.resw"))
            .Root!.Elements("data").ToDictionary(element => (string)element.Attribute("name")!, element => (string)element.Element("value")!);
        return id => resources[id];
    }

    private sealed partial class SearchTestProvider : CommandProvider
    {
        private readonly ICommandItem[] _items;

        public SearchTestProvider(string name, ICommandItem[] items)
        {
            Id = name;
            DisplayName = name;
            _items = items;
        }

        public override ICommandItem[] TopLevelCommands() => _items;
    }

    private sealed class CountingMatcher(IPrecomputedFuzzyMatcher matcher) : IPrecomputedFuzzyMatcher
    {
        public IPrecomputedFuzzyMatcher Matcher { get; set; } = matcher;

        public int TargetCount { get; private set; }

        public uint SchemaId => Matcher.SchemaId;

        public FuzzyQuery PrecomputeQuery(string? input) => Matcher.PrecomputeQuery(input);

        public FuzzyTarget PrecomputeTarget(string? input)
        {
            TargetCount++;
            return Matcher.PrecomputeTarget(input);
        }

        public int Score(scoped in FuzzyQuery query, scoped in FuzzyTarget target) => Matcher.Score(query, target);
    }
}
