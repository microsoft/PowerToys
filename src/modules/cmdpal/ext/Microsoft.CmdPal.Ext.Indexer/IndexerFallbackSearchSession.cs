// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Indexer.Indexer;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Indexer;

internal sealed partial class IndexerFallbackSearchSession : IDisposable
{
    private const uint QueryCookie = 10;
    private const int ProgressBatchSize = 2;
    private const int MaximumBatchSize = 50;

    private readonly IFallbackQueryArgs _args;
    private readonly IIndexerFallbackSearchEngine _searchEngine;
    private readonly List<IListItem> _items = [];
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private int _disposed;

    internal IndexerFallbackSearchSession(IFallbackQueryArgs args, IIndexerFallbackSearchEngine? searchEngine = null)
    {
        _args = args;
        _searchEngine = searchEngine ?? new SearchEngine();
    }

    internal async Task<IFallbackCommandResult> QueryAsync(
        CancellationToken cancellationToken,
        IProgress<IFallbackCommandResult> progress)
    {
        try
        {
            return await Task.Run(() => Query(cancellationToken, progress), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private IFallbackCommandResult Query(CancellationToken cancellationToken, IProgress<IFallbackCommandResult> progress)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_args.RequestedItemCount == 0)
        {
            Dispose();
            return CreateResult(false);
        }

        _searchEngine.Query(_args.Query, QueryCookie);
        cancellationToken.ThrowIfCancellationRequested();

        var requestedCount = (int)Math.Min(_args.RequestedItemCount, MaximumBatchSize);
        var firstCount = Math.Min(requestedCount, ProgressBatchSize);
        var hasMore = Fetch(firstCount, out var notice);
        cancellationToken.ThrowIfCancellationRequested();

        if (hasMore && _items.Count > 0 && requestedCount > firstCount)
        {
            progress.Report(CreateProgressResult());
            hasMore = Fetch(requestedCount - _items.Count, out notice);
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (_items.Count == 0 && notice is not null)
        {
            _items.Add(CreateNoticeItem(notice.Value));
            hasMore = false;
        }

        var result = CreateResult(hasMore);
        if (!hasMore)
        {
            Dispose();
        }

        return result;
    }

    private async Task<IFallbackCommandResult> LoadMoreAsync(
        uint requestedItemCount,
        CancellationToken cancellationToken,
        IProgress<IFallbackCommandResult> progress)
    {
        IFallbackCommandResult result;
        await _loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            result = await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var count = (int)Math.Min(requestedItemCount, MaximumBatchSize);
                    var hasMore = count == 0 || Fetch(count, out _);
                    cancellationToken.ThrowIfCancellationRequested();
                    return CreateResult(hasMore);
                },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _loadLock.Release();
        }

        if (!result.HasMoreItems)
        {
            Dispose();
        }

        return result;
    }

    private bool Fetch(int count, out SearchNoticeInfo? notice)
    {
        if (count <= 0)
        {
            notice = null;
            return false;
        }

        var results = _searchEngine.FetchNextItems(count, QueryCookie, out var hasMore, out notice);
        foreach (var item in results)
        {
            IndexerFallbackResultSource.SetStableCommandId(item);
            _items.Add(item);
        }

        return hasMore;
    }

    private FallbackQueryResult CreateResult(bool hasMore)
    {
        return new FallbackQueryResult(
            _args.Query,
            _args.QueryId,
            [.. _items],
            hasMore,
            hasMore ? LoadMoreAsync : null,
            hasMore ? Dispose : null);
    }

    private FallbackQueryResult CreateProgressResult()
    {
        return new FallbackQueryResult(_args.Query, _args.QueryId, [.. _items]);
    }

    private IListItem CreateNoticeItem(SearchNoticeInfo notice)
    {
        return new ListItem(new IndexerPage(_args.Query))
        {
            Title = Resources.IndexerCommandsProvider_DisplayName,
            Subtitle = notice.Title,
            Icon = Icons.FileExplorerIcon,
            MoreCommands =
            [
                new CommandContextItem(new OpenUrlCommand("ms-settings:search") { Name = Resources.Indexer_Command_OpenIndexerSettings! }),
            ],
        };
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _searchEngine.Dispose();
        _loadLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
