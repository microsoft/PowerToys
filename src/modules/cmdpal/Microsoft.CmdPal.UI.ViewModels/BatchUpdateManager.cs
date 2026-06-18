// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.Common.Helpers;

namespace Microsoft.CmdPal.UI.ViewModels;

internal static class BatchUpdateManager
{
    private const int ExpectedBatchSize = 32;

    // 30 ms chosen empirically to balance responsiveness and batching:
    // - Keeps perceived latency low (< ~50 ms) for user-visible updates.
    // - Still allows multiple COM/background events to be coalesced into a single batch.
    private static readonly TimeSpan BatchDelay = TimeSpan.FromMilliseconds(40);
    private static readonly ConcurrentQueue<IBatchUpdateTarget> DirtyQueue = [];
    private static readonly Timer Timer = new(static _ => Flush(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

    private static InterlockedBoolean _isFlushScheduled;

    /// <summary>
    /// Enqueue a target for batched processing. Safe to call from any thread (including COM callbacks).
    /// </summary>
    public static void Queue(IBatchUpdateTarget target)
    {
        if (!target.TryMarkBatchQueued())
        {
            return; // already queued in current batch window
        }

        DirtyQueue.Enqueue(target);
        TryScheduleFlush();
    }

    private static void TryScheduleFlush()
    {
        if (!_isFlushScheduled.Set())
        {
            return;
        }

        if (DirtyQueue.IsEmpty)
        {
            _isFlushScheduled.Clear();

            if (DirtyQueue.IsEmpty)
            {
                return;
            }

            if (!_isFlushScheduled.Set())
            {
                return;
            }
        }

        try
        {
            Timer.Change(BatchDelay, Timeout.InfiniteTimeSpan);
        }
        catch (Exception ex)
        {
            _isFlushScheduled.Clear();
            CoreLogger.LogError("Failed to arm batch timer.", ex);
        }
    }

    private static void Flush()
    {
        try
        {
            var drained = new List<IBatchUpdateTarget>(ExpectedBatchSize);
            while (DirtyQueue.TryDequeue(out var item))
            {
                drained.Add(item);
            }

            if (drained.Count == 0)
            {
                return;
            }

            // LOAD BEARING:
            // ApplyPendingUpdates must run on a background thread.
            // The VM itself is responsible for marshaling UI notifications to its _uiScheduler.
            ApplyBatch(drained);
        }
        catch (Exception ex)
        {
            // Don't kill the timer thread.
            CoreLogger.LogError("Batch flush failed.", ex);
        }
        finally
        {
            _isFlushScheduled.Clear();
            TryScheduleFlush();
        }
    }

    private static void ApplyBatch(List<IBatchUpdateTarget> items)
    {
        // Runs on the Timer callback thread (ThreadPool). That's fine: background work only.
        foreach (var item in items)
        {
            // Allow re-queueing immediately if more COM events arrive during apply.
            item.ClearBatchQueued();

            try
            {
                item.ApplyPendingUpdates();
            }
            catch (Exception ex)
            {
                CoreLogger.LogError("Failed to apply pending updates for a batched target.", ex);
            }
        }
    }
}

internal interface IBatchUpdateTarget
{
    /// <summary>Gets UI scheduler (used by targets internally for UI marshaling). Kept here for diagnostics / consistency.</summary>
    TaskScheduler UIScheduler { get; }

    /// <summary>Apply any coalesced updates. Must be safe to call on a background thread.</summary>
    void ApplyPendingUpdates();

    /// <summary>De-dupe gate: returns true only for the first enqueue until cleared.</summary>
    bool TryMarkBatchQueued();

    /// <summary>Clear the de-dupe gate so the item can be queued again.</summary>
    void ClearBatchQueued();
}
