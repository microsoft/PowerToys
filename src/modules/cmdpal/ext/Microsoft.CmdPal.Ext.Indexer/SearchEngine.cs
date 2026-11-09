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

namespace Microsoft.CmdPal.Ext.Indexer;

public sealed partial class SearchEngine : IDisposable
{
    private SearchQuery? _searchQuery = new();

    public SearchNoticeInfo? Query(string query, uint queryCookie)
    {
        var searchQuery = _searchQuery;
        if (searchQuery is null)
        {
            return null;
        }

        searchQuery.SearchResults.Clear();
        searchQuery.CancelOutstandingQueries();

        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        Stopwatch stopwatch = new();
        stopwatch.Start();

        searchQuery.Execute(query, queryCookie);

        stopwatch.Stop();
        Logger.LogDebug($"Query time: {stopwatch.ElapsedMilliseconds} ms, query: \"{query}\"");

        return BuildNotice(searchQuery);
    }

    public IList<IListItem> FetchItems(int offset, int limit, uint queryCookie, out bool hasMore, out SearchNoticeInfo? notice)
    {
        hasMore = false;
        notice = null;

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
        notice = BuildNotice(searchQuery);

        while (!searchQuery.SearchResults.IsEmpty && searchQuery.SearchResults.TryDequeue(out var result) && ++index <= limit)
        {
            var indexerListItem = new IndexerListItem(new IndexerItem
            {
                FileName = result.ItemDisplayName,
                FullPath = result.LaunchUri,
            });

            results.Add(indexerListItem);
        }

        hasMore = hasMoreItems;
        return results;
    }

    private static SearchNoticeInfo? BuildNotice(SearchQuery searchQuery)
    {
        return SearchNoticeInfoBuilder.FromQueryStatus(searchQuery.GetExecutionStatus())
            ?? SearchNoticeInfoBuilder.FromCatalogStatus(SearchCatalogStatusReader.GetStatus());
    }

    public void Dispose()
    {
        var searchQuery = _searchQuery;
        _searchQuery = null;

        searchQuery?.Dispose();

        GC.SuppressFinalize(this);
    }
}
