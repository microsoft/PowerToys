// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.MainPage;

/// <summary>
/// Owns all of the main-page search telemetry state and emission, so <see cref="MainListPage"/>
/// stays responsible for producing results and this type is the single, clearly-identifiable owner
/// of the (privacy-safe) telemetry. Everything here is opt-in and non-identifying: search events
/// carry only query LENGTH, result count and latency; selection events carry only query LENGTH, the
/// invoked item's visible rank and its ranker tier - never the raw query text or item content.
///
/// Search events are emitted only when a query settles (trailing-edge debounce) so we never send an
/// event on every keystroke; selection events are emitted only when the user invokes a result. All
/// emission is measured at boundaries, never inside the per-item scoring loop.
/// </summary>
internal sealed partial class MainListPageSearchTelemetry : IDisposable
{
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(600);

    private readonly ThrottledDebouncedAction _resultsDebounce;
    private readonly Lock _pendingLock = new();
    private (int QueryLength, int ResultCount, long LatencyMs) _pendingResults;

    // Snapshots of the most recent rendered search results, read off the hot path (only when the
    // user invokes a result) to resolve the invoked item's visible rank and ranker tier. The scored
    // inputs and query length are captured together with the rendered items at render time, so
    // selection telemetry resolves rank, tier and query length from one coherent generation even
    // after a newer query has already published fresh scored fields.
    private IReadOnlyList<IListItem>? _lastViewItems;
    private IReadOnlyList<RoScored<IListItem>>? _lastScoredGlobalFallbacks;
    private RoScored<IListItem>[]? _lastViewFilteredItems;
    private RoScored<IListItem>[]? _lastViewFilteredApps;
    private IEnumerable<RoScored<IListItem>>? _lastViewFallbackItems;
    private int _lastViewQueryLength;

    public MainListPageSearchTelemetry()
    {
        _resultsDebounce = new ThrottledDebouncedAction(EmitPendingResults, SettleDelay);
    }

    // Stores the latest settled-search metrics and (re)arms the debounce. Only the query LENGTH is
    // retained - the query text is never stored for telemetry.
    public void QueueSearchResults(int queryLength, int resultCount, long latencyMs)
    {
        lock (_pendingLock)
        {
            _pendingResults = (queryLength, resultCount, latencyMs);
        }

        _resultsDebounce.Invoke();
    }

    // Snapshots the rendered order plus every scored input and the query length together, so a later
    // selection resolves an invoked item's rank, tier and query length from this one generation off
    // the hot path. These are plain reference assignments - no extra allocation.
    public void CaptureSearchView(
        IReadOnlyList<IListItem> renderedItems,
        RoScored<IListItem>[]? filteredItems,
        RoScored<IListItem>[]? filteredApps,
        IReadOnlyList<RoScored<IListItem>>? scoredGlobalFallbacks,
        IEnumerable<RoScored<IListItem>>? fallbackItems,
        int queryLength)
    {
        _lastViewItems = renderedItems;
        _lastScoredGlobalFallbacks = scoredGlobalFallbacks;
        _lastViewFilteredItems = filteredItems;
        _lastViewFilteredApps = filteredApps;
        _lastViewFallbackItems = fallbackItems;
        _lastViewQueryLength = queryLength;
    }

    // Drops any pending settled-search event without emitting it. Used when an alias query supersedes
    // a normal query whose telemetry is still pending in the debounce.
    public void CancelPendingResults() => _resultsDebounce.Cancel();

    // Drops any pending settled-search event and forgets the last rendered search view, so a cleared
    // query never emits and a subsequent selection resolves to nothing.
    public void ClearSearchView()
    {
        _resultsDebounce.Cancel();
        _lastViewItems = null;
        _lastScoredGlobalFallbacks = null;
        _lastViewFilteredItems = null;
        _lastViewFilteredApps = null;
        _lastViewFallbackItems = null;
        _lastViewQueryLength = 0;
    }

    // Emits selection telemetry when the user invokes a result during an active search. Runs only on
    // invoke (a deliberate, infrequent user action - never on the typing/scoring path) and captures
    // only non-identifying aggregates: the query LENGTH, the invoked item's visible rank, and its
    // ranker tier. Nothing is emitted for the default (no-search) view, or when the invoked item is
    // not among the last rendered search results.
    public void ReportSelection(IListItem invoked, Separator resultsSeparator, Separator fallbacksSeparator)
    {
        // Resolve everything from the last rendered search-view snapshot so the invoked item's rank,
        // tier, and the reported query length all come from one generation. If the last render was
        // the default (no-search) view, _lastViewItems is null and nothing is emitted.
        var lastView = _lastViewItems;
        if (lastView is null || _lastViewQueryLength <= 0)
        {
            return;
        }

        var index = ResolveVisibleIndex(lastView, invoked, resultsSeparator, fallbacksSeparator);
        if (index < 0)
        {
            return;
        }

        var packed = (_lastViewFilteredItems ?? Enumerable.Empty<RoScored<IListItem>>())
            .Concat(_lastViewFilteredApps ?? Enumerable.Empty<RoScored<IListItem>>())
            .Concat(_lastScoredGlobalFallbacks ?? Enumerable.Empty<RoScored<IListItem>>());

        var tier = ResolveSelectedTier(invoked, packed, _lastViewFallbackItems);
        if (tier == RankTier.None)
        {
            return;
        }

        WeakReferenceMessenger.Default.Send(BuildSearchSelectedMessage(_lastViewQueryLength, index, tier));
    }

    private void EmitPendingResults()
    {
        (int QueryLength, int ResultCount, long LatencyMs) snapshot;
        lock (_pendingLock)
        {
            snapshot = _pendingResults;
        }

        if (snapshot.QueryLength <= 0)
        {
            return;
        }

        WeakReferenceMessenger.Default.Send(
            BuildSearchResultsMessage(snapshot.QueryLength, snapshot.ResultCount, snapshot.LatencyMs));
    }

    // Builds the settled-search telemetry payload from a query string, capturing only its LENGTH.
    // Exposed for tests to prove the raw query text is never carried.
    internal static TelemetrySearchResultsMessage BuildSearchResultsMessage(string query, int resultCount, long latencyMs)
        => BuildSearchResultsMessage(query?.Length ?? 0, resultCount, latencyMs);

    internal static TelemetrySearchResultsMessage BuildSearchResultsMessage(int queryLength, int resultCount, long latencyMs)
    {
        var length = Math.Max(queryLength, 0);
        var count = Math.Max(resultCount, 0);
        var latency = latencyMs < 0 ? 0UL : (ulong)latencyMs;
        return new TelemetrySearchResultsMessage(length, count, count == 0, latency);
    }

    // Builds the selection telemetry payload, capturing only the query LENGTH, the selected rank,
    // and the ranker tier. Exposed for tests to prove the raw query text is never carried.
    internal static TelemetrySearchResultSelectedMessage BuildSearchSelectedMessage(string query, int selectedIndex, RankTier selectedTier)
        => BuildSearchSelectedMessage(query?.Length ?? 0, selectedIndex, selectedTier);

    internal static TelemetrySearchResultSelectedMessage BuildSearchSelectedMessage(int queryLength, int selectedIndex, RankTier selectedTier)
        => new(Math.Max(queryLength, 0), selectedIndex, selectedTier);

    // Zero-based visible rank of an invoked item within the rendered results, skipping the section
    // separators. Returns -1 when the item is not present (e.g. it was invoked from a different view).
    internal static int ResolveVisibleIndex(IReadOnlyList<IListItem>? renderedResults, IListItem invoked, params IListItem[] separators)
    {
        if (renderedResults is null)
        {
            return -1;
        }

        var visible = 0;
        foreach (var item in renderedResults)
        {
            var isSeparator = false;
            foreach (var separator in separators)
            {
                if (ReferenceEquals(item, separator))
                {
                    isSeparator = true;
                    break;
                }
            }

            if (isSeparator)
            {
                continue;
            }

            if (ReferenceEquals(item, invoked))
            {
                return visible;
            }

            visible++;
        }

        return -1;
    }

    // Resolves the ranker tier of an invoked item. Packed sources (commands, apps, global
    // fallbacks) decode their tier via MainListRanker.TierOf; common fallbacks carry rank-based
    // (non-packed) scores, so they are reported at the fallback floor. Returns None when the item
    // is not found in any source.
    internal static RankTier ResolveSelectedTier(
        IListItem invoked,
        IEnumerable<RoScored<IListItem>>? packedResults,
        IEnumerable<RoScored<IListItem>>? fallbackResults)
    {
        if (packedResults is not null)
        {
            foreach (var scored in packedResults)
            {
                if (ReferenceEquals(scored.Item, invoked))
                {
                    return MainListRanker.TierOf(scored.Score);
                }
            }
        }

        if (fallbackResults is not null)
        {
            foreach (var scored in fallbackResults)
            {
                if (ReferenceEquals(scored.Item, invoked))
                {
                    return RankTier.FallbackFloor;
                }
            }
        }

        return RankTier.None;
    }

    public void Dispose() => _resultsDebounce.Dispose();
}
