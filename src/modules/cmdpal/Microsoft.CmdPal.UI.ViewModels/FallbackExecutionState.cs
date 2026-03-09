// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

internal sealed class FallbackExecutionState
{
    private static readonly ConcurrentDictionary<string, Regex?> RegexCache = new(StringComparer.Ordinal);
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(50);

    private readonly Lock _stateLock = new();
    private readonly IFallbackCommandItem _fallbackCommandItem;
    private readonly IFallbackHandler2? _asyncFallbackHandler;
    private readonly IFormattedFallbackCommandItem? _formattedFallbackCommandItem;
    private readonly IHostMatchedFallbackCommandItem? _hostMatchedFallbackCommandItem;
    private readonly Func<FallbackExecutionPolicy> _getPolicy;
    private readonly MaterializeFallbackSnapshotItemsCallback _materializeSnapshotItems;
    private readonly Action _requestRefresh;

    private FallbackQueryState _queryState = FallbackQueryState.Empty;
    private long _querySequence;
    private PendingAsyncFallbackQuery? _pendingAsyncFallbackQuery;
    private string _activeAsyncFallbackQueryId = string.Empty;
    private CancellationTokenSource? _scheduledAsyncFallbackCts;

    internal FallbackExecutionState(
        IFallbackCommandItem fallbackCommandItem,
        IFallbackHandler2? asyncFallbackHandler,
        IFormattedFallbackCommandItem? formattedFallbackCommandItem,
        IHostMatchedFallbackCommandItem? hostMatchedFallbackCommandItem,
        Func<FallbackExecutionPolicy> getPolicy,
        MaterializeFallbackSnapshotItemsCallback materializeSnapshotItems,
        Action requestRefresh)
    {
        _fallbackCommandItem = fallbackCommandItem;
        _asyncFallbackHandler = asyncFallbackHandler;
        _formattedFallbackCommandItem = formattedFallbackCommandItem;
        _hostMatchedFallbackCommandItem = hostMatchedFallbackCommandItem;
        _getPolicy = getPolicy;
        _materializeSnapshotItems = materializeSnapshotItems;
        _requestRefresh = requestRefresh;
    }

    internal bool UsesInlineEvaluation => _formattedFallbackCommandItem is not null;

    internal FallbackExecutionPolicy GetExecutionPolicy() => _getPolicy();

    internal bool UpdateSynchronous(string query, IListItem sourceItem)
    {
        return UpdateCore(query, sourceItem, inlineOnly: false);
    }

    internal bool UpdateInline(string query, IListItem sourceItem)
    {
        if (_formattedFallbackCommandItem is null)
        {
            return false;
        }

        return UpdateCore(query, sourceItem, inlineOnly: true);
    }

    internal IListItem[] GetCurrentItems()
    {
        lock (_stateLock)
        {
            return _queryState.QueryId == _queryState.LatestRequestedQueryId
                ? _queryState.Items
                : [];
        }
    }

    internal void CancelOutstandingQuery()
    {
        string currentQueryId;
        string activeAsyncQueryId;
        CancellationTokenSource? scheduledAsyncFallbackCts;

        lock (_stateLock)
        {
            currentQueryId = _queryState.LatestRequestedQueryId;
            activeAsyncQueryId = _activeAsyncFallbackQueryId;
            _activeAsyncFallbackQueryId = string.Empty;
            _pendingAsyncFallbackQuery = null;
            scheduledAsyncFallbackCts = _scheduledAsyncFallbackCts;
            _scheduledAsyncFallbackCts = null;
            _queryState = FallbackQueryState.Empty;
        }

        CancelAndDispose(scheduledAsyncFallbackCts);
        CancelQueryIfSupported(_fallbackCommandItem.FallbackHandler, activeAsyncQueryId);
        if (!string.Equals(activeAsyncQueryId, currentQueryId, StringComparison.Ordinal))
        {
            CancelQueryIfSupported(_fallbackCommandItem.FallbackHandler, currentQueryId);
        }
    }

    internal bool IsCurrentQueryId(string queryId)
    {
        lock (_stateLock)
        {
            return !string.IsNullOrEmpty(queryId) && _queryState.LatestRequestedQueryId == queryId;
        }
    }

    internal IFallbackCommandInvocationArgs? GetCurrentInvocationArgs()
    {
        lock (_stateLock)
        {
            if (string.IsNullOrEmpty(_queryState.LatestRequestedQueryId))
            {
                return null;
            }

            return new FallbackCommandInvocationArgs()
            {
                Query = _queryState.LatestRequestedQuery,
                QueryId = _queryState.LatestRequestedQueryId,
            };
        }
    }

    internal static bool IsRegexMatch(string pattern, string query)
    {
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        var regex = RegexCache.GetOrAdd(pattern, CreateFallbackRegex);
        return regex?.IsMatch(query) ?? false;
    }

    private bool UpdateCore(string query, IListItem sourceItem, bool inlineOnly)
    {
        if (!GetExecutionPolicy().ShouldEvaluate(query))
        {
            return SuppressQuery(query);
        }

        if (_formattedFallbackCommandItem is not null)
        {
            return ApplyFormattedSnapshot(query, sourceItem);
        }

        if (inlineOnly)
        {
            return false;
        }

        var (queryId, previousQueryId, hadVisibleResults) = BeginQuery(query);

        if (_asyncFallbackHandler is not null)
        {
            QueueAsyncQuery(query, queryId);
            return hadVisibleResults;
        }

        CancelQueryIfSupported(_fallbackCommandItem.FallbackHandler, previousQueryId);

        _fallbackCommandItem.FallbackHandler.UpdateQuery(query);
        var snapshotItems = string.IsNullOrWhiteSpace(sourceItem.Title)
            ? Array.Empty<IListItem>()
            : [sourceItem];

        var snapshotDefinitions = snapshotItems
            .Select(item => new FallbackSnapshotDefinition(item, true, null, null))
            .ToArray();

        return TryApplySnapshot(query, queryId, snapshotDefinitions, hadVisibleResults);
    }

    private bool SuppressQuery(string query)
    {
        var (queryId, _, hadVisibleResults) = BeginQuery(query);
        CancelPendingAndScheduledQueries(queryId);
        return TryApplySnapshot(query, queryId, [], hadVisibleResults);
    }

    private (string QueryId, string PreviousQueryId, bool HadVisibleResults) BeginQuery(string query)
    {
        lock (_stateLock)
        {
            var previousQueryId = _queryState.LatestRequestedQueryId;
            var hadVisibleResults = _queryState.Items.Length > 0 && _queryState.QueryId == previousQueryId;
            var queryId = Interlocked.Increment(ref _querySequence).ToString(System.Globalization.CultureInfo.InvariantCulture);

            _queryState = new FallbackQueryState(
                LatestRequestedQuery: query,
                LatestRequestedQueryId: queryId,
                Query: query,
                QueryId: queryId,
                Items: []);

            return (queryId, previousQueryId, hadVisibleResults);
        }
    }

    private bool ApplyFormattedSnapshot(string query, IListItem sourceItem)
    {
        if (_formattedFallbackCommandItem is null)
        {
            return false;
        }

        var (queryId, _, hadVisibleResults) = BeginQuery(query);

        if (_hostMatchedFallbackCommandItem is not null && !IsHostMatch(_hostMatchedFallbackCommandItem, query))
        {
            return TryApplySnapshot(query, queryId, [], hadVisibleResults);
        }

        var formattedItems = new[]
        {
            new FallbackSnapshotDefinition(
                sourceItem,
                false,
                FormatTemplate(_formattedFallbackCommandItem.TitleTemplate, query),
                FormatTemplate(_formattedFallbackCommandItem.SubtitleTemplate, query)),
        };

        return TryApplySnapshot(query, queryId, formattedItems, hadVisibleResults);
    }

    private void QueueAsyncQuery(string query, string queryId)
    {
        CancellationTokenSource? scheduledAsyncFallbackCts;
        string activeAsyncQueryId;
        bool shouldSchedulePendingQuery;

        lock (_stateLock)
        {
            _pendingAsyncFallbackQuery = new PendingAsyncFallbackQuery(query, queryId);
            scheduledAsyncFallbackCts = _scheduledAsyncFallbackCts;
            _scheduledAsyncFallbackCts = null;
            activeAsyncQueryId = _activeAsyncFallbackQueryId;
            shouldSchedulePendingQuery = string.IsNullOrEmpty(activeAsyncQueryId);
        }

        CancelAndDispose(scheduledAsyncFallbackCts);
        CancelQueryIfSupported(_fallbackCommandItem.FallbackHandler, activeAsyncQueryId);

        if (shouldSchedulePendingQuery)
        {
            SchedulePendingAsyncQuery();
        }
    }

    private void SchedulePendingAsyncQuery()
    {
        PendingAsyncFallbackQuery? pendingQuery;
        CancellationTokenSource? delayCts = null;
        var delay = GetExecutionPolicy().Delay;

        lock (_stateLock)
        {
            if (_pendingAsyncFallbackQuery is null || !string.IsNullOrEmpty(_activeAsyncFallbackQueryId))
            {
                return;
            }

            pendingQuery = _pendingAsyncFallbackQuery;
            if (delay > TimeSpan.Zero)
            {
                delayCts = new CancellationTokenSource();
                _scheduledAsyncFallbackCts = delayCts;
            }
            else
            {
                _activeAsyncFallbackQueryId = pendingQuery.Value.QueryId;
                _pendingAsyncFallbackQuery = null;
            }
        }

        if (pendingQuery is null)
        {
            return;
        }

        if (delay <= TimeSpan.Zero)
        {
            StartAsyncQuery(pendingQuery.Value.Query, pendingQuery.Value.QueryId);
            return;
        }

        _ = WaitAndStartPendingAsyncQueryAsync(pendingQuery.Value.QueryId, delay, delayCts!);
    }

    private async Task WaitAndStartPendingAsyncQueryAsync(string queryId, TimeSpan delay, CancellationTokenSource delayCts)
    {
        try
        {
            await Task.Delay(delay, delayCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            delayCts.Dispose();
            return;
        }

        string? queryToStart = null;

        try
        {
            lock (_stateLock)
            {
                if (!ReferenceEquals(_scheduledAsyncFallbackCts, delayCts)
                    || _pendingAsyncFallbackQuery is not { } pendingQuery
                    || !string.Equals(pendingQuery.QueryId, queryId, StringComparison.Ordinal)
                    || !string.IsNullOrEmpty(_activeAsyncFallbackQueryId))
                {
                    return;
                }

                _scheduledAsyncFallbackCts = null;
                _pendingAsyncFallbackQuery = null;
                _activeAsyncFallbackQueryId = pendingQuery.QueryId;
                queryToStart = pendingQuery.Query;
            }
        }
        finally
        {
            delayCts.Dispose();
        }

        StartAsyncQuery(queryToStart, queryId);
    }

    private void StartAsyncQuery(string query, string queryId)
    {
        if (_asyncFallbackHandler is null)
        {
            CompleteAsyncQuery(queryId);
            return;
        }

        try
        {
            var operation = _asyncFallbackHandler.UpdateQueryAsync(query, queryId);
            _ = AwaitFallbackResultsAsync(operation, query, queryId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.ToString());
            CompleteAsyncQuery(queryId);
        }
    }

    private async Task AwaitFallbackResultsAsync(IAsyncOperation<IFallbackCommandResult> operation, string query, string queryId)
    {
        try
        {
            var result = await operation.AsTask().ConfigureAwait(false);
            var resultQuery = result?.Query ?? query;
            var resultQueryId = result?.QueryId ?? queryId;
            var items = CreateSnapshotItems(result?.Items);

            if (!TryApplySnapshot(resultQuery, resultQueryId, items, hadVisibleResults: false))
            {
                return;
            }

            if (items.Length > 0)
            {
                _requestRefresh();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.ToString());
        }
        finally
        {
            CompleteAsyncQuery(queryId);
        }
    }

    private void CompleteAsyncQuery(string queryId)
    {
        bool shouldSchedulePendingQuery;

        lock (_stateLock)
        {
            if (string.Equals(_activeAsyncFallbackQueryId, queryId, StringComparison.Ordinal))
            {
                _activeAsyncFallbackQueryId = string.Empty;
            }

            shouldSchedulePendingQuery = string.IsNullOrEmpty(_activeAsyncFallbackQueryId) && _pendingAsyncFallbackQuery is not null;
        }

        if (shouldSchedulePendingQuery)
        {
            SchedulePendingAsyncQuery();
        }
    }

    private FallbackSnapshotDefinition[] CreateSnapshotItems(IEnumerable<IListItem>? items)
    {
        if (items is null)
        {
            return [];
        }

        return items
            .Where(item => item is not null && !string.IsNullOrWhiteSpace(item.Title))
            .Select(item => new FallbackSnapshotDefinition(item, true, null, null))
            .ToArray();
    }

    private bool TryApplySnapshot(string query, string queryId, IReadOnlyList<FallbackSnapshotDefinition> items, bool hadVisibleResults)
    {
        lock (_stateLock)
        {
            if (_queryState.LatestRequestedQueryId != queryId)
            {
                return false;
            }

            var materializedItems = _materializeSnapshotItems(query, queryId, items);
            _queryState = _queryState with
            {
                Query = query,
                QueryId = queryId,
                Items = materializedItems,
            };

            return hadVisibleResults || materializedItems.Length > 0;
        }
    }

    private void CancelPendingAndScheduledQueries(string queryId)
    {
        string activeAsyncQueryId;
        CancellationTokenSource? scheduledAsyncFallbackCts;

        lock (_stateLock)
        {
            activeAsyncQueryId = _activeAsyncFallbackQueryId;
            _activeAsyncFallbackQueryId = string.Empty;
            _pendingAsyncFallbackQuery = null;
            scheduledAsyncFallbackCts = _scheduledAsyncFallbackCts;
            _scheduledAsyncFallbackCts = null;
        }

        CancelAndDispose(scheduledAsyncFallbackCts);
        CancelQueryIfSupported(_fallbackCommandItem.FallbackHandler, activeAsyncQueryId);
        if (!string.Equals(activeAsyncQueryId, queryId, StringComparison.Ordinal))
        {
            CancelQueryIfSupported(_fallbackCommandItem.FallbackHandler, queryId);
        }
    }

    private static void CancelQueryIfSupported(IFallbackHandler? fallbackHandler, string queryId)
    {
        if (string.IsNullOrEmpty(queryId) || fallbackHandler is not IFallbackHandler2 asyncFallbackHandler)
        {
            return;
        }

        try
        {
            asyncFallbackHandler.CancelQuery(queryId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.ToString());
        }
    }

    private static void CancelAndDispose(CancellationTokenSource? cancellationTokenSource)
    {
        if (cancellationTokenSource is null)
        {
            return;
        }

        try
        {
            cancellationTokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            cancellationTokenSource.Dispose();
        }
    }

    private static string FormatTemplate(string template, string query)
    {
        return string.IsNullOrEmpty(template)
            ? string.Empty
            : template.Replace("{query}", query, StringComparison.Ordinal);
    }

    private static Regex? CreateFallbackRegex(string pattern)
    {
        try
        {
            return new Regex(
                $"^(?:{pattern})$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.NonBacktracking,
                RegexTimeout);
        }
        catch (ArgumentException ex)
        {
            Logger.LogError(ex.ToString());
            return null;
        }
    }

    private static bool IsHostMatch(IHostMatchedFallbackCommandItem hostMatchedFallback, string query)
    {
        return hostMatchedFallback.MatchKind switch
        {
            HostMatchKind.Regex => IsRegexMatch(hostMatchedFallback.MatchValue, query),
            _ => !string.IsNullOrWhiteSpace(query),
        };
    }

    private readonly record struct FallbackQueryState(
        string LatestRequestedQuery,
        string LatestRequestedQueryId,
        string Query,
        string QueryId,
        IListItem[] Items)
    {
        public static readonly FallbackQueryState Empty = new(string.Empty, string.Empty, string.Empty, string.Empty, []);
    }

    private readonly record struct PendingAsyncFallbackQuery(string Query, string QueryId);
}

internal readonly record struct FallbackExecutionPolicy(TimeSpan Delay, uint MinQueryLength)
{
    internal static readonly FallbackExecutionPolicy Empty = new(TimeSpan.Zero, 0);

    internal bool ShouldEvaluate(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        return query.Trim().Length >= MinQueryLength;
    }

    internal TimeSpan GetDelayForQuery(string query) => ShouldEvaluate(query) ? Delay : TimeSpan.Zero;
}

internal readonly record struct FallbackSnapshotDefinition(
    IListItem SourceItem,
    bool ListenForSourceItemUpdates,
    string? TitleOverride,
    string? SubtitleOverride);

internal delegate IListItem[] MaterializeFallbackSnapshotItemsCallback(
    string query,
    string queryId,
    IReadOnlyList<FallbackSnapshotDefinition> snapshotItems);
