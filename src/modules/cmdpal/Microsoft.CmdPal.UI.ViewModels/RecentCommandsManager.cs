// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels;

public record RecentCommandsManager : IRecentCommandsManager
{
    [JsonInclude]
    internal ImmutableList<HistoryItem> History { get; init; } = ImmutableList<HistoryItem>.Empty;

    public RecentCommandsManager()
    {
    }

    public int GetCommandHistoryWeight(string commandId)
    {
        var entry = History
            .Index()
            .Where(item => item.Item.CommandId == commandId)
            .FirstOrDefault();

        // These numbers are vaguely scaled so that "VS" will make "Visual Studio" the
        // match after one use.
        // Usually it has a weight of 84, compared to 109 for the VS cmd prompt
        if (entry.Item is not null)
        {
            var index = entry.Index;

            // First, add some weight based on how early in the list this appears
            var bucket = index switch
            {
                _ when index <= 2 => 35,
                _ when index <= 10 => 25,
                _ when index <= 15 => 15,
                _ when index <= 35 => 10,
                _ => 5,
            };

            // Then, add weight for how often this is used, but cap the weight from usage.
            var uses = Math.Min(entry.Item.Uses * 5, 35);

            return bucket + uses;
        }

        return 0;
    }

    /// <summary>
    /// Returns a new RecentCommandsManager with the given command added/promoted in history.
    /// Pure function — does not mutate this instance.
    /// </summary>
    public RecentCommandsManager WithHistoryItem(string commandId)
    {
        var existing = History.FirstOrDefault(item => item.CommandId == commandId);
        ImmutableList<HistoryItem> newHistory;

        if (existing is not null)
        {
            newHistory = History.Remove(existing);
            var updated = existing with { Uses = existing.Uses + 1 };
            newHistory = newHistory.Insert(0, updated);
        }
        else
        {
            var newItem = new HistoryItem { CommandId = commandId, Uses = 1 };
            newHistory = History.Insert(0, newItem);
        }

        if (newHistory.Count > 50)
        {
            newHistory = newHistory.RemoveRange(50, newHistory.Count - 50);
        }

        return this with { History = newHistory };
    }
}

public interface IRecentCommandsManager
{
    int GetCommandHistoryWeight(string commandId);

    RecentCommandsManager WithHistoryItem(string commandId);
}
