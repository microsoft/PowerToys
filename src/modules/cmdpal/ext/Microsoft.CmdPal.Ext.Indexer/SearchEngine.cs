// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

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
    private SearchQuery? _searchQuery = new();

    public void Query(string query, uint queryCookie)
    {
        var searchQuery = _searchQuery;
        if (searchQuery is null)
        {
            return;
        }

        searchQuery.SearchResults.Clear();
        searchQuery.CancelOutstandingQueries();

        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        Stopwatch stopwatch = new();
        stopwatch.Start();

        searchQuery.Execute(query, queryCookie);

        stopwatch.Stop();
        Logger.LogDebug($"Query time: {stopwatch.ElapsedMilliseconds} ms, query: \"{query}\"");
    }

    public IList<IListItem> FetchItems(int offset, int limit, uint queryCookie, out bool hasMore, bool noIcons = false)
    {
        hasMore = false;

        var searchQuery = _searchQuery;
        if (searchQuery is null)
        {
            return [];
        }

        var cookie = searchQuery.Cookie;
        if (cookie != queryCookie)
        {
            return [];
        }

        var results = new List<IListItem>();
        var index = 0;
        var hasMoreItems = searchQuery.FetchRows(offset, limit);

        while (!searchQuery.SearchResults.IsEmpty && searchQuery.SearchResults.TryDequeue(out var result) && ++index <= limit)
        {
            var indexerListItem = new IndexerListItem(new IndexerItem
            {
                FileName = result.ItemDisplayName,
                FullPath = result.LaunchUri,
            });

            if (!noIcons)
            {
                IconInfo? icon = null;
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

                indexerListItem.Icon = icon;
            }

            results.Add(indexerListItem);
        }

        hasMore = hasMoreItems;
        return results;
    }

    public void Dispose()
    {
        var searchQuery = _searchQuery;
        _searchQuery = null;

        searchQuery?.Dispose();

        GC.SuppressFinalize(this);
    }
}
