// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Search.FuzzSearch;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.UnitTests.Services
{
    [TestClass]
    [DoNotParallelize]
    public class SettingsSearchTests
    {
        [TestMethod]
        public void LoadIndexFromJson_ReturnsEntries()
        {
            const string json = @"
[
  {
    ""type"": 0,
    ""header"": ""General"",
    ""pageTypeName"": ""GeneralPage"",
    ""elementName"": """",
    ""elementUid"": ""General_Page"",
    ""parentElementName"": """",
    ""description"": ""General settings"",
    ""icon"": """"
  },
  {
    ""type"": 1,
    ""header"": ""Mouse Utilities"",
    ""pageTypeName"": ""MouseUtilsPage"",
    ""elementName"": ""MouseUtilsSetting"",
    ""elementUid"": ""MouseUtils_Setting"",
    ""parentElementName"": """",
    ""description"": ""Adjust mouse settings"",
    ""icon"": """"
  }
]
";

            var entries = SettingsSearch.LoadIndexFromJson(json);

            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual(EntryType.SettingsPage, entries[0].Type);
            Assert.AreEqual("GeneralPage", entries[0].PageTypeName);
            Assert.AreEqual("MouseUtilsPage", entries[1].PageTypeName);
        }

        [TestMethod]
        public async Task InitializeIndexAsync_InitializesSearchAndReturnsResults()
        {
            var entries = new List<SettingEntry>
            {
                new(EntryType.SettingsPage, "General", "GeneralPage", string.Empty, "General_Page"),
                new(EntryType.SettingsCard, "Mouse Utilities", "MouseUtilsPage", "MouseUtilsSetting", "MouseUtils_Setting", description: "Adjust mouse settings"),
            };

            using var search = new SettingsSearch(new FuzzSearchEngine<SettingEntry>());
            await search.InitializeIndexAsync(entries);

            Assert.IsTrue(search.IsReady);

            var results = await search.SearchAsync("mouse", options: null);

            Assert.IsTrue(results.Count > 0);
            Assert.AreEqual("Mouse Utilities", results[0].Entry.Header);
            Assert.IsTrue(results[0].Score > 0);
            Assert.IsNotNull(results[0].MatchSpans);
        }
    }
}
