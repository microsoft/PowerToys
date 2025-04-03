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

    private uint _queryCookie = 10;

    private SettingsManager _settingsManager;

    private SearchEngine _searchEngine = new();

    public IndexerPage(SettingsManager settingsManager)
    {
        Id = "com.microsoft.indexer.fileSearch";
        Icon = Icons.FileExplorer;
        Name = Resources.Indexer_Title;
        PlaceholderText = Resources.Indexer_PlaceholderText;
        _settingsManager = settingsManager;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (oldSearch != newSearch)
        {
            _ = Task.Run(() =>
            {
                Query(newSearch);
                LoadMore();
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
        _searchEngine.Dispose();
        GC.SuppressFinalize(this);
    }
}
