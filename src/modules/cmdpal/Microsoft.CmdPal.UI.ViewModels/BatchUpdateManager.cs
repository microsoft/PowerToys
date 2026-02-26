// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.UI.ViewModels;

internal static partial class BatchUpdateManager
{
    private const int ExpectedBatchSize = 32;

    // 30 ms chosen empirically to balance responsiveness and batching:
    // - Keeps perceived latency low (< ~50 ms) for user-visible updates.
    // - Still allows multiple COM/background events to be coalesced into a single batch.
    private static readonly TimeSpan BatchDelay = TimeSpan.FromMilliseconds(30);
    private static readonly ConcurrentQueue<IBatchUpdateTarget> DirtyQueue = [];
    private static readonly Timer Timer = new(static state => Flush((ILogger)state), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

    private static InterlockedBoolean _isFlushScheduled;

    /// <summary>
    /// Enqueue a target for batched processing. Safe to call from any thread (including COM callbacks).
    /// </summary>
    public static void Queue(IBatchUpdateTarget target, ILogger logger)
    {
        if (!target.TryMarkBatchQueued())
        {
            return; // already queued in current batch window
        }

        DirtyQueue.Enqueue(target);
        TryScheduleFlush(logger);
    }

    private static void TryScheduleFlush(ILogger logger)
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
            Log_ArmBatchTimerFailure(logger, ex);
        }
    }

    private static void Flush(ILogger logger)
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
            ApplyBatch(drained, logger);
        }
        catch (Exception ex)
        {
            // Don't kill the timer thread.
            Log_BatchFlushFailure(logger, ex);
        }
        finally
        {
            _isFlushScheduled.Clear();
            TryScheduleFlush(logger);
        }
    }

    private static void ApplyBatch(List<IBatchUpdateTarget> items, ILogger logger)
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
                Log_FailureApplyingUpdate(logger, ex);
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to apply pending updates for a batched target.")]
    static partial void Log_FailureApplyingUpdate(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Batch flush failed.")]
    static partial void Log_BatchFlushFailure(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to arm batch timer.")]
    static partial void Log_ArmBatchTimerFailure(ILogger logger, Exception ex);
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
