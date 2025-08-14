// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

    public IndexerPage()
    {
        Id = "com.microsoft.indexer.fileSearch";
        Icon = Icons.FileExplorerIcon;
        Name = Resources.Indexer_Title;
        PlaceholderText = Resources.Indexer_PlaceholderText;
        _searchEngine = new();
        _queryCookie = 10;
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
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (oldSearch != newSearch && newSearch != initialQuery)
        {
            _ = Task.Run(() =>
            {
                Query(newSearch);
                LoadMore();
                initialQuery = string.Empty;
            });
        }
    }

    public override IListItem[] GetItems() => [.. _indexerListItems];

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
