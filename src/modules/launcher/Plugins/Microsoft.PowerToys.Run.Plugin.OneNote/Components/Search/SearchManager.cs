// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.OneNote.Components.Search
{
    public class SearchManager
    {
        private readonly PluginInitContext _context;
        private readonly DefaultSearch _defaultSearch;
        private readonly TitleSearch _titleSearch;
        private readonly RecentPages _recentPages;
        private readonly NotebookExplorer _oneNoteExplorer;

        // If the plugin is in global mode and does not start with the action keyword, do not show single results like invalid query.
        private bool showSingleResults = true;

        internal SearchManager(PluginInitContext context, OneNoteSettings settings, ResultCreator resultCreator)
        {
            _context = context;
            _defaultSearch = new DefaultSearch(resultCreator, settings);
            _titleSearch = new TitleSearch(resultCreator, settings);
            _recentPages = new RecentPages(resultCreator, settings);
            _oneNoteExplorer = new NotebookExplorer(resultCreator, settings, _titleSearch);
        }

        internal List<Result> Query(Query query)
        {
            if (_context.CurrentPluginMetadata.IsGlobal)
            {
                showSingleResults = query.RawUserQuery.StartsWith(_context.CurrentPluginMetadata.ActionKeyword, StringComparison.Ordinal);
            }

            string search = query.Search;
            return search switch
            {
                { } when search.StartsWith(_recentPages.Keyword, StringComparison.Ordinal) => _recentPages.GetResults(search, showSingleResults),
                { } when search.StartsWith(_oneNoteExplorer.Keyword, StringComparison.Ordinal) => _oneNoteExplorer.GetResults(search, showSingleResults),
                { } when search.StartsWith(_titleSearch.Keyword, StringComparison.Ordinal) => _titleSearch.GetResults(search, showSingleResults),
                _ => _defaultSearch.GetResults(search!, showSingleResults),
            };
        }
    }
}
