// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using LinqToOneNote;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.OneNote.Components.Search
{
    public abstract class SearchBase
    {
        private readonly OneNoteSettings _settings;

        protected ResultCreator ResultCreator { get; }

        public string Keyword { get; }

        protected SearchBase(string keyword, ResultCreator resultCreator, OneNoteSettings settings)
        {
            Keyword = keyword;
            ResultCreator = resultCreator;
            _settings = settings;
        }

        public abstract List<Result> GetResults(string search, bool showSingleResults);

        protected static bool FuzzySearch(string itemName, string search, out List<int> highlightData, out int score)
        {
            var matchResult = StringMatcher.FuzzySearch(search, itemName);
            highlightData = matchResult.MatchData;
            score = matchResult.Score;
            return matchResult.IsSearchPrecisionScoreMet();
        }

        protected bool SettingsCheck(IOneNoteItem item)
        {
            bool success = true;
            if (!_settings.ShowEncryptedSections && item is Section section)
            {
                success = !section.Encrypted;
            }

            if (!_settings.ShowRecycleBins && item.IsInRecycleBin())
            {
                success = false;
            }

            return success;
        }
    }
}
