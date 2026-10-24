// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    /// <summary>
    /// Custom content in the page's InfoBars, checked in the page's XAML and code-behind because
    /// the page itself can't be built in a unit test.
    /// </summary>
    [TestClass]
    public class TextExpanderInfoBarMarkup
    {
        private const string Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        private static readonly string[] KnownContent = { "PrivacyNoticeBullets", "DiscardWarningButtons" };

        [TestMethod]
        public void CustomContentIsInsetLikeTheInfoBarsOwnMessage()
        {
            string page = FindPage();
            if (page == null)
            {
                Assert.Inconclusive("The page's XAML isn't next to the tests here.");
            }

            string code = File.ReadAllText(page + ".cs");
            var contents = XDocument.Load(page).Descendants()
                .Where(element => element.Name.LocalName == "InfoBar")
                .SelectMany(CustomContent)
                .ToList();
            var names = contents.Select(content => (string)content.Attribute(XName.Get("Name", Xaml))).ToList();

            // Both ways of giving an InfoBar content: as its child, and in InfoBar.Content.
            CollectionAssert.IsSubsetOf(KnownContent, names);

            // InfoBar insets its own title and message but not custom content, so the constructor
            // insets each one the same way, from the theme's InfoBar resources.
            foreach (XElement content in contents)
            {
                string name = (string)content.Attribute(XName.Get("Name", Xaml));
                Assert.IsFalse(string.IsNullOrEmpty(name), $"{content.Name.LocalName} in an InfoBar needs a name, so the constructor can inset it.");
                StringAssert.Contains(code, $"InsetLikeAnInfoBarMessage({name});");
                Assert.IsNull(content.Attribute("Margin"), $"{name}: the constructor sets its margin.");

                // WrapPanel adds HorizontalSpacing after the last item in a row too, so right-aligned
                // buttons would sit that much further in from the inset.
                Assert.IsNull(content.Attribute("HorizontalSpacing"), $"{name}: space the items with margins instead.");
            }
        }

        private static IEnumerable<XElement> CustomContent(XElement infoBar)
            => infoBar.Elements().SelectMany(child => child.Name.LocalName switch
            {
                "InfoBar.Content" => child.Elements(),
                _ when child.Name.LocalName.Contains('.', StringComparison.Ordinal) => Enumerable.Empty<XElement>(),
                _ => new[] { child },
            });

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
