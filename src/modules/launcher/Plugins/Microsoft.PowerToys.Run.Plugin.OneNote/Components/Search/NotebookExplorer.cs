// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using LinqToOneNote;
using LinqToOneNote.Abstractions;
using Microsoft.PowerToys.Run.Plugin.OneNote.Properties;
using Wox.Plugin;
using OneNoteApplication = LinqToOneNote.OneNote;

namespace Microsoft.PowerToys.Run.Plugin.OneNote.Components.Search
{
    public class NotebookExplorer : SearchBase
    {
        private readonly TitleSearch _titleSearch;
        private static readonly CompositeFormat OpenXInOneNote = CompositeFormat.Parse(Resources.OpenXInOneNote);
        private static readonly CompositeFormat SearchingByTitleInX = CompositeFormat.Parse(Resources.SearchingByTitleInX);
        private static readonly CompositeFormat SearchingPagesInX = CompositeFormat.Parse(Resources.SearchingPagesInX);
        private static readonly CompositeFormat SearchInItemInfo = CompositeFormat.Parse(Resources.SearchInItemInfo);

        public NotebookExplorer(ResultCreator resultCreator, OneNoteSettings settings, TitleSearch titleSearch)
            : base(Keywords.NotebookExplorer, resultCreator, settings)
        {
            _titleSearch = titleSearch;
        }

        private string ScopeSearchKeyword => Keywords.ScopedSearch;

        public override List<Result> GetResults(string search, bool showSingleResults)
        {
            var results = new List<Result>();

            string fullSearch = search[(search.IndexOf(Keywords.NotebookExplorer, StringComparison.Ordinal) + Keywords.NotebookExplorer.Length)..];

            IOneNoteItem? parent = null;
            IEnumerable<IOneNoteItem> collection = OneNoteApplication.GetFullHierarchy().Notebooks;

            string[] searches = fullSearch.Split(Keywords.NotebookExplorerSeparator, StringSplitOptions.None);

            for (int i = -1; i < searches.Length - 1; i++)
            {
                if (i < 0)
                {
                    continue;
                }

                parent = collection.FirstOrDefault(item => item.Name.Equals(searches[i], StringComparison.Ordinal));
                if (parent == null)
                {
                    return results;
                }

                collection = parent.Children;
            }

            string lastSearch = searches[^1];

            results = lastSearch switch
            {
                // Empty search so show all in collection
                string s when string.IsNullOrWhiteSpace(s) => EmptySearch(parent, collection),

                // Search by title
                string s when s.StartsWith(_titleSearch.Keyword, StringComparison.Ordinal) && parent is not Page
                    => _titleSearch.Filter(s, parent, collection, showSingleResults),

                // Scoped search
                string s when s.StartsWith(ScopeSearchKeyword, StringComparison.Ordinal) && (parent is Notebook || parent is SectionGroup)
                    => ScopedSearch(s, parent, showSingleResults),

                // Default notebook explorer functionality
                _ => Explorer(lastSearch, parent, collection),
            };

            // parent is null if items in the collection are notebooks.
            if (parent != null)
            {
                // This result is a shortcut to opening the current parent the user is looking in.
                var result = ResultCreator.CreateOneNoteItemResult(parent, false, score: 4000);
                result.Title = string.Format(CultureInfo.CurrentCulture, OpenXInOneNote, parent.Name);
                result.SubTitle = lastSearch switch
                {
                    string s when s.StartsWith(_titleSearch.Keyword, StringComparison.Ordinal)
                        => string.Format(CultureInfo.CurrentCulture, SearchingByTitleInX, parent.Name),

                    string s when s.StartsWith(ScopeSearchKeyword, StringComparison.Ordinal)
                        => string.Format(CultureInfo.CurrentCulture, SearchingPagesInX, parent.Name),

                    _ => string.Format(CultureInfo.CurrentCulture, SearchInItemInfo, ScopeSearchKeyword, _titleSearch.Keyword),
                };

                results.Add(result);
            }

            return results;
        }

        private List<Result> EmptySearch(IOneNoteItem? parent, IEnumerable<IOneNoteItem> collection)
        {
            List<Result> results = collection.Where(SettingsCheck)
                                             .Select(item => ResultCreator.CreateOneNoteItemResult(item, true))
                                             .ToList();

            return results.Count == 0 ? ResultCreator.NoItemsInCollection(parent, results) : results;
        }

        private List<Result> ScopedSearch(string query, IOneNoteItem parent, bool showSingleResults)
        {
            if (query.Length == ScopeSearchKeyword.Length)
            {
                return ResultCreator.NoMatchesFound(showSingleResults);
            }

            if (!char.IsLetterOrDigit(query[ScopeSearchKeyword.Length]))
            {
                return ResultCreator.InvalidQuery(showSingleResults);
            }

            string currentSearch = query[_titleSearch.Keyword.Length..];
            var results = new List<Result>();

            results = OneNoteApplication.FindPages(currentSearch, parent)
                                        .Select(pg => ResultCreator.CreatePageResult(pg, currentSearch))
                                        .ToList();

            if (results.Count == 0)
            {
                results = ResultCreator.NoMatchesFound(showSingleResults);
            }

            return results;
        }

        private List<Result> Explorer(string search, IOneNoteItem? parent, IEnumerable<IOneNoteItem> collection)
        {
            List<int>? highlightData = null;
            int score = 0;

            var results = collection.Where(item => SettingsCheck(item) && FuzzySearch(item.Name, search, out highlightData, out score))
                                    .Select(item => ResultCreator.CreateOneNoteItemResult(item, true, highlightData, score))
                                    .ToList();

            AddCreateNewOneNoteItemResults(search, parent, results);
            return results;
        }

        private void AddCreateNewOneNoteItemResults(string newItemName, IOneNoteItem? parent, List<Result> results)
        {
            if (results.Any(result => string.Equals(newItemName.Trim(), result.Title, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            if (parent?.IsInRecycleBin() == true)
            {
                return;
            }

            switch (parent)
            {
                case null:
                    results.Add(ResultCreator.CreateNewNotebookResult(newItemName));
                    break;
                case INotebookOrSectionGroup notebookOrSectionGroup:
                    results.Add(ResultCreator.CreateNewSectionResult(newItemName, notebookOrSectionGroup));
                    results.Add(ResultCreator.CreateNewSectionGroupResult(newItemName, notebookOrSectionGroup));
                    break;
                case Section section when !section.Locked:
                    results.Add(ResultCreator.CreateNewPageResult(newItemName, section));
                    break;
                default:
                    break;
            }
        }
    }
}
