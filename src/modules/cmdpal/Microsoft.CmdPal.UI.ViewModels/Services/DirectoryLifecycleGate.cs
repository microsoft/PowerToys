// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Serializes all lifecycle operations (initial load, refresh, crash-restart,
/// hot-reload, and removal) for a single extension directory so that concurrent
/// triggers cannot launch duplicate processes for the same extension. The gate is
/// keyed by the canonical directory path (case-insensitive), matching the rest of
/// the service's directory comparisons.
/// </summary>
/// <remarks>
/// Entries are reference counted. An entry stays alive while any caller holds or is
/// waiting on it, so <see cref="Remove"/> during a concurrent acquire never disposes
/// a semaphore out from under a waiter (which would surface as an
/// <see cref="ObjectDisposedException"/>). The backing semaphore is disposed only
/// once the last reference is released after a removal, or when the gate itself is
/// disposed.
/// </remarks>
internal sealed partial class DirectoryLifecycleGate : IDisposable
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>
    /// Produces the canonical key for a directory: an absolute path with any trailing
    /// separator trimmed. Invalid paths fall back to the trimmed original so callers
    /// still get a stable key rather than an exception.
    /// </summary>
    /// <param name="directory">The directory to canonicalize.</param>
    /// <returns>The canonical key used to group lifecycle operations.</returns>
    public static string Canonicalize(string directory)
    {
        if (string.IsNullOrEmpty(directory))
        {
            return string.Empty;
        }

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Path.TrimEndingDirectorySeparator(directory);
        }
    }

    /// <summary>
    /// Acquires exclusive access to the lifecycle of a directory. Dispose the returned
    /// handle to release it. Operations for different directories run concurrently;
    /// operations for the same directory are serialized.
    /// </summary>
    /// <param name="directory">The extension directory whose lifecycle is being changed.</param>
    /// <param name="cancellationToken">A token that cancels the wait.</param>
    /// <returns>A handle that releases the gate when disposed.</returns>
    public async Task<IDisposable> AcquireAsync(string directory, CancellationToken cancellationToken)
    {
        var key = Canonicalize(directory);
        Entry entry;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_entries.TryGetValue(key, out var existing))
            {
                existing = new Entry();
                _entries[key] = existing;
            }

            existing.Refs++;
            entry = existing;
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // The wait was canceled (or failed); drop the reference we took so the
            // entry can be cleaned up and does not leak.
            ReleaseReference(key, entry, releaseSemaphore: false);
            throw;
        }

        return new Releaser(this, key, entry);
    }

    /// <summary>
    /// Marks a directory's gate entry for removal. When no one holds or awaits it the
    /// entry is evicted and its semaphore disposed immediately. When a holder or waiter
    /// still exists the entry is left in place (marked removed) so that any operation
    /// already queued behind it, and any operation that arrives before it drains, keep
    /// serializing on the same semaphore. The entry is evicted only once its last
    /// reference is released (see <see cref="ReleaseReference"/>). This guarantees a new
    /// generation for the directory strictly supersedes the prior one and can never run
    /// concurrently with it.
    /// </summary>
    /// <param name="directory">The directory whose gate entry should be released.</param>
    public void Remove(string directory)
    {
        var key = Canonicalize(directory);
        lock (_lock)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                entry.Removed = true;
                if (entry.Refs == 0)
                {
                    _entries.Remove(key);
                    entry.Semaphore.Dispose();
                }

                // Otherwise keep the entry in the map so later acquires reuse it and stay
                // serialized behind the drain; ReleaseReference evicts it at Refs == 0.
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var entry in _entries.Values)
            {
                entry.Removed = true;
                if (entry.Refs == 0)
                {
                    entry.Semaphore.Dispose();
                }
            }

            _entries.Clear();
        }
    }

    private void ReleaseReference(string key, Entry entry, bool releaseSemaphore)
    {
        if (releaseSemaphore)
        {
            try
            {
                entry.Semaphore.Release();
            }
            catch (ObjectDisposedException)
            {
                // The gate was disposed while this operation held it; nothing to release.
            }
        }

        lock (_lock)
        {
            entry.Refs--;
            if (entry.Refs == 0 && entry.Removed)
            {
                // The last reference to a removed entry is gone; evict it so the next
                // acquire for this directory starts a fresh generation, and dispose the
                // semaphore. Guard against evicting a different entry that may have taken
                // this key (belt and suspenders; a removed entry is never replaced while
                // it is still present).
                if (_entries.TryGetValue(key, out var current) && ReferenceEquals(current, entry))
                {
                    _entries.Remove(key);
                }

                entry.Semaphore.Dispose();
            }
        }
    }

    private sealed class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int Refs { get; set; }

        public bool Removed { get; set; }
    }

    private sealed partial class Releaser : IDisposable
    {
        private readonly DirectoryLifecycleGate _gate;
        private readonly string _key;
        private readonly Entry _entry;
        private bool _released;

        public Releaser(DirectoryLifecycleGate gate, string key, Entry entry)
        {
            _gate = gate;
            _key = key;
            _entry = entry;
        }

        public void Dispose()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            _gate.ReleaseReference(_key, _entry, releaseSemaphore: true);
        }
    }
}
