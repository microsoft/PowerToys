// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Odotocodot.OneNote.Linq;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.OneNote.Components
{
    public partial class SearchManager
    {
        private sealed class NotebookExplorer
        {
            private readonly SearchManager _searchManager;
            private readonly ResultCreator _resultCreator;

            internal NotebookExplorer(SearchManager searchManager, ResultCreator resultCreator)
            {
                _searchManager = searchManager;
                _resultCreator = resultCreator;
            }

            internal List<Result> Query(Query query)
            {
                var results = new List<Result>();

                string fullSearch = query.Search[(query.Search.IndexOf(Keywords.NotebookExplorer, StringComparison.Ordinal) + Keywords.NotebookExplorer.Length)..];

                IOneNoteItem? parent = null;
                IEnumerable<IOneNoteItem> collection = OneNoteApplication.GetNotebooks();

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
                    string search when string.IsNullOrWhiteSpace(search)
                        => EmptySearch(parent, collection),

                    // Search by title
                    string search when search.StartsWith(Keywords.TitleSearch, StringComparison.Ordinal) && parent is not OneNotePage
                        => _searchManager.TitleSearch(search, parent, collection),

                    // Scoped search
                    string search when search.StartsWith(Keywords.ScopedSearch, StringComparison.Ordinal) && (parent is OneNoteNotebook || parent is OneNoteSectionGroup)
                        => ScopedSearch(search, parent),

                    // Default search
                    _ => Explorer(lastSearch, parent, collection),
                };

                if (parent != null)
                {
                    var result = _resultCreator.CreateOneNoteItemResult(parent, false, score: 4000);
                    result.Title = $"Open \"{parent.Name}\" in OneNote";
                    result.SubTitle = lastSearch switch
                    {
                        string search when search.StartsWith(Keywords.TitleSearch, StringComparison.Ordinal)
                            => $"Now search by title in \"{parent.Name}\"",

                        string search when search.StartsWith(Keywords.ScopedSearch, StringComparison.Ordinal)
                            => $"Now searching all pages in \"{parent.Name}\"",

                        _ => $"Use \'{Keywords.ScopedSearch}\' to search this item. Use \'{Keywords.TitleSearch}\' to search by title in this item",
                    };

                    results.Add(result);
                }

                return results;
            }

            private List<Result> EmptySearch(IOneNoteItem? parent, IEnumerable<IOneNoteItem> collection)
            {
                List<Result> results = collection.Where(_searchManager.SettingsCheck)
                                                 .Select(item => _resultCreator.CreateOneNoteItemResult(item, true))
                                                 .ToList();

                return results.Count == 0 ? _resultCreator.NoItemsInCollection(parent, results) : results;
            }

            private List<Result> ScopedSearch(string query, IOneNoteItem parent)
            {
                if (query.Length == Keywords.ScopedSearch.Length)
                {
                    return _resultCreator.NoMatchesFound(_searchManager.showSingleResults);
                }

                if (!char.IsLetterOrDigit(query[Keywords.ScopedSearch.Length]))
                {
                    return _resultCreator.InvalidQuery(_searchManager.showSingleResults);
                }

                string currentSearch = query[Keywords.TitleSearch.Length..];
                var results = new List<Result>();

                results = OneNoteApplication.FindPages(currentSearch, parent)
                                            .Select(pg => _resultCreator.CreatePageResult(pg, currentSearch))
                                            .ToList();

                if (results.Count == 0)
                {
                    results = _resultCreator.NoMatchesFound(_searchManager.showSingleResults);
                }

                return results;
            }

            private List<Result> Explorer(string search, IOneNoteItem? parent, IEnumerable<IOneNoteItem> collection)
            {
                List<int>? highlightData = null;
                int score = 0;

                var results = collection.Where(_searchManager.SettingsCheck)
                                        .Where(item => _searchManager.FuzzySearch(item.Name, search, out highlightData, out score))
                                        .Select(item => _resultCreator.CreateOneNoteItemResult(item, true, highlightData, score))
                                        .ToList();

                AddCreateNewOneNoteItemResults(search, parent, results);
                return results;
            }

            private void AddCreateNewOneNoteItemResults(string newItemName, IOneNoteItem? parent, List<Result> results)
            {
                if (!results.Any(result => string.Equals(newItemName.Trim(), result.Title, StringComparison.OrdinalIgnoreCase)))
                {
                    if (parent?.IsInRecycleBin() == true)
                    {
                        return;
                    }

                    switch (parent)
                    {
                        case null:
                            results.Add(_resultCreator.CreateNewNotebookResult(newItemName));
                            break;
                        case OneNoteNotebook:
                        case OneNoteSectionGroup:
                            results.Add(_resultCreator.CreateNewSectionResult(newItemName, parent));
                            results.Add(_resultCreator.CreateNewSectionGroupResult(newItemName, parent));
                            break;
                        case OneNoteSection section:
                            if (!section.Locked)
                            {
                                results.Add(_resultCreator.CreateNewPageResult(newItemName, section));
                            }

                            break;
                        default:
                            break;
                    }
                }
            }
        }
    }
}
