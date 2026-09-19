// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;

internal sealed class ClipboardHistoryCache(IClipboardHistorySource source, IClipboardHistoryWorker worker, Action changed) : IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _shutdown = new();
    private ClipboardHistorySnapshot _snapshot = ClipboardHistorySnapshot.Empty;
    private Task _refresh = Task.CompletedTask;
    private Task? _dispose;
    private CancellationTokenSource? _loadCancellation;
    private long _version;
    private bool _refreshing;
    private bool _disposed;

    public bool IsRefreshing
    {
        get
        {
            lock (_gate)
            {
                return _refreshing;
            }
        }
    }

    public IListItem[] Items
    {
        get
        {
            lock (_gate)
            {
                return _snapshot.Items;
            }
        }
    }

    public Task RefreshAsync()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            _version++;
            if (_refreshing)
            {
                _loadCancellation?.Cancel();
            }
            else
            {
                _refreshing = true;
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _refresh = completion.Task;
                _ = RunWorkerAsync(completion);
            }

            return _refresh;
        }
    }

    private async Task RunWorkerAsync(TaskCompletionSource completion)
    {
        try
        {
            await worker.RunAsync(RefreshCoreAsync).ConfigureAwait(false);
            completion.SetResult();
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                if (ReferenceEquals(_refresh, completion.Task))
                {
                    _refreshing = false;
                }
            }

            completion.SetException(ex);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _version++;
            _snapshot = ClipboardHistorySnapshot.Empty;
            _loadCancellation?.Cancel();
        }

        changed();
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_dispose is null)
            {
                _disposed = true;
                _snapshot = ClipboardHistorySnapshot.Empty;
                _refreshing = false;
                _shutdown.Cancel();
                _dispose = DisposeCoreAsync();
            }

            return new ValueTask(_dispose);
        }
    }

    private async Task RefreshCoreAsync()
    {
        while (true)
        {
            long version;
            ClipboardHistorySnapshot previous;
            CancellationTokenSource cancellation;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                version = _version;
                previous = _snapshot;
                cancellation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                _loadCancellation = cancellation;
            }

            ClipboardHistorySnapshot next;
            try
            {
                var entries = await source.ReadAsync(cancellation.Token);
                next = await previous.RefreshAsync(entries, cancellation.Token);
            }
            catch (Exception ex)
            {
                bool hadItems;
                lock (_gate)
                {
                    if (_disposed || version != _version)
                    {
                        if (ex is not OperationCanceledException)
                        {
                            ExtensionHost.LogMessage(ex.ToString());
                        }

                        if (_disposed)
                        {
                            return;
                        }

                        continue;
                    }

                    hadItems = _snapshot.Items.Length > 0;
                    _snapshot = ClipboardHistorySnapshot.Empty;
                    _refreshing = false;
                }

                if (hadItems)
                {
                    changed();
                }

                throw;
            }
            finally
            {
                lock (_gate)
                {
                    _loadCancellation = null;
                    cancellation.Dispose();
                }
            }

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                if (version != _version)
                {
                    continue;
                }

                _snapshot = next;
                _refreshing = false;
            }

            changed();
            return;
        }
    }

    private async Task DisposeCoreAsync()
    {
        try
        {
            await worker.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _shutdown.Dispose();
        }
    }
}
