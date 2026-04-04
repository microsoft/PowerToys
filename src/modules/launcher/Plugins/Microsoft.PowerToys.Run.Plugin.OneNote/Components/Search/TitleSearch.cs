// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using LinqToOneNote;
using Wox.Plugin;
using OneNoteApplication = LinqToOneNote.OneNote;

namespace Microsoft.PowerToys.Run.Plugin.OneNote.Components.Search
{
    public class TitleSearch : SearchBase
    {
        public TitleSearch(ResultCreator resultCreator, OneNoteSettings settings)
            : base(Keywords.TitleSearch, resultCreator, settings)
        {
        }

        public override List<Result> GetResults(string search, bool showSingleResults)
        {
            return Filter(search, null, OneNoteApplication.GetFullHierarchy().Notebooks, showSingleResults);
        }

        public List<Result> Filter(string query, IOneNoteItem? parent, IEnumerable<IOneNoteItem> currentCollection, bool showSingleResults)
        {
            if (query.Length == Keyword.Length && parent == null)
            {
                return ResultCreator.SearchingByTitle();
            }

            List<int>? highlightData = null;
            int score = 0;

            var currentSearch = query[Keyword.Length..];

            var results = currentCollection.Descendants(item => SettingsCheck(item) && FuzzySearch(item.Name, currentSearch, out highlightData, out score))
                                           .Select(item => ResultCreator.CreateOneNoteItemResult(item, false, highlightData, score))
                                           .ToList();

            return results.Count != 0 ? results : ResultCreator.NoMatchesFound(showSingleResults);
        }
    }
}
