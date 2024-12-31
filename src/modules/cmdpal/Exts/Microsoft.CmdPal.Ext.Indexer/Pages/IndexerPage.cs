// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Indexer;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.Indexer;

internal sealed partial class IndexerPage : DynamicListPage, IDisposable
{
    private readonly Lock _lockObject = new(); // Lock object for synchronization
    private readonly List<IListItem> _indexerListItems = [];

    private SearchQuery _searchQuery = new();

    private uint _queryCookie = 10;

    public IndexerPage()
    {
        Icon = new("\ue729");
        Name = Resources.Indexer_Title;
        PlaceholderText = Resources.Indexer_PlaceholderText;

        Logger.InitializeLogger("\\CmdPal\\Indexer\\Logs");
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (oldSearch != newSearch)
        {
            _ = Task.Run(() =>
            {
                Logger.LogDebug($"Update {oldSearch} -> {newSearch}");
                StartQuery(newSearch);
                RaiseItemsChanged(0);
            });
        }
    }

    public override IListItem[] GetItems() => DoGetItems();

    private void StartQuery(string query)
    {
        if (query == string.Empty)
        {
            return;
        }

        Stopwatch stopwatch = new();
        stopwatch.Start();
        Query(query);
        stopwatch.Stop();
        Logger.LogDebug($"Query time: {stopwatch.ElapsedMilliseconds} ms, query: \"{query}\"");
    }

    private IListItem[] DoGetItems()
    {
        if (string.IsNullOrEmpty(SearchText))
        {
            return [];
        }

        Stopwatch stopwatch = new();
        stopwatch.Start();

        lock (_lockObject)
        {
            if (_searchQuery != null)
            {
                var cookie = _searchQuery.Cookie;
                if (cookie == _queryCookie)
                {
                    SearchResult result;
                    while (!_searchQuery.SearchResults.IsEmpty && _searchQuery.SearchResults.TryDequeue(out result))
                    {
                        _indexerListItems.Add(new IndexerListItem(new IndexerItem
                        {
                            FileName = result.ItemDisplayName,
                            FullPath = result.LaunchUri,
                        })
                        {
                            Icon = new(result.IsFolder ? "\uE838" : "\uE8E5"),
                        });
                    }
                }
            }
        }

        stopwatch.Stop();
        Logger.LogDebug($"Build ListItems: {stopwatch.ElapsedMilliseconds} ms, results: {_indexerListItems.Count}, query: \"{SearchText}\"");

        return [.. _indexerListItems];
    }

    private uint Query(string searchText)
    {
        if (searchText == string.Empty)
        {
            return _queryCookie;
        }

        _queryCookie++;
        lock (_lockObject)
        {
            _searchQuery.CancelOutstandingQueries();
            _searchQuery.SearchResults.Clear();
            _indexerListItems.Clear();

            // Just forward on to the helper with the right callback for feeding us results
            // Set up the binding for the items
            _searchQuery.Execute(searchText, _queryCookie);
        }

        // unlock
        // Wait for the query executed event
        _searchQuery.WaitForQueryCompletedEvent();

        return _queryCookie;
    }

    public void Dispose()
    {
        _searchQuery = null;
        GC.SuppressFinalize(this);
    }
}
