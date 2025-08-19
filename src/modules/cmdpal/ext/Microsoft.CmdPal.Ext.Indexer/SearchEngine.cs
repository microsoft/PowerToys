// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Indexer;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.Indexer;

public sealed partial class SearchEngine : IDisposable
{
    private SearchQuery _searchQuery = new();

    public void Query(string query, uint queryCookie)
    {
        // _indexerListItems.Clear();
        _searchQuery.SearchResults.Clear();
        _searchQuery.CancelOutstandingQueries();

        if (query == string.Empty)
        {
            return;
        }

        Stopwatch stopwatch = new();
        stopwatch.Start();

        _searchQuery.Execute(query, queryCookie);

        stopwatch.Stop();
        Logger.LogDebug($"Query time: {stopwatch.ElapsedMilliseconds} ms, query: \"{query}\"");
    }

    public IList<IListItem> FetchItems(int offset, int limit, uint queryCookie, out bool hasMore)
    {
        hasMore = false;
        var results = new List<IListItem>();
        if (_searchQuery is not null)
        {
            var cookie = _searchQuery.Cookie;
            if (cookie == queryCookie)
            {
                var index = 0;
                SearchResult result;

                // var hasMoreItems = _searchQuery.FetchRows(_indexerListItems.Count, limit);
                var hasMoreItems = _searchQuery.FetchRows(offset, limit);

                while (!_searchQuery.SearchResults.IsEmpty && _searchQuery.SearchResults.TryDequeue(out result) && ++index <= limit)
                {
                    IconInfo icon = null;
                    try
                    {
                        var stream = ThumbnailHelper.GetThumbnail(result.LaunchUri).Result;
                        if (stream is not null)
                        {
                            var data = new IconData(RandomAccessStreamReference.CreateFromStream(stream));
                            icon = new IconInfo(data, data);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to get the icon.", ex);
                    }

                    results.Add(new IndexerListItem(new IndexerItem
                    {
                        FileName = result.ItemDisplayName,
                        FullPath = result.LaunchUri,
                    })
                    {
                        Icon = icon,
                    });
                }

                hasMore = hasMoreItems;
            }
        }

        return results;
    }

    public void Dispose()
    {
        _searchQuery = null;
        GC.SuppressFinalize(this);
    }
}
