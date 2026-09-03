// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Owns the crash-recovery work that <see cref="JsonRpcExtensionService"/> starts when an
/// extension's Node.js process exits. Recovery is triggered from a process-exit event, so
/// whoever raised it cannot await it; tracking each recovery here (keyed by canonical
/// extension directory) gives uninstall, service stop, and disposal a way to cancel it,
/// await it, and clean it up instead of leaving a detached <c>Task.Run</c> running against
/// state that has already been torn down.
/// </summary>
/// <remarks>
/// Only crash recovery is tracked here. Watcher-driven install/uninstall work is
/// deliberately not, because that work is what calls <see cref="CancelAndDrainAsync"/>;
/// tracking it in the same per-directory drain would make a removal wait on itself.
///
/// Deadlock safety rests on cancel-before-await: every drain cancels the directory's token
/// first and only then awaits, so recovery blocked on the directory lifecycle gate is
/// released rather than waited on. Drains are also bounded by a timeout, and
/// <see cref="Dispose"/> uses a short bounded wait because it can run on the UI thread.
/// </remarks>
internal sealed partial class CrashRecoveryTracker : IDisposable
{
    private static readonly TimeSpan DefaultDrainTimeout = TimeSpan.FromSeconds(5);

    // Disposal can run on the UI thread during shutdown, so it waits only briefly and then
    // moves on. The work is already canceled at that point, so the worst case is a
    // straggler winding down after Dispose returns rather than a hung shutdown.
    private static readonly TimeSpan DisposeDrainTimeout = TimeSpan.FromSeconds(2);

    private readonly Lock _lock = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    // Set by CancelAll (service stop) and cleared by BeginCycle (a new load cycle). While
    // set, a process exit cannot start recovery, so tearing extensions down during shutdown
    // does not spawn restart work behind the shutdown.
    private bool _stopped;

    /// <summary>
    /// Gets the number of recovery tasks that have not completed yet, across all
    /// directories. Exposed for tests and diagnostics.
    /// </summary>
    public int InFlightCount
    {
        get
        {
            lock (_lock)
            {
                return _entries.Values.Sum(entry => entry.Tasks.Count(t => !t.IsCompleted));
            }
        }
    }

    /// <summary>
    /// Returns a value indicating whether recovery work is currently tracked for
    /// <paramref name="directory"/>. Exposed for tests and diagnostics.
    /// </summary>
    /// <param name="directory">The extension directory to check.</param>
    /// <returns>True when at least one recovery task for the directory is still tracked.</returns>
    public bool IsTracking(string directory)
    {
        var key = DirectoryLifecycleGate.Canonicalize(directory);
        lock (_lock)
        {
            return _entries.TryGetValue(key, out var entry) && entry.Tasks.Any(t => !t.IsCompleted);
        }
    }

    /// <summary>
    /// Re-opens the tracker for a new service load cycle after a previous
    /// <see cref="CancelAll"/>. Mirrors <see cref="ReloadCancellation.BeginCycle"/>, so a
    /// load that follows a stop can recover crashes again.
    /// </summary>
    public void BeginCycle()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            foreach (var entry in _entries.Values)
            {
                if (!entry.Draining && entry.Cancellation.IsCancellationRequested)
                {
                    entry.RetiredCancellations.Add(entry.Cancellation);
                    entry.Cancellation = new CancellationTokenSource();
                }
            }

            _stopped = false;
        }
    }

    /// <summary>
    /// Starts <paramref name="recovery"/> for <paramref name="directory"/> and tracks it, so
    /// it can later be canceled and awaited. The work runs on the thread pool with a
    /// per-directory cancellation token and never faults the tracked task: exceptions are
    /// logged and swallowed.
    /// </summary>
    /// <param name="directory">The extension directory the recovery belongs to.</param>
    /// <param name="recovery">The recovery work, which must honor the token it is handed.</param>
    /// <returns>
    /// True when the work was started and tracked; false when the tracker is disposed, the
    /// service is stopping, or this directory is currently being drained (uninstall), in
    /// which case there is nothing worth restarting.
    /// </returns>
    public bool TryTrack(string directory, Func<CancellationToken, Task> recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);

        var key = DirectoryLifecycleGate.Canonicalize(directory);
        lock (_lock)
        {
            if (_disposed || _stopped)
            {
                return false;
            }

            if (!_entries.TryGetValue(key, out var entry))
            {
                entry = new Entry();
                _entries[key] = entry;
            }

            if (entry.Draining)
            {
                return false;
            }

            var token = entry.Cancellation.Token;

            // The task is created under the lock and registered before the lock is released.
            // Its completion bookkeeping also takes the lock, so it can never prune an entry
            // before the task it belongs to has been recorded.
            var task = Task.Run(() => RunAsync(recovery, key, token));
            entry.Tasks.Add(task);

            _ = task.ContinueWith(
                completed => OnRecoveryCompleted(key, entry, completed),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);

            return true;
        }
    }

    /// <summary>
    /// Cancels and awaits every recovery task for one directory, then forgets the directory
    /// so a later (re)install can recover again. Used by the uninstall path: canceling before
    /// awaiting is what keeps this from deadlocking against recovery that is blocked on, or
    /// holding, the directory's lifecycle gate.
    /// </summary>
    /// <param name="directory">The extension directory being removed.</param>
    /// <param name="timeout">How long to wait before giving up and continuing. Defaults to five seconds.</param>
    /// <returns>A task that completes once the directory's recovery work has drained (or the wait timed out).</returns>
    public async Task CancelAndDrainAsync(string directory, TimeSpan? timeout = null)
    {
        var key = DirectoryLifecycleGate.Canonicalize(directory);

        Entry entry;
        Task[] pending;
        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out var existing))
            {
                existing = new Entry();
                _entries[key] = existing;
            }

            entry = existing;

            // Block new recovery for this directory for the duration of the drain, so the
            // uninstall cannot race a restart that was queued while it waited.
            entry.Draining = true;
            Cancel(entry);
            pending = [.. entry.Tasks];
        }

        var drained = await DrainAsync(pending, timeout ?? DefaultDrainTimeout, key).ConfigureAwait(false);

        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out var current) || !ReferenceEquals(current, entry))
            {
                return;
            }

            current.Tasks.RemoveAll(t => t.IsCompleted);

            if (!drained)
            {
                Logger.LogWarning($"Crash recovery for {key} is still winding down. The directory remains blocked from new recovery.");
            }
        }
    }

    /// <summary>
    /// Reopens a directory after its uninstall finishes. The caller must first drain recovery,
    /// then keep the directory blocked until the extension and its watcher are gone.
    /// </summary>
    public void CompleteDirectoryRemoval(string directory)
    {
        var key = DirectoryLifecycleGate.Canonicalize(directory);
        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out var entry) || !entry.Draining)
            {
                return;
            }

            entry.RemovalCompleted = true;
            entry.Tasks.RemoveAll(t => t.IsCompleted);
            if (entry.Tasks.Count != 0)
            {
                return;
            }

            _entries.Remove(key);
            DisposeCancellations(entry);
        }
    }

    /// <summary>
    /// Cancels every tracked recovery task and stops accepting new ones without waiting.
    /// Safe to call from any thread, including from a shutdown path that must not block.
    /// Pair it with <see cref="DrainAllAsync"/> when the caller can await.
    /// </summary>
    public void CancelAll()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _stopped = true;
            foreach (var entry in _entries.Values)
            {
                Cancel(entry);
            }
        }
    }

    /// <summary>
    /// Cancels every tracked recovery task and awaits them. Used by the service stop path,
    /// which can await, so no recovery outlives the stop.
    /// </summary>
    /// <param name="timeout">How long to wait before giving up and continuing. Defaults to five seconds.</param>
    /// <returns>A task that completes once all recovery work has drained (or the wait timed out).</returns>
    public async Task DrainAllAsync(TimeSpan? timeout = null)
    {
        Task[] pending;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _stopped = true;
            foreach (var entry in _entries.Values)
            {
                Cancel(entry);
            }

            pending = [.. _entries.Values.SelectMany(e => e.Tasks)];
        }

        await DrainAsync(pending, timeout ?? DefaultDrainTimeout, "all extension directories").ConfigureAwait(false);

        lock (_lock)
        {
            PruneCompleted();
        }
    }

    public void Dispose()
    {
        Task[] pending;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stopped = true;
            foreach (var entry in _entries.Values)
            {
                Cancel(entry);
            }

            pending = [.. _entries.Values.SelectMany(e => e.Tasks)];
        }

        // Bounded, because Dispose is synchronous and may run on the UI thread while a
        // recovery task is finishing work that needs another thread. The work is already
        // canceled, so a timeout leaves a winding-down straggler, never a hung shutdown.
        var drained = pending.Length == 0;
        if (!drained)
        {
            try
            {
                drained = Task.WhenAll(pending).Wait(DisposeDrainTimeout);
            }
            catch (AggregateException)
            {
                // Tracked tasks log their own failures; a faulted straggler is still drained.
                drained = true;
            }

            if (!drained)
            {
                Logger.LogWarning(
                    $"Timed out waiting for extension crash recovery to finish during disposal; {pending.Length} task(s) were canceled and left to unwind.");
            }
        }

        lock (_lock)
        {
            foreach (var entry in _entries.Values)
            {
                // Only dispose a source no one can still be waiting on. A straggler that
                // outlived the bounded wait still holds its token.
                if (drained)
                {
                    DisposeCancellations(entry);
                }
            }

            _entries.Clear();
        }
    }

    private static async Task RunAsync(Func<CancellationToken, Task> recovery, string key, CancellationToken token)
    {
        try
        {
            await recovery(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is the expected outcome of an uninstall, a stop, or disposal.
        }
        catch (Exception ex)
        {
            // Keep the tracked task from faulting, so a drain never observes an exception
            // for work the service already logs and recovers from.
            Logger.LogError($"Crash recovery for {key} failed.", ex);
        }
    }

    private static async Task<bool> DrainAsync(Task[] pending, TimeSpan timeout, string what)
    {
        if (pending.Length == 0)
        {
            return true;
        }

        try
        {
            await Task.WhenAll(pending).WaitAsync(timeout).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            Logger.LogWarning($"Timed out waiting for extension crash recovery to finish for {what}; continuing.");
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to drain extension crash recovery for {what}.", ex);
            return true;
        }
    }

    private static void Cancel(Entry entry)
    {
        try
        {
            entry.Cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The entry is already being torn down; its work has been canceled.
        }
    }

    private void OnRecoveryCompleted(string key, Entry entry, Task completed)
    {
        lock (_lock)
        {
            entry.Tasks.Remove(completed);

            if (entry.Tasks.Count != 0 || _disposed)
            {
                if (entry.Tasks.Count == 0 && _disposed)
                {
                    DisposeCancellations(entry);
                }

                return;
            }

            // A timed out uninstall leaves recovery blocked until directory removal
            // finishes. Once both sides are done, this continuation reopens the directory.
            if ((!entry.Draining || entry.RemovalCompleted)
                && _entries.TryGetValue(key, out var current)
                && ReferenceEquals(current, entry))
            {
                _entries.Remove(key);
                DisposeCancellations(entry);
            }
        }
    }

    // Caller must hold _lock.
    private void PruneCompleted()
    {
        foreach (var key in _entries.Keys.ToList())
        {
            var entry = _entries[key];
            entry.Tasks.RemoveAll(t => t.IsCompleted);
            if (entry.Tasks.Count == 0 && !entry.Draining)
            {
                _entries.Remove(key);
                DisposeCancellations(entry);
            }
        }
    }

    private static void DisposeCancellations(Entry entry)
    {
        if (entry.CancellationsDisposed)
        {
            return;
        }

        entry.CancellationsDisposed = true;
        entry.Cancellation.Dispose();
        foreach (var cancellation in entry.RetiredCancellations)
        {
            cancellation.Dispose();
        }

        entry.RetiredCancellations.Clear();
    }

    private sealed class Entry
    {
        public CancellationTokenSource Cancellation { get; set; } = new();

        public List<CancellationTokenSource> RetiredCancellations { get; } = [];

        public List<Task> Tasks { get; } = [];

        public bool Draining { get; set; }

        public bool RemovalCompleted { get; set; }

        public bool CancellationsDisposed { get; set; }
    }
}
