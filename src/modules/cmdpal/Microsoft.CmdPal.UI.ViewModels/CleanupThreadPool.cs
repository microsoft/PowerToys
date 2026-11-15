// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using ManagedCommon;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Centralize cleanup work to track pending cleanup tasks and log failures. Doesn't solve our problems,
/// but at least we can track the cleanup tasks, and trace them back to the original callsites when
/// we refactor the cleanup code to something that actually makes sense.
/// </summary>
internal static class CleanupThreadPool
{
    private static readonly ConcurrentDictionary<Task, PendingCleanup> PendingCleanupTasks = new();

    public static PendingCleanup[] Pending => PendingCleanupTasks.Values.ToArray();

    public static Task Queue(Action cleanup, string description)
    {
        var queuedAt = DateTimeOffset.UtcNow;
        var task = Task.Run(cleanup);
        PendingCleanupTasks.TryAdd(task, new PendingCleanup(task, description, queuedAt));

        return task.ContinueWith(
            completed =>
            {
                PendingCleanupTasks.TryRemove(completed, out _);
                if (completed.Exception is { } exception)
                {
                    Logger.LogError($"Background cleanup failed: {description}", exception);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    internal sealed record PendingCleanup(Task Task, string Description, DateTimeOffset QueuedAt);
}
