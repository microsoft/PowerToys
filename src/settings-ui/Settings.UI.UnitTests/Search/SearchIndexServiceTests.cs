// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Reflection;
using System.Threading;
using Common.Search.FuzzSearch;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Settings.UI.Library;

namespace Settings.UI.UnitTests.Search
{
    [TestClass]
    [DoNotParallelize]
    public class SearchIndexServiceTests
    {
        private ImmutableArray<SettingEntry> _originalIndex;
        private FieldInfo _indexField;

        [TestInitialize]
        public void Initialize()
        {
            _indexField = typeof(SearchIndexService).GetField("_index", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(_indexField);
            _originalIndex = SearchIndexService.Index;

            // Exercise a nonempty index without loading resources or populating the text/page caches.
            _indexField.SetValue(null, ImmutableArray.Create(new SettingEntry { Description = "PowerToys settings" }));
        }

        [TestCleanup]
        public void Cleanup()
        {
            _indexField.SetValue(null, _originalIndex);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow(" \t\r\n")]
        [DataRow("\u00A0\u2003\u3000")]
        [DataRow("\u00A8")]
        [DataRow("\u00A8\u00A8")]
        [DataRow("\u0301\u0308")]
        [DataRow(" \u0301 ")]
        public void Search_EmptyOrWhitespaceAfterNormalization_ReturnsNoResults(string query)
        {
            Assert.AreEqual(1, SearchIndexService.Index.Length);
            Assert.AreEqual(0, SearchIndexService.Search(query).Count);
            Assert.AreEqual(0, SearchIndexService.Search(query, CancellationToken.None).Count);
        }

        [TestMethod]
        [DataRow(null, "")]
        [DataRow("", "")]
        [DataRow("\u00A8", " ")]
        [DataRow("\u00A8\u00A8", "  ")]
        [DataRow("\u0301\u0308", "")]
        [DataRow(" \u0301 ", "  ")]
        [DataRow("\u00A0\u2003\u3000", "   ")]
        public void NormalizeString_EmptyOrWhitespaceResult_DoesNotMatch(string query, string expected)
        {
            var normalized = SearchIndexService.NormalizeString(query);
            var match = StringMatcher.FuzzyMatch(normalized, "PowerToys");

            Assert.AreEqual(expected, normalized);
            Assert.IsFalse(match.Success);
            Assert.AreEqual(0, match.Score);
        }

        [TestMethod]
        [DataRow("PowerToys", "powertoys")]
        [DataRow("CAF\u00C9", "cafe")]
        [DataRow("Cafe\u0301", "cafe")]
        [DataRow("\uFF30\uFF4F\uFF57\uFF45\uFF52", "power")]
        [DataRow("\u00A8PowerToys", " powertoys")]
        public void NormalizeString_NonemptyQuery_PreservesMatching(string query, string expected)
        {
            var normalized = SearchIndexService.NormalizeString(query);
            var match = StringMatcher.FuzzyMatch(normalized, expected.Trim());

            Assert.AreEqual(expected, normalized);
            Assert.IsTrue(match.Success);
            Assert.IsTrue(match.Score > 0);
        }
    }
}
