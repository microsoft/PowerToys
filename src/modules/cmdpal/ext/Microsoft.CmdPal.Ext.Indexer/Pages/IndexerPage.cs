// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Indexer.Indexer;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Indexer;

internal sealed partial class IndexerPage : DynamicListPage, IDisposable
{
    // Cookie to identify our queries; since we replace the SearchEngine on each search,
    // this can be a constant.
    private const uint HardQueryCookie = 10;

    private readonly List<IListItem> _indexerListItems = [];
    private readonly Lock _searchLock = new();

    private SearchEngine? _searchEngine;

    private CancellationTokenSource? _searchCts;
    private string _initialQuery = string.Empty;
    private bool _isEmptyQuery = true;

    private CommandItem? _noSearchEmptyContent;
    private CommandItem? _nothingFoundEmptyContent;

    private bool _deferredLoad;

    public override ICommandItem EmptyContent => _isEmptyQuery ? _noSearchEmptyContent! : _nothingFoundEmptyContent!;

    public IndexerPage()
    {
        Id = "com.microsoft.indexer.fileSearch";
        Icon = Icons.FileExplorerIcon;
        Name = Resources.Indexer_Title;
        PlaceholderText = Resources.Indexer_PlaceholderText;

        _searchEngine = new();

        var filters = new SearchFilters();
        filters.PropChanged += Filters_PropChanged;
        Filters = filters;

        CreateEmptyContent();
    }

    public IndexerPage(string query)
    {
        Icon = Icons.FileExplorerIcon;
        Name = Resources.Indexer_Title;

        _searchEngine = new();

        _initialQuery = query;
        SearchText = query;

        var filters = new SearchFilters();
        filters.PropChanged += Filters_PropChanged;
        Filters = filters;

        CreateEmptyContent();
        IsLoading = true;
        _deferredLoad = true;
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

    private void Filters_PropChanged(object sender, IPropChangedEventArgs args)
    {
        PerformSearch(SearchText);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (oldSearch != newSearch && newSearch != _initialQuery)
        {
            PerformSearch(newSearch);
        }
    }

    public override IListItem[] GetItems()
    {
        if (_deferredLoad)
        {
            PerformSearch(_initialQuery);
            _deferredLoad = false;
        }

        return [.. _indexerListItems];
    }

    private string FullSearchString(string query)
    {
        switch (Filters?.CurrentFilterId)
        {
            case "folders":
                return $"System.Kind:folders {query}";
            case "files":
                return $"System.Kind:NOT folders {query}";
            case "all":
            default:
                return query;
        }
    }

    public override void LoadMore()
    {
        var ct = Volatile.Read(ref _searchCts)?.Token;

        IsLoading = true;

        var hasMore = false;
        SearchEngine? searchEngine;
        int offset;

        lock (_searchLock)
        {
            searchEngine = _searchEngine;
            offset = _indexerListItems.Count;
        }

        var results = searchEngine?.FetchItems(offset, 20, queryCookie: HardQueryCookie, out hasMore) ?? [];

        if (ct?.IsCancellationRequested == true)
        {
            IsLoading = false;
            return;
        }

        lock (_searchLock)
        {
            if (ct?.IsCancellationRequested == true)
            {
                IsLoading = false;
                return;
            }

            _indexerListItems.AddRange(results);
            HasMoreItems = hasMore;
            IsLoading = false;
            RaiseItemsChanged(_indexerListItems.Count);
        }
    }

    private void Query(string query)
    {
        lock (_searchLock)
        {
            _indexerListItems.Clear();
            _searchEngine?.Query(query, queryCookie: HardQueryCookie);
        }
    }

    private void ReplaceSearchEngine(SearchEngine newSearchEngine)
    {
        SearchEngine? oldEngine;

        lock (_searchLock)
        {
            oldEngine = _searchEngine;
            _searchEngine = newSearchEngine;
        }

        oldEngine?.Dispose();
    }

    private void PerformSearch(string newSearch)
    {
        var actualSearch = FullSearchString(newSearch);

        var newCts = new CancellationTokenSource();
        var oldCts = Interlocked.Exchange(ref _searchCts, newCts);
        oldCts?.Cancel();
        oldCts?.Dispose();

        var ct = newCts.Token;

        _ = Task.Run(
            () =>
            {
                ct.ThrowIfCancellationRequested();

                lock (_searchLock)
                {
                    // If the user hasn't provided any base query text, results should be empty
                    // regardless of the currently selected filter.
                    _isEmptyQuery = string.IsNullOrWhiteSpace(newSearch);

                    if (_isEmptyQuery)
                    {
                        _indexerListItems.Clear();
                        HasMoreItems = false;
                        IsLoading = false;
                        RaiseItemsChanged(0);
                        OnPropertyChanged(nameof(EmptyContent));
                        _initialQuery = string.Empty;
                        return;
                    }

                    // Track the most recent query we initiated, so UpdateSearchText doesn't
                    // spuriously suppress a search when SearchText gets set programmatically.
                    _initialQuery = newSearch;
                }

                ct.ThrowIfCancellationRequested();
                ReplaceSearchEngine(new SearchEngine());

                ct.ThrowIfCancellationRequested();
                Query(actualSearch);

                ct.ThrowIfCancellationRequested();
                LoadMore();

                ct.ThrowIfCancellationRequested();

                lock (_searchLock)
                {
                    OnPropertyChanged(nameof(EmptyContent));
                }
            },
            ct);
    }

    public void Dispose()
    {
        var cts = Interlocked.Exchange(ref _searchCts, null);
        cts?.Cancel();
        cts?.Dispose();

        SearchEngine? searchEngine;

        lock (_searchLock)
        {
            searchEngine = _searchEngine;
            _searchEngine = null;
            _indexerListItems.Clear();
        }

        searchEngine?.Dispose();

        GC.SuppressFinalize(this);
    }
}
