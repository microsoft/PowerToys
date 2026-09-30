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
        Assert.AreEqual(1, entries.Count(entry => entry.Destination!.SettingsPageTag == SettingsPageTags.Gallery));

        var results = SettingsSearchCatalog.Search("DOCK theme", entries, _matcher);
        Assert.AreEqual(SettingsLinkIds.Dock.Theme, results[0].Destination!.SettingsLinkId);
        Assert.AreEqual("Dock", results[0].Breadcrumb);
        Assert.IsTrue(SettingsSearchCatalog.Search("background image", entries, _matcher).Any(entry => entry.Destination!.SettingsLinkId == SettingsLinkIds.Appearance.Background));
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
        var suggestions = SettingsSearchCatalog.CreateSuggestions(results, "Show all results", "No results found");

        Assert.AreEqual(8, results.Length);
        Assert.AreEqual(6, suggestions.Length);
        CollectionAssert.AreEqual(results.Take(5).ToArray(), suggestions.Take(5).ToArray());
        Assert.IsTrue(suggestions[^1].IsShowAllResults);
        Assert.IsNull(suggestions[^1].Destination);
        Assert.AreEqual("Show all results", suggestions[^1].ToString());

        var emptySuggestions = SettingsSearchCatalog.CreateSuggestions([], "Show all results", "No results found");
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
