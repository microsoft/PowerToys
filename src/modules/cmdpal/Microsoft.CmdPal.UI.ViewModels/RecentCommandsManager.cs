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

    // Legacy history items (persisted before LastUsed existed) deserialize with a default
    // timestamp. Treat them as used one day ago: recent enough that they still rank, mild
    // enough that a single fresh use outranks them, and uniform so their relative order falls
    // back to Uses (frequency) instead of collapsing to all-equal or zero.
    internal static readonly TimeSpan LegacyBackdate = TimeSpan.FromDays(1);

    private ImmutableList<HistoryItem>? _history = ImmutableList<HistoryItem>.Empty;

    // Persisted so recent-command frecency (including the LastUsed timestamps) survives a
    // restart. [JsonInclude] is required because the property is internal; without it the
    // source-generated serializer emits an empty object and history is silently dropped.
    // Old persisted state (an empty object) still deserializes fine - History stays empty.
    [JsonInclude]
    internal ImmutableList<HistoryItem> History
    {
        get => _history ?? ImmutableList<HistoryItem>.Empty;
        init => _history = value;
    }

    public RecentCommandsManager()
    {
    }

    public int GetCommandHistoryWeight(string commandId)
        => GetCommandHistoryWeight(commandId, DateTimeOffset.UtcNow);

    /// <summary>
    /// Computes the time-decayed frecency weight for a command relative to <paramref name="now"/>.
    /// Recency uses an exponential half-life decay and frequency uses log(uses); the two are
    /// combined so recency leads while frequency amplifies. The public overload evaluates at
    /// the current time; this overload exists so tests can inject a fixed evaluation time.
    /// </summary>
    internal int GetCommandHistoryWeight(string commandId, DateTimeOffset now)
    {
        var entry = History.FirstOrDefault(item => item.CommandId == commandId);
        if (entry is null)
        {
            return 0;
        }

        // Migrate legacy entries (no timestamp) to a mild backdate so they degrade to
        // Uses-ordering instead of appearing brand-new or invisible.
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

    RecentCommandsManager WithHistoryItem(string commandId);
}
