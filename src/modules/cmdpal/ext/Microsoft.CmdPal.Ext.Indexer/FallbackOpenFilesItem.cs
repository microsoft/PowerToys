// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.Indexer;

internal sealed partial class FallbackOpenFilesItem : FallbackCommandItem, IFallbackHandler2
{
    private const string CommandId = "com.microsoft.cmdpal.builtin.indexer.fallback";
    private const uint HardQueryCookie = 10;
    private const int MaxFallbackItems = 5;
    private static readonly NoOpCommand BaseCommandWithId = new() { Id = CommandId };

    private readonly Lock _queryLock = new();
    private readonly Dictionary<string, CancellationTokenSource> _queries = [];
    private Func<string, bool>? _suppressCallback;

    public FallbackOpenFilesItem()
        : base(BaseCommandWithId, Resources.Indexer_Find_Path_fallback_display_title, CommandId)
    {
        Icon = Icons.FileExplorerIcon;
        SuggestedQueryDelayMilliseconds = new(true, 120);
        SuggestedMinQueryLength = new(true, 2);
    }

    public override void UpdateQuery(string query)
    {
        // This fallback is query-snapshot based and is expected to be driven via IFallbackHandler2.
    }

    public IAsyncOperation<IFallbackCommandResult> UpdateQueryAsync(string query, string queryId)
    {
        var cts = new CancellationTokenSource();

        lock (_queryLock)
        {
            _queries[queryId] = cts;
        }

        return Task.Run(() => ExecuteQuery(query, queryId, cts)).AsAsyncOperation();
    }

    public void CancelQuery(string queryId)
    {
        CancellationTokenSource? cts = null;

        lock (_queryLock)
        {
            if (_queries.TryGetValue(queryId, out cts))
            {
                _queries.Remove(queryId);
            }
        }

        if (cts is not null)
        {
            cts.Cancel();
        }
    }

    public void SuppressFallbackWhen(Func<string, bool> callback)
    {
        _suppressCallback = callback;
    }

    private IFallbackCommandResult ExecuteQuery(string query, string queryId, CancellationTokenSource cts)
    {
        try
        {
            var token = cts.Token;
            token.ThrowIfCancellationRequested();

            var suppressCallback = _suppressCallback;
            if (string.IsNullOrWhiteSpace(query) || (suppressCallback is not null && suppressCallback(query)))
            {
                return EmptyResult(query, queryId);
            }

            if (Path.Exists(query))
            {
                var directMatch = new IndexerListItem(new IndexerItem(fullPath: query), IncludeBrowseCommand.AsDefault)
                {
                    Icon = Icons.FileExplorerIcon,
                };
                StartLazyIconLoad(directMatch, query, token);

                return CreateResult(query, queryId, directMatch);
            }

            using var searchEngine = new SearchEngine();
            searchEngine.Query(query, queryCookie: HardQueryCookie);
            token.ThrowIfCancellationRequested();

            var results = searchEngine.FetchItems(0, MaxFallbackItems, queryCookie: HardQueryCookie, out _, noIcons: true);
            token.ThrowIfCancellationRequested();

            foreach (var result in results)
            {
                if (result is CommandItem commandItem && commandItem.Icon is null)
                {
                    commandItem.Icon = Icons.FileExplorerIcon;
                }

                if (result is IndexerListItem indexerListItem)
                {
                    StartLazyIconLoad(indexerListItem, indexerListItem.FilePath, token);
                }
            }

            return CreateResult(query, queryId, results);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return EmptyResult(query, queryId);
        }
        finally
        {
            CleanupQuery(queryId, cts);
        }
    }

    private static FallbackCommandResult CreateResult(string query, string queryId, params IListItem[] items)
    {
        return new FallbackCommandResult
        {
            Query = query,
            QueryId = queryId,
            Items = items,
        };
    }

    private static FallbackCommandResult CreateResult(string query, string queryId, IEnumerable<IListItem> items)
    {
        return new FallbackCommandResult
        {
            Query = query,
            QueryId = queryId,
            Items = items.ToArray(),
        };
    }

    private static FallbackCommandResult EmptyResult(string query, string queryId) => CreateResult(query, queryId, []);

    private static IIconInfo LoadIconOrDefault(string path)
    {
        try
        {
            var stream = ThumbnailHelper.GetThumbnail(path).GetAwaiter().GetResult();
            if (stream is not null)
            {
                var iconData = new IconData(RandomAccessStreamReference.CreateFromStream(stream));
                return new IconInfo(iconData, iconData);
            }
        }
        catch
        {
        }

        return Icons.FileExplorerIcon;
    }

    private static void StartLazyIconLoad(IndexerListItem item, string path, CancellationToken cancellationToken)
    {
        _ = Task.Run(
            () =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var icon = LoadIconOrDefault(path);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                item.Icon = icon;
            },
            cancellationToken);
    }

    private void CleanupQuery(string queryId, CancellationTokenSource cts)
    {
        lock (_queryLock)
        {
            if (_queries.TryGetValue(queryId, out var current) && ReferenceEquals(current, cts))
            {
                _queries.Remove(queryId);
            }
        }

        cts.Dispose();
    }
}
