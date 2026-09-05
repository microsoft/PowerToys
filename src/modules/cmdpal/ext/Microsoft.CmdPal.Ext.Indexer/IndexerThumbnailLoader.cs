// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.Indexer;

internal sealed partial class IndexerThumbnailLoader : IDisposable
{
    internal const int MaxConcurrency = 4;

    private readonly Lock _gate;
    private readonly Func<string, CancellationToken, Task<IconInfo?>> _loadThumbnail;
    private readonly Action<Exception> _reportError;
    private readonly Queue<(IndexerListItem Item, CancellationToken Token)> _pending = new();
    private TaskCompletionSource _idle = CompletedSource();
    private int _workers;
    private bool _disposed;

    internal IndexerThumbnailLoader(
        Lock gate,
        Func<string, CancellationToken, Task<IconInfo?>>? loadThumbnail = null,
        Action<Exception>? reportError = null)
    {
        _gate = gate;
        _loadThumbnail = loadThumbnail ?? LoadThumbnailAsync;
        _reportError = reportError ?? (ex => Logger.LogError("Failed to get the icon.", ex));
    }

    internal Task Completion
    {
        get
        {
            lock (_gate)
            {
                return _idle.Task;
            }
        }
    }

    internal void Request(IndexerListItem item, CancellationToken token)
    {
        lock (_gate)
        {
            if (_disposed || token.IsCancellationRequested)
            {
                return;
            }

            _pending.Enqueue((item, token));
            if (_workers < MaxConcurrency)
            {
                if (_workers++ == 0)
                {
                    _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
                }

                _ = Task.Run(ProcessQueueAsync, CancellationToken.None);
            }
        }
    }

    internal void ClearPending()
    {
        lock (_gate)
        {
            _pending.Clear();
        }
    }

    private async Task ProcessQueueAsync()
    {
        while (true)
        {
            (IndexerListItem Item, CancellationToken Token) request;
            lock (_gate)
            {
                if (_disposed || !_pending.TryDequeue(out request))
                {
                    if (--_workers == 0)
                    {
                        _idle.TrySetResult();
                    }

                    return;
                }
            }

            try
            {
                request.Token.ThrowIfCancellationRequested();
                var icon = await _loadThumbnail(request.Item.FilePath, request.Token).ConfigureAwait(false);
                lock (_gate)
                {
                    if (!_disposed && !request.Token.IsCancellationRequested && icon is not null)
                    {
                        request.Item.Icon = icon;
                    }
                }
            }
            catch (OperationCanceledException) when (request.Token.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _reportError(ex);
            }
        }
    }

    private static Task<IconInfo?> LoadThumbnailAsync(string path, CancellationToken token)
    {
        return LoadThumbnailAsync(() => ThumbnailHelper.GetThumbnail(path), token);
    }

    internal static async Task<IconInfo?> LoadThumbnailAsync(Func<Task<IRandomAccessStream?>> getThumbnail, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var stream = await getThumbnail().ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        return stream is null ? null : CreateIcon(stream);
    }

    internal static IconInfo CreateIcon(IRandomAccessStream stream)
    {
        // The reference retains its own clone so closing the source cannot interrupt a host read.
        var clone = stream.CloneStream();
        try
        {
            return IconInfo.FromStream(clone);
        }
        catch
        {
            clone.Dispose();
            throw;
        }
    }

    private static TaskCompletionSource CompletedSource()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult();
        return source;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _pending.Clear();
        }
    }
}
