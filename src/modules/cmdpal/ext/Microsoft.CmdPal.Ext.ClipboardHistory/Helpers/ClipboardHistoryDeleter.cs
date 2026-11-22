// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;

/// <summary>
/// Removes an item from clipboard history after a paste, without racing the target app's clipboard read.
/// </summary>
internal static class ClipboardHistoryDeleter
{
    internal const int ClipboardCantOpenHResult = unchecked((int)0x800401D0); // CLIPBRD_E_CANT_OPEN

    internal static readonly TimeSpan DefaultDelayAfterPaste = TimeSpan.FromMilliseconds(400);
    internal static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMilliseconds(50);
    internal const int DefaultMaxAttempts = 5;

    internal static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(100);
    internal const int DefaultMaxPolls = 10;
    internal static readonly TimeSpan DefaultVerifyDelay = TimeSpan.FromMilliseconds(200);
    internal const int DefaultMaxDeleteRounds = 2;

    // Tolerates clock granularity between our timestamp and the history service's.
    private static readonly TimeSpan TimestampTolerance = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Removes the pasted item from clipboard history on a background thread, once the target app has had time to read the clipboard.
    /// </summary>
    /// <remarks>
    /// Putting the content back on the clipboard makes the history service add a new entry (with a new Id) asynchronously,
    /// so deleting only <paramref name="originalItem"/> is unreliable. This waits for that new entry and deletes it together
    /// with the original one. Deleting the current history entry also clears the live clipboard, which is fine once the
    /// target app has pasted.
    /// </remarks>
    /// <param name="originalItem">The history item that was pasted.</param>
    /// <param name="pasted">What was put on the clipboard for the paste.</param>
    /// <param name="setTime">A time captured before the content was put on the clipboard.</param>
    internal static void DeleteAfterPaste(ClipboardHistoryItem originalItem, PastedContent pasted, DateTimeOffset setTime)
    {
        ArgumentNullException.ThrowIfNull(originalItem);

        _ = Task.Run(() =>
        {
            try
            {
                Thread.Sleep(DefaultDelayAfterPaste);

                var itemsById = new Dictionary<string, ClipboardHistoryItem>();
                RemovePastedEntries(
                    () => ReadHistory(originalItem.Id, setTime, itemsById),
                    id => itemsById.TryGetValue(id, out var historyItem) && TryDeleteWithRetry(
                        () => Clipboard.DeleteItemFromHistory(historyItem),
                        Thread.Sleep,
                        DefaultMaxAttempts,
                        DefaultRetryDelay,
                        LogWarning),
                    Thread.Sleep,
                    originalItem.Id,
                    pasted,
                    setTime,
                    LogWarning);
            }
            catch (Exception ex)
            {
                LogWarning($"Failed to remove pasted item from clipboard history: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Waits (bounded) for the history entry created by the paste, deletes it and the original entry, and verifies they are gone.
    /// </summary>
    /// <returns><see langword="true"/> if no matching entry is left in history; otherwise <see langword="false"/>. Never throws.</returns>
    internal static bool RemovePastedEntries(
        Func<IReadOnlyList<ClipboardHistoryEntry>?> readHistory,
        Func<string, bool> deleteById,
        Action<TimeSpan> sleep,
        string originalId,
        PastedContent pasted,
        DateTimeOffset setTime,
        Action<string> logWarning,
        int maxPolls = DefaultMaxPolls,
        int maxDeleteRounds = DefaultMaxDeleteRounds)
    {
        ArgumentNullException.ThrowIfNull(readHistory);
        ArgumentNullException.ThrowIfNull(deleteById);
        ArgumentNullException.ThrowIfNull(sleep);
        ArgumentNullException.ThrowIfNull(logWarning);

        IReadOnlyList<string> targets = [];
        for (var poll = 1; poll <= maxPolls; poll++)
        {
            var history = readHistory();
            if (history is null)
            {
                logWarning("Could not read clipboard history; the pasted item may remain in history");
                return false;
            }

            targets = SelectEntriesToDelete(history, originalId, pasted, setTime, out var foundNewEntry);
            if (foundNewEntry)
            {
                break;
            }

            if (poll < maxPolls)
            {
                sleep(DefaultPollInterval);
            }
        }

        for (var round = 1; targets.Count > 0; round++)
        {
            foreach (var id in targets)
            {
                deleteById(id);
            }

            // Deletion is processed asynchronously too; check what is left once it has settled.
            sleep(DefaultVerifyDelay);
            var history = readHistory();
            if (history is null)
            {
                logWarning("Could not read clipboard history to verify the pasted item was removed");
                return false;
            }

            targets = SelectEntriesToDelete(history, originalId, pasted, setTime, out _);
            if (targets.Count > 0 && round >= maxDeleteRounds)
            {
                logWarning($"Pasted item is still in clipboard history after delete ({targets.Count} matching entr{(targets.Count == 1 ? "y" : "ies")} left)");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Picks the original history entry (if still present) and the newest entry created by the paste (matching what was put on the clipboard).
    /// </summary>
    internal static IReadOnlyList<string> SelectEntriesToDelete(
        IReadOnlyList<ClipboardHistoryEntry> history,
        string originalId,
        PastedContent pasted,
        DateTimeOffset setTime,
        out bool foundNewEntry)
    {
        ArgumentNullException.ThrowIfNull(history);

        var targets = new List<string>();
        ClipboardHistoryEntry? newest = null;
        foreach (var entry in history)
        {
            if (entry.Id == originalId)
            {
                targets.Add(entry.Id);
            }
            else if (entry.Timestamp >= setTime - TimestampTolerance && Matches(entry, pasted) && (newest is null || entry.Timestamp > newest.Timestamp))
            {
                newest = entry;
            }
        }

        foundNewEntry = newest is not null;
        if (newest is not null)
        {
            targets.Add(newest.Id);
        }

        return targets;
    }

    private static bool Matches(ClipboardHistoryEntry entry, PastedContent pasted) => pasted.Format switch
    {
        ClipboardFormat.Text => entry.Text is not null && string.Equals(entry.Text, pasted.Text, StringComparison.Ordinal),
        ClipboardFormat.Image => entry.HasBitmap && entry.Text is null,
        _ => false,
    };

    private static IReadOnlyList<ClipboardHistoryEntry>? ReadHistory(string originalId, DateTimeOffset setTime, Dictionary<string, ClipboardHistoryItem> itemsById)
    {
        ClipboardHistoryItemsResult result;
        try
        {
            result = Clipboard.GetHistoryItemsAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            LogWarning($"Failed to read clipboard history: {ex.Message}");
            return null;
        }

        if (result.Status != ClipboardHistoryItemsResultStatus.Success)
        {
            return null;
        }

        itemsById.Clear();
        var entries = new List<ClipboardHistoryEntry>();
        foreach (var item in result.Items)
        {
            // Only the original item and entries created since the paste can be targets; skip reading the rest.
            if (item.Id != originalId && item.Timestamp < setTime - TimestampTolerance)
            {
                continue;
            }

            string? text = null;
            try
            {
                if (item.Content.Contains(StandardDataFormats.Text))
                {
                    text = item.Content.GetTextAsync().AsTask().GetAwaiter().GetResult();
                }
            }
            catch (Exception)
            {
                // Unreadable entries can't be matched by text; they can still match by Id.
            }

            itemsById[item.Id] = item;
            entries.Add(new ClipboardHistoryEntry(item.Id, item.Timestamp, text, item.Content.Contains(StandardDataFormats.Bitmap)));
        }

        return entries;
    }

    /// <returns><see langword="true"/> if the item was deleted; otherwise <see langword="false"/>. Never throws.</returns>
    internal static bool TryDeleteWithRetry(
        Func<bool> deleteItem,
        Action<TimeSpan> sleep,
        int maxAttempts,
        TimeSpan retryDelay,
        Action<string> logWarning)
    {
        ArgumentNullException.ThrowIfNull(deleteItem);
        ArgumentNullException.ThrowIfNull(sleep);
        ArgumentNullException.ThrowIfNull(logWarning);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (deleteItem())
                {
                    return true;
                }

                logWarning("Clipboard history item was not deleted after paste (it may already be gone)");
                return false;
            }
            catch (COMException ex) when (ex.HResult == ClipboardCantOpenHResult && attempt < maxAttempts)
            {
                // Another app (typically the paste target) has the clipboard open; back off and retry.
                sleep(retryDelay * attempt);
            }
            catch (Exception ex)
            {
                logWarning($"Failed to delete clipboard history item after paste (attempt {attempt}/{maxAttempts}): {ex.Message}");
                return false;
            }
        }

        return false;
    }

    private static void LogWarning(string message) =>
        ExtensionHost.LogMessage(new LogMessage { Message = message, State = MessageState.Warning });
}
