// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels;

public record RecentCommandsManager : IRecentCommandsManager
{
    // Recency half-life: a command's recency contribution halves every this many days. Three
    // days balances an interactive launcher's usage rhythm - commands touched in the last few
    // days still feel "recent", while month-old one-offs decay toward zero and stop crowding
    // out newer habits.
    internal const double HalfLifeDays = 3.0;

    // Baseline points for a just-used command (recency == 1) before the frequency term. Kept
    // near the previous top recency bucket so the frecency signal keeps roughly the same
    // influence in the ranker's within-tier math (MainListRanker.FrecencyScale == 1.0).
    internal const double BaseWeight = 10.0;

    // Points added per unit of log2(uses + 1), scaled by recency. log() keeps heavy usage
    // helpful without letting it dominate recency (uses 1 -> +10, 7 -> +30, 31 -> +50).
    internal const double FrequencyWeight = 10.0;

    // Upper bound on the returned weight. Holds the signal in the ~0..70 range the previous
    // implementation produced, so tier ordering is unaffected (the tier always dominates and
    // frecency only reorders within a tier).
    internal const double MaxWeight = 70.0;

    // Retain a few hundred commands. Large enough to remember habitual commands across weeks
    // of use, small enough that the persisted JSON stays tiny (tens of KB). The old cap of 50
    // evicted still-useful history within a single busy session on active machines.
    internal const int MaxHistoryEntries = 500;

    // Defensive fallback for any history item that carries a default (missing) LastUsed - for
    // example a value constructed without a timestamp, or state written by a build that predates
    // the LastUsed field. Such an item is treated as used one day ago: recent enough that it
    // still ranks, mild enough that a single fresh use outranks it, and uniform so a group of
    // them falls back to Uses (frequency) ordering instead of collapsing to all-equal or zero.
    // In practice this rarely fires: earlier builds never actually persisted history (the
    // internal History property was dropped by the serializer, see [JsonInclude] below), so
    // upgrading users start from an empty store rather than one full of timestamp-less items.
    internal static readonly TimeSpan LegacyBackdate = TimeSpan.FromDays(1);

    private ImmutableList<HistoryItem>? _history = ImmutableList<HistoryItem>.Empty;

    // Cached commandId -> entry lookup over History, rebuilt lazily whenever History is
    // (re)assigned and never persisted. ScoreTopLevelItem calls GetCommandHistoryWeight for
    // every candidate item on every keystroke, and History can hold up to MaxHistoryEntries
    // (500) entries, so a plain linear scan would be O(items x history) per keystroke. The
    // dictionary keeps each lookup O(1) so the hot path stays cheap as the store grows.
    [JsonIgnore]
    private Dictionary<string, HistoryItem>? _index;

    // Persisted so recent-command frecency (including the LastUsed timestamps) survives a
    // restart. [JsonInclude] is required because the property is internal; without it the
    // source-generated serializer emits an empty object and history is silently dropped.
    // Old persisted state (an empty object) still deserializes fine - History stays empty.
    [JsonInclude]
    internal ImmutableList<HistoryItem> History
    {
        get => _history ?? ImmutableList<HistoryItem>.Empty;
        init
        {
            _history = value;

            // Invalidate the cached lookup so it is rebuilt from the new list on next use.
            // A record 'with' copy carries over the old field, so this reset is what keeps
            // the index from going stale after History changes.
            _index = null;
        }
    }

    private Dictionary<string, HistoryItem> Index
    {
        get
        {
            if (_index is null)
            {
                // Ordinal to match the string '==' comparison the previous linear scan used.
                var map = new Dictionary<string, HistoryItem>(StringComparer.Ordinal);
                foreach (var item in History)
                {
                    // History is most-recent-first and command ids are unique in it, but keep
                    // the first (most recent) occurrence if a duplicate ever slips in so the
                    // lookup matches the old FirstOrDefault behavior.
                    map.TryAdd(item.CommandId, item);
                }

                _index = map;
            }

            return _index;
        }
    }

    public RecentCommandsManager()
    {
    }

    /// <summary>
    /// Forces the lazy command-id lookup (<see cref="Index"/>) to be built now, on the calling
    /// thread. The build itself is not thread-safe (it populates a shared dictionary field without
    /// locking), so callers that are about to score items in parallel MUST call this once,
    /// single-threaded, before the parallel loop. Once built, <see cref="GetCommandHistoryWeight(string)"/>
    /// only performs concurrent dictionary reads, which are safe.
    /// </summary>
    public void PrewarmIndex() => _ = Index;

    public int GetCommandHistoryWeight(string commandId)
        => GetCommandHistoryWeight(commandId, DateTimeOffset.UtcNow);

    /// <summary>
    /// Computes the time-decayed frecency weight for a command relative to <paramref name="now"/>.
    /// Recency uses an exponential half-life decay and frequency uses log(uses); the two are
    /// combined so recency leads while frequency amplifies. The parameterless overload evaluates at
    /// the current time; this overload lets callers pin a single evaluation time across a batch (and
    /// tests inject a fixed time).
    /// </summary>
    public int GetCommandHistoryWeight(string commandId, DateTimeOffset now)
    {
        if (!Index.TryGetValue(commandId, out var entry))
        {
            return 0;
        }

        // Migrate items with a default (missing) timestamp to a mild backdate so they degrade
        // to Uses-ordering instead of appearing brand-new or invisible. See LegacyBackdate.
        var lastUsed = entry.LastUsed == default ? now - LegacyBackdate : entry.LastUsed;

        // Clamp age at zero so a slightly-future timestamp (e.g. clock skew) can't amplify.
        var ageDays = Math.Max(0.0, (now - lastUsed).TotalDays);

        // Exponential time decay: recency in (0, 1], halving every HalfLifeDays.
        var recency = Math.Pow(2.0, -ageDays / HalfLifeDays);

        // Frequency via log2 so heavy usage helps but can't outrun recency.
        var frequency = Math.Log(entry.Uses + 1, 2);

        var weight = recency * (BaseWeight + (FrequencyWeight * frequency));

        return (int)Math.Round(Math.Clamp(weight, 0.0, MaxWeight));
    }

    /// <summary>
    /// Returns a new RecentCommandsManager with the given command added/promoted in history.
    /// Pure function - does not mutate this instance.
    /// </summary>
    public RecentCommandsManager WithHistoryItem(string commandId)
        => WithHistoryItem(commandId, DateTimeOffset.UtcNow);

    /// <summary>
    /// Records a use of <paramref name="commandId"/> at the explicit time <paramref name="now"/>.
    /// The public overload uses the current time; this overload lets tests inject
    /// strictly-increasing timestamps so time-decay behavior is deterministic.
    /// </summary>
    internal RecentCommandsManager WithHistoryItem(string commandId, DateTimeOffset now)
    {
        var existing = History.FirstOrDefault(item => item.CommandId == commandId);
        ImmutableList<HistoryItem> newHistory;

        if (existing is not null)
        {
            newHistory = History.Remove(existing);
            var updated = existing with { Uses = existing.Uses + 1, LastUsed = now };
            newHistory = newHistory.Insert(0, updated);
        }
        else
        {
            var newItem = new HistoryItem { CommandId = commandId, Uses = 1, LastUsed = now };
            newHistory = History.Insert(0, newItem);
        }

        if (newHistory.Count > MaxHistoryEntries)
        {
            newHistory = newHistory.RemoveRange(MaxHistoryEntries, newHistory.Count - MaxHistoryEntries);
        }

        return this with { History = newHistory };
    }
}

public interface IRecentCommandsManager
{
    int GetCommandHistoryWeight(string commandId);

    /// <summary>
    /// Computes the frecency weight for a command relative to an explicit evaluation time. Callers
    /// that score a whole batch should capture a single <paramref name="now"/> once and pass it for
    /// every item so the batch is scored against one consistent time snapshot.
    /// </summary>
    int GetCommandHistoryWeight(string commandId, DateTimeOffset now);

    RecentCommandsManager WithHistoryItem(string commandId);

    /// <summary>
    /// Builds any lazily-initialized internal state (e.g. the command-id lookup) on the calling
    /// thread so that subsequent <see cref="GetCommandHistoryWeight(string)"/> calls are safe to
    /// issue concurrently. Callers that score items in parallel must invoke this once, single-
    /// threaded, before the parallel loop.
    /// </summary>
    void PrewarmIndex();
}
