// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    /// <summary>
    /// What the snippet list's code relies on in its markup, checked in the page's XAML because
    /// the page itself can't be built in a unit test.
    /// </summary>
    [TestClass]
    public class TextExpanderPageMarkup
    {
        private const string Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        private static readonly string[] RowHandlers = { "EditSnippet_Click", "EditSnippet_Click", "DuplicateSnippet_Click", "DeleteSnippet_Click" };
        private static readonly string[] MenuOrder = { "TextExpander_EditSnippet", "TextExpander_DuplicateSnippet", "TextExpander_DeleteSnippet" };

        [TestMethod]
        public void EverythingClickableInARowCarriesItsSnippetInItsTag()
        {
            XElement row = LoadRowTemplate();

            // ItemsRepeater sets no DataContext on rows built with x:Bind, so a handler can only find
            // the row's snippet in Tag. Selecting a row did nothing while the row relied on DataContext.
            var clickable = row.DescendantsAndSelf().Where(element => element.Attribute("Click") != null).ToList();
            CollectionAssert.AreEquivalent(RowHandlers, clickable.Select(element => (string)element.Attribute("Click")).ToArray());
            foreach (XElement element in clickable)
            {
                Assert.AreEqual("{x:Bind}", (string)element.Attribute("Tag"), $"{element.Name.LocalName} ({(string)element.Attribute("Click")})");
            }

            // {Binding} reads DataContext as well, which these rows don't have.
            Assert.IsFalse(row.DescendantsAndSelf().Attributes().Any(attribute => attribute.Value.Contains("{Binding", StringComparison.Ordinal)));
        }

        [TestMethod]
        public void RowsShowAPencilAndEditComesFirstInMoreOptions()
        {
            XElement row = LoadRowTemplate();
            Assert.AreEqual("SettingsCard", row.Name.LocalName);
            Assert.AreEqual("EditSnippet_Click", (string)row.Attribute("Click"));
            StringAssert.Contains((string)row.Attribute("ActionIcon"), "\uE70F", "A pencil instead of the chevron, so it's clear that selecting a row edits it.");

            XElement menu = row.Descendants().Single(element => element.Name.LocalName == "MenuFlyout");
            CollectionAssert.AreEqual(MenuOrder, menu.Elements().Select(item => (string)item.Attribute(XName.Get("Uid", Xaml))).ToArray());
        }

        [TestMethod]
        public void AttributionUsesTheCurrentPublicWebsite()
        {
            XElement link = LoadPage().Descendants()
                .Single(element => element.Name.LocalName == "PageLink"
                    && (string)element.Attribute(XName.Get("Uid", Xaml)) == "Attribution_TextExpander");

            Assert.AreEqual("https://howdygary.github.io/text-expander/", (string)link.Attribute("Link"));
        }

        private static XElement LoadRowTemplate()
        {
            XElement list = LoadPage().Descendants()
                .Single(element => element.Name.LocalName == "ItemsRepeater" && ((string)element.Attribute("ItemsSource") ?? string.Empty).Contains("FilteredSnippets", StringComparison.Ordinal));
            return list.Descendants().Single(element => element.Name.LocalName == "DataTemplate").Elements().Single();
        }

        private static XDocument LoadPage()
        {
            string page = FindPage();
            if (page == null)
            {
                Assert.Inconclusive("The page's XAML isn't next to the tests here.");
            }

            return XDocument.Load(page);
        }

        private static string FindPage()
        {
            for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
            {
                string page = Path.Combine(folder.FullName, "src", "settings-ui", "Settings.UI", "SettingsXAML", "Views", "TextExpanderPage.xaml");
                if (File.Exists(page))
                {
                    return page;
                }
            }

            return null;
        }
    }
}
