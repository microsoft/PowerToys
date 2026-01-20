// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using LinqToOneNote;
using Microsoft.PowerToys.Run.Plugin.OneNote.Properties;
using Wox.Infrastructure;
using Wox.Plugin;
using OneNoteApplication = LinqToOneNote.OneNote;

namespace Microsoft.PowerToys.Run.Plugin.OneNote.Components
{
    public partial class SearchManager
    {
        private readonly PluginInitContext _context;
        private readonly OneNoteSettings _settings;
        private readonly IconProvider _iconProvider;
        private readonly ResultCreator _resultCreator;
        private readonly NotebookExplorer _notebookExplorer;

        // If the plugin is in global mode and does not start with the action keyword, do not show single results like invalid query.
        private bool showSingleResults = true;

        internal SearchManager(PluginInitContext context, OneNoteSettings settings, IconProvider iconProvider, ResultCreator resultCreator)
        {
            _context = context;
            _settings = settings;
            _resultCreator = resultCreator;
            _iconProvider = iconProvider;
            _notebookExplorer = new NotebookExplorer(this, resultCreator);
        }

        internal List<Result> Query(Query query)
        {
            if (_context.CurrentPluginMetadata.IsGlobal)
            {
                showSingleResults = query.RawUserQuery.StartsWith(_context.CurrentPluginMetadata.ActionKeyword, StringComparison.Ordinal);
            }

            return query.Search switch
            {
                string s when s.StartsWith(Keywords.RecentPages, StringComparison.Ordinal)
                    => RecentPages(s),

                string s when s.StartsWith(Keywords.NotebookExplorer, StringComparison.Ordinal)
                    => _notebookExplorer.Query(query),

                string s when s.StartsWith(Keywords.TitleSearch, StringComparison.Ordinal)
                    => TitleSearch(s, null, OneNoteApplication.GetFullHierarchy().Notebooks),

                _ => DefaultSearch(query.Search),
            };
        }

        private List<Result> DefaultSearch(string query)
        {
            // Check for invalid start of query i.e. symbols
            if (!char.IsLetterOrDigit(query[0]))
            {
                return _resultCreator.InvalidQuery(showSingleResults);
            }

            var results = OneNoteApplication.FindPages(query)
                                            .Select(pg => _resultCreator.CreatePageResult(pg, query))
                                            .ToList();

            return results.Count != 0 ? results : _resultCreator.NoMatchesFound(showSingleResults);
        }

        private List<Result> TitleSearch(string query, IOneNoteItem? parent, IEnumerable<IOneNoteItem> currentCollection)
        {
            if (query.Length == Keywords.TitleSearch.Length && parent == null)
            {
                return ResultCreator.SingleResult(Resources.SearchingByTitle, null, _iconProvider.Search);
            }

            List<int>? highlightData = null;
            int score = 0;

            var currentSearch = query[Keywords.TitleSearch.Length..];

            var results = currentCollection.Descendants(item => SettingsCheck(item) && FuzzySearch(item.Name, currentSearch, out highlightData, out score))
                                           .Select(item => _resultCreator.CreateOneNoteItemResult(item, false, highlightData, score))
                                           .ToList();

            return results.Count != 0 ? results : _resultCreator.NoMatchesFound(showSingleResults);
        }

        private List<Result> RecentPages(string query)
        {
            int count = 25; // TODO: Ideally this should match PowerToysRunSettings.MaxResultsToShow
            /*          var settingsUtils = new SettingsUtils();
                        var generalSettings = settingsUtils.GetSettings<GeneralSettings>();*/
            if (query.Length > Keywords.RecentPages.Length && int.TryParse(query[Keywords.RecentPages.Length..], out int userChosenCount))
            {
                count = userChosenCount;
            }

            return OneNoteApplication.GetFullHierarchy()
                                     .Notebooks
                                     .GetAllPages()
                                     .Where(SettingsCheck)
                                     .OrderByDescending(pg => pg.LastModified)
                                     .Take(count)
                                     .Select(_resultCreator.CreateRecentPageResult)
                                     .ToList();
        }

        private bool FuzzySearch(string itemName, string search, out List<int> highlightData, out int score)
        {
            var matchResult = StringMatcher.FuzzySearch(search, itemName);
            highlightData = matchResult.MatchData;
            score = matchResult.Score;
            return matchResult.IsSearchPrecisionScoreMet();
        }

        private bool SettingsCheck(IOneNoteItem item)
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
