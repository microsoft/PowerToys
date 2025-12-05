// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Indexer.Indexer;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Indexer;

internal sealed partial class IndexerPage : DynamicListPage, IDisposable
{
    private readonly List<IListItem> _indexerListItems = [];
    private readonly SearchEngine _searchEngine;
    private readonly bool disposeSearchEngine = true;

    private uint _queryCookie;

    private string initialQuery = string.Empty;

    private bool _isEmptyQuery = true;

    private CommandItem _noSearchEmptyContent;
    private CommandItem _nothingFoundEmptyContent;

    public IndexerPage()
    {
        Id = "com.microsoft.indexer.fileSearch";
        Icon = Icons.FileExplorerIcon;
        Name = Resources.Indexer_Title;
        PlaceholderText = Resources.Indexer_PlaceholderText;
        _searchEngine = new();
        _queryCookie = 10;

        var filters = new SearchFilters();
        filters.PropChanged += Filters_PropChanged;
        Filters = filters;

        CreateEmptyContent();
    }

    public IndexerPage(string query, SearchEngine searchEngine, uint queryCookie, IList<IListItem> firstPageData)
    {
        Icon = Icons.FileExplorerIcon;
        Name = Resources.Indexer_Title;
        _searchEngine = searchEngine;
        _queryCookie = queryCookie;
        _indexerListItems.AddRange(firstPageData);
        initialQuery = query;
        SearchText = query;
        disposeSearchEngine = false;

        var filters = new SearchFilters();
        filters.PropChanged += Filters_PropChanged;
        Filters = filters;

        CreateEmptyContent();
    }

    private void CreateEmptyContent()
    {
        _noSearchEmptyContent = new CommandItem(new NoOpCommand())
        {
            Icon = Icon,
            Subtitle = Resources.Indexer_NoSearchQueryMessageTip,
        };

        _nothingFoundEmptyContent = new CommandItem(new AnonymousCommand(StartManualSearch) { Name = Resources.Indexer_Command_SearchAllFiles! })
        {
            Icon = Icon,
            Title = Resources.Indexer_NoResultsMessage,
            Subtitle = Resources.Indexer_NoResultsMessageTip,
            MoreCommands = [
                new CommandContextItem(new OpenUrlCommand("ms-settings:search") { Name = Resources.Indexer_Command_OpenIndexerSettings! })
                {
                    Title = Resources.Indexer_Command_SearchAllFiles!,
                },
                ],
        };
    }

    private void StartManualSearch()
    {
        // {20D04FE0-3AEA-1069-A2D8-08002B30309D} is CLSID for "This PC"
        const string template = "search-ms:query={0}&crumb=location:::{{20D04FE0-3AEA-1069-A2D8-08002B30309D}}";
        var fullSearchText = FullSearchString(SearchText);
        var encodedSearchText = UrlEncoder.Default.Encode(fullSearchText);
        var command = string.Format(CultureInfo.CurrentCulture, template, encodedSearchText);
        ShellHelpers.OpenInShell(command);
    }

    public override ICommandItem EmptyContent => _isEmptyQuery ? _noSearchEmptyContent : _nothingFoundEmptyContent;

    private void Filters_PropChanged(object sender, IPropChangedEventArgs args)
    {
        PerformSearch(SearchText);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (oldSearch != newSearch && newSearch != initialQuery)
        {
            PerformSearch(newSearch);
        }
    }

    private void PerformSearch(string newSearch)
    {
        var actualSearch = FullSearchString(newSearch);
        _ = Task.Run(() =>
        {
            _isEmptyQuery = string.IsNullOrWhiteSpace(actualSearch);
            Query(actualSearch);
            LoadMore();
            OnPropertyChanged(nameof(EmptyContent));
            initialQuery = null;
        });
    }

    public override IListItem[] GetItems() => [.. _indexerListItems];

    private string FullSearchString(string query)
    {
        switch (Filters.CurrentFilterId)
        {
            case "folders":
                return $"{query} kind:folders";
            case "files":
                return $"{query} kind:NOT folders";
            case "all":
            default:
                return query;
        }
    }

    public override void LoadMore()
    {
        IsLoading = true;
        var results = _searchEngine.FetchItems(_indexerListItems.Count, 20, _queryCookie, out var hasMore);
        _indexerListItems.AddRange(results);
        HasMoreItems = hasMore;
        IsLoading = false;
        RaiseItemsChanged(_indexerListItems.Count);
    }

    private void Query(string query)
    {
        ++_queryCookie;
        _indexerListItems.Clear();

        _searchEngine.Query(query, _queryCookie);
    }

    public void Dispose()
    {
        if (disposeSearchEngine)
        {
            _searchEngine.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
