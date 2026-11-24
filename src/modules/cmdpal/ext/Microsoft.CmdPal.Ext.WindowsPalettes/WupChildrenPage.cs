// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.UI.Palettes;

namespace Microsoft.CmdPal.Ext.WindowsPalettes;

internal sealed partial class WupChildrenPage : DynamicListPage, IDisposable
{
    private static readonly TimeSpan InitialLoadTimeout = TimeSpan.FromSeconds(30);

    private readonly PaletteItemView _parent;
    private readonly IPaletteItemSourceExtension _source;
    private readonly string _sourceDisplayName;
    private readonly object _stateLock = new();
    private readonly List<WupListItemAdapter> _adapters = [];
    private CancellationTokenSource? _queryCancellation;
    private TaskCompletionSource _initialLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _queryGeneration;
    private bool _started;
    private bool _hasReadItems;

    internal WupChildrenPage(
        PaletteItemView parent,
        IPaletteItemSourceExtension source,
        string sourceDisplayName,
        string? commandId = null)
    {
        _parent = parent;
        _source = source;
        _sourceDisplayName = sourceDisplayName;
        Id = commandId ?? WupCommandId.Create(parent.ExtensionId, parent.Id, WupCommandKind.Children);
        Name = parent.Title ?? string.Empty;
        Title = Name;
        Icon = WupIconAdapter.Create(parent.Icon);
        PlaceholderText = Name;
        ShowDetails = true;
    }

    public override IListItem[] GetItems()
    {
        // CmdPal currently subscribes to ItemsChanged after its initial GetItems call, so an asynchronous
        // first result can go unnoticed. Return the initial snapshot synchronously until the host fixes
        // that ordering; then we can return immediately and notify for the first query like later searches.
        while (true)
        {
            var searchText = SearchText ?? string.Empty;
            TaskCompletionSource initialLoad;
            lock (_stateLock)
            {
                if (!_started)
                {
                    QueueQueryUnderLock(searchText);
                }

                if (_hasReadItems)
                {
                    return [.. _adapters];
                }

                initialLoad = _initialLoad;
            }

            try
            {
                initialLoad.Task.WaitAsync(InitialLoadTimeout).GetAwaiter().GetResult();
            }
            catch (TimeoutException ex)
            {
                CancellationTokenSource? cancellation;
                lock (_stateLock)
                {
                    if (!ReferenceEquals(initialLoad, _initialLoad) || !initialLoad.TrySetException(ex))
                    {
                        if (initialLoad.Task.IsFaulted)
                        {
                            throw;
                        }

                        continue;
                    }

                    _queryGeneration++;
                    cancellation = _queryCancellation;
                    _queryCancellation = null;
                }

                WupDiagnostics.Write($"Initial children query timed out for '{_parent.ExtensionId}:{_parent.Id}' after {InitialLoadTimeout.TotalSeconds} seconds.");
                CancelQuery(cancellation);
                throw;
            }

            lock (_stateLock)
            {
                if (!ReferenceEquals(initialLoad, _initialLoad))
                {
                    continue;
                }

                _hasReadItems = true;
                return [.. _adapters];
            }
        }
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        lock (_stateLock)
        {
            if (!_hasReadItems && _initialLoad.Task.IsCompleted)
            {
                _initialLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            QueueQueryUnderLock(newSearch);
        }
    }

    public override void LoadMore()
    {
    }

    // A replaced list item disposes its children page, but Command Palette may still be showing it. Disposal
    // releases the current results and the next GetItems call re-runs the query.
    public void Dispose()
    {
        CancellationTokenSource? cancellation;
        WupListItemAdapter[] adapters;
        lock (_stateLock)
        {
            _queryGeneration++;
            cancellation = _queryCancellation;
            _queryCancellation = null;
            adapters = [.. _adapters];
            _adapters.Clear();
            _initialLoad.TrySetCanceled();
            _initialLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _started = false;
            _hasReadItems = false;
        }

        CancelQuery(cancellation);
        foreach (var adapter in adapters)
        {
            adapter.Dispose();
        }

        IsLoading = false;
        RaiseItemsChanged(0);
        GC.SuppressFinalize(this);
    }

    private static void CancelQuery(CancellationTokenSource? cancellation)
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
    }

    private void QueueQueryUnderLock(string searchText)
    {
        _started = true;
        var generation = ++_queryGeneration;
        var previous = _queryCancellation;
        _queryCancellation = new CancellationTokenSource();
        var cancellationToken = _queryCancellation.Token;
        var initialLoad = _initialLoad;
        var notify = _hasReadItems;
        _ = Task.Run(async () =>
        {
            CancelQuery(previous);
            await ExecuteQueryAsync(searchText, generation, cancellationToken, initialLoad, notify).ConfigureAwait(false);
        });
    }

    private async Task ExecuteQueryAsync(
        string searchText,
        long generation,
        CancellationToken cancellationToken,
        TaskCompletionSource initialLoad,
        bool notify)
    {
        PaletteQuery? query = null;
        try
        {
            if (!IsCurrentQuery(generation, cancellationToken))
            {
                return;
            }

            if (notify)
            {
                IsLoading = true;
            }

            query = new PaletteQuery
            {
                Arguments = string.IsNullOrEmpty(searchText) ? [] : [searchText],
                Mode = PaletteQueryMode.Children,
                Scopes = [_parent],
            };

            var results = await _source.ExecuteQueryAsync(query).AsCancellableTask(cancellationToken).ConfigureAwait(false);
            PublishResults(results, query, generation, cancellationToken, initialLoad, notify);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            WupDiagnostics.Write($"Children failed for '{_parent.ExtensionId}:{_parent.Id}': 0x{ex.HResult:X8} {ex.Message}");
            lock (_stateLock)
            {
                if (!IsCurrentQuery(generation, cancellationToken) || initialLoad.TrySetException(ex))
                {
                    return;
                }
            }

            PublishResults(null, query, generation, cancellationToken, initialLoad, notify);
        }
    }

    private bool IsCurrentQuery(long generation, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested && generation == Volatile.Read(ref _queryGeneration);

    private void PublishResults(
        PaletteQueryResultsView? results,
        PaletteQuery? query,
        long generation,
        CancellationToken cancellationToken,
        TaskCompletionSource initialLoad,
        bool notify)
    {
        List<WupListItemAdapter> replacements = [];
        try
        {
            foreach (var item in results?.Items ?? [])
            {
                replacements.Add(new(item, _source, query, _sourceDisplayName, null));
            }
        }
        catch
        {
            foreach (var adapter in replacements)
            {
                adapter.Dispose();
            }

            throw;
        }

        WupListItemAdapter[] oldAdapters;
        bool published;
        lock (_stateLock)
        {
            published = IsCurrentQuery(generation, cancellationToken);
            if (published)
            {
                oldAdapters = [.. _adapters];
                _adapters.Clear();
                _adapters.AddRange(replacements);
                // Initial readers must be released before disposal or events can re-enter GetItems.
                initialLoad.TrySetResult();
            }
            else
            {
                oldAdapters = [.. replacements];
            }
        }

        foreach (var adapter in oldAdapters)
        {
            adapter.Dispose();
        }

        if (published && notify && IsCurrentQuery(generation, cancellationToken))
        {
            IsLoading = false;
            RaiseItemsChanged(replacements.Count);
        }
    }
}
