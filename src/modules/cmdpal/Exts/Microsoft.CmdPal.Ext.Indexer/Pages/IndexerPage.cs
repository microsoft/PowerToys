// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Indexer;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.Indexer;

internal sealed partial class IndexerPage : DynamicListPage, IDisposable
{
    private readonly List<IListItem> _indexerListItems = [];

    private SearchQuery _searchQuery = new();

    private uint _queryCookie = 10;

    public IndexerPage()
    {
        Id = "com.microsoft.indexer.fileSearch";
        Icon = Icons.FileExplorer;
        Name = Resources.Indexer_Title;
        PlaceholderText = Resources.Indexer_PlaceholderText;
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
        FetchItems(20);
        IsLoading = false;
        RaiseItemsChanged(_indexerListItems.Count);
    }

    private void Query(string query)
    {
        ++_queryCookie;
        _indexerListItems.Clear();
        _searchQuery.SearchResults.Clear();
        _searchQuery.CancelOutstandingQueries();

        if (query == string.Empty)
        {
            return;
        }

        Stopwatch stopwatch = new();
        stopwatch.Start();

        _searchQuery.Execute(query, _queryCookie);

        stopwatch.Stop();
        Logger.LogDebug($"Query time: {stopwatch.ElapsedMilliseconds} ms, query: \"{query}\"");
    }

    private void FetchItems(int limit)
    {
        if (_searchQuery != null)
        {
            var cookie = _searchQuery.Cookie;
            if (cookie == _queryCookie)
            {
                var index = 0;
                SearchResult result;

                var hasMoreItems = _searchQuery.FetchRows(_indexerListItems.Count, limit);

                while (!_searchQuery.SearchResults.IsEmpty && _searchQuery.SearchResults.TryDequeue(out result) && ++index <= limit)
                {
                    IconInfo icon = null;
                    try
                    {
                        var stream = ThumbnailHelper.GetThumbnail(result.LaunchUri).Result;
                        if (stream != null)
                        {
                            var data = new IconData(RandomAccessStreamReference.CreateFromStream(stream));
                            icon = new IconInfo(data, data);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to get the icon.", ex);
                    }

                    _indexerListItems.Add(new IndexerListItem(new IndexerItem
                    {
                        FileName = result.ItemDisplayName,
                        FullPath = result.LaunchUri,
                    })
                    {
                        Icon = icon,
                    });
                }

                HasMoreItems = hasMoreItems;
            }
        }
    }

    public void Dispose()
    {
        _searchQuery = null;
        GC.SuppressFinalize(this);
    }
}
