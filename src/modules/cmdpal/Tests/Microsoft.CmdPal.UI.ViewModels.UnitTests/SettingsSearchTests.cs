// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class SettingsSearchTests
{
    private readonly IPrecomputedFuzzyMatcher _matcher = new FuzzyMatcherProvider(new PrecomputedFuzzyMatcherOptions()).Current;

    [TestMethod]
    public void Catalog_UsesExistingLabelsAndCoversSettingsDestinations()
    {
        var resources = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "SettingsPages", "Resources.resw"))
            .Root!.Elements("data").ToDictionary(element => (string)element.Attribute("name")!, element => (string)element.Element("value")!);
        var entries = SettingsSearchCatalog.CreateEntries(id => resources[id]);
        var indexedLinks = entries.Select(entry => entry.Destination!.SettingsLinkId).Where(id => id is not null).ToArray();

        // These are toolbar/list anchors on the already indexed Extensions page, not settings.
        var navigationOnlyLinks = new[] { SettingsLinkIds.Extensions.Search, SettingsLinkIds.Extensions.More, SettingsLinkIds.Extensions.Providers };
        var expectedLinks = SettingsLinkResolver.Destinations.ToArray()
            .Where(destination => !destination.RequiresExtensionProvider && !navigationOnlyLinks.Contains(destination.LinkId))
            .Select(destination => destination.LinkId).ToArray();
        CollectionAssert.AreEquivalent(expectedLinks, indexedLinks);
        Assert.IsTrue(entries.All(entry => !string.IsNullOrWhiteSpace(entry.Title) && !string.IsNullOrWhiteSpace(entry.Breadcrumb)));
        Assert.IsTrue(entries.All(entry => !string.IsNullOrWhiteSpace(entry.IconGlyph)));
        Assert.IsTrue(entries.All(entry => !string.IsNullOrWhiteSpace(entry.GroupName)));
        Assert.IsTrue(entries.All(entry => !string.IsNullOrWhiteSpace(entry.Category)));
        Assert.AreEqual(1, entries.Count(entry => entry.Destination!.SettingsPageTag == SettingsPageTags.Gallery));

        var results = SettingsSearchCatalog.Search("DOCK theme", entries, _matcher);
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
        Assert.IsTrue(SettingsSearchCatalog.Search("background image", entries, _matcher).Any(entry => entry.Destination!.SettingsLinkId == SettingsLinkIds.Appearance.Background));
    }

    [TestMethod]
    [DataRow("preview", SettingsLinkIds.Appearance.ShowAppDetails)]
    [DataRow("preview pane", SettingsLinkIds.Appearance.ShowAppDetails)]
    [DataRow("wallpaper", SettingsLinkIds.Appearance.Background)]
    [DataRow("dock theme", SettingsLinkIds.Dock.Theme)]
    [DataRow("dock dark", SettingsLinkIds.Dock.Theme)]
    public void Search_RanksExpectedSettingFirst(string query, string expectedLink)
    {
        var resources = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "SettingsPages", "Resources.resw"))
            .Root!.Elements("data").ToDictionary(element => (string)element.Attribute("name")!, element => (string)element.Element("value")!);
        var entries = SettingsSearchCatalog.CreateEntries(id => resources[id]);

        Assert.AreEqual(expectedLink, SettingsSearchCatalog.Search(query, entries, _matcher)[0].Destination!.SettingsLinkId);
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
        var resources = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "SettingsPages", "Resources.resw"))
            .Root!.Elements("data").ToDictionary(element => (string)element.Attribute("name")!, element => (string)element.Element("value")!);
        var entries = SettingsSearchCatalog.CreateEntries(id => resources[id]);

        Assert.IsTrue(SettingsSearchCatalog.Search(query, entries, _matcher).Any(entry => entry.Destination!.SettingsLinkId == expectedLink));
    }

    [TestMethod]
    public void Suggestions_LimitMatchesAndOmitFooterWhenEmpty()
    {
        var entries = Enumerable.Range(0, 8)
            .Select(index => new SettingsSearchResult($"Setting {index}", "General", new(SettingsLinkId: SettingsLinkIds.General.Page)))
            .ToArray();
        var results = SettingsSearchCatalog.Search("setting", entries, _matcher);
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

        CollectionAssert.AreEqual(new[] { provider, entries[2], entries[1], entries[4], entries[0], entries[6], entries[5] }, SettingsSearchCatalog.Search("theme", entries, _matcher));
        Assert.AreSame(provider, SettingsSearchCatalog.Search("theme extensions", entries, _matcher).Single());
        Assert.AreEqual("com.example.provider", SettingsSearchCatalog.Search("theme", entries, _matcher)[0].Destination!.ExtensionProviderId);
        Assert.AreEqual(0, SettingsSearchCatalog.Search(" \t\r\n", entries, _matcher).Length);
        Assert.AreEqual(0, SettingsSearchCatalog.Search("no matching setting", entries, _matcher).Length);
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

        var results = SettingsSearchCatalog.Search(query, entries, _matcher);
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

        Assert.AreEqual(0, SettingsSearchCatalog.Search("abc", entries, _matcher).Length);
        Assert.AreSame(entry, SettingsSearchCatalog.Search("axbycz", entries, _matcher).Single());
        Assert.AreSame(entry, SettingsSearchCatalog.Search("aprc axbycz", entries, _matcher).Single());
        Assert.AreEqual(0, SettingsSearchCatalog.Search("aprc zqx", entries, _matcher).Length);
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

        var result = SettingsSearchCatalog.Search(query, [command], _matcher).Single();
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

        Assert.AreSame(entries[3], SettingsSearchCatalog.Search("Control + Windows + P", entries, _matcher).Single());
        Assert.AreEqual(0, SettingsSearchCatalog.Search("ctl+win+p", entries, _matcher).Length);
        Assert.AreEqual(0, SettingsSearchCatalog.Search("ctrl+p", entries, _matcher).Length);
        Assert.AreEqual(0, SettingsSearchCatalog.Search("win+ctrl+p", [entries[3] with { AssignedHotkey = string.Empty }], _matcher).Length);
    }

    [TestMethod]
    public void Search_UsesProvidedDiacriticsAndPinyinSettings()
    {
        var provider = new FuzzyMatcherProvider(new PrecomputedFuzzyMatcherOptions { RemoveDiacritics = false });
        var extension = new SettingsSearchResult("Café", "Extensions", new(SettingsLinkId: SettingsLinkIds.Extensions.Extension.Page, ExtensionProviderId: "com.example.cafe"));
        var localized = new SettingsSearchResult("北京", "Extensions", new(SettingsLinkId: SettingsLinkIds.General.Page));
        SettingsSearchResult[] entries = [extension, localized];

        Assert.AreEqual(0, SettingsSearchCatalog.Search("cafe", entries, provider.Current).Length);
        Assert.AreEqual(0, SettingsSearchCatalog.Search("beijing", entries, provider.Current).Length);

        provider.UpdateSettings(new PrecomputedFuzzyMatcherOptions(), new PinyinFuzzyMatcherOptions { Mode = PinyinMode.On });

        Assert.AreSame(extension, SettingsSearchCatalog.Search("cafe", entries, provider.Current).Single());
        Assert.AreSame(localized, SettingsSearchCatalog.Search("beijing", entries, provider.Current).Single());
    }
}
