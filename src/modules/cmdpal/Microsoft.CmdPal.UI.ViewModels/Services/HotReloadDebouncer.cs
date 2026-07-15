// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Coalesces rapid file-change notifications per key (extension directory) into a
/// single delayed callback. Changes under <c>node_modules</c> are ignored. Used by
/// <see cref="JsonRpcExtensionService"/> to debounce hot-reloads while a developer saves.
/// </summary>
internal sealed partial class HotReloadDebouncer : IDisposable
{
    private readonly TimeSpan _delay;
    private readonly Action<string> _callback;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Timer> _timers = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HotReloadDebouncer"/> class.
    /// </summary>
    /// <param name="callback">Invoked with the key once a key has been quiet for the debounce delay.</param>
    /// <param name="delay">The debounce window. Defaults to 500 ms when null.</param>
    public HotReloadDebouncer(Action<string> callback, TimeSpan? delay = null)
    {
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        _delay = delay ?? TimeSpan.FromMilliseconds(500);
    }

    /// <summary>
    /// Returns a value indicating whether the given path represents a change that should
    /// trigger a hot-reload (that is, it is not inside a <c>node_modules</c> directory).
    /// </summary>
    /// <param name="changedPath">The full path of the changed file.</param>
    /// <returns>True when the change is relevant; otherwise false.</returns>
    public static bool IsRelevantChange(string changedPath)
    {
        if (string.IsNullOrEmpty(changedPath))
        {
            return false;
        }

        return changedPath.IndexOf("node_modules", StringComparison.OrdinalIgnoreCase) < 0;
    }

    /// <summary>
    /// Notifies the debouncer of a change to <paramref name="changedPath"/> for the given key.
    /// Irrelevant changes are dropped; relevant ones (re)start the debounce window.
    /// </summary>
    /// <param name="key">The key that groups the change, typically the extension directory.</param>
    /// <param name="changedPath">The full path of the changed file.</param>
    public void Notify(string key, string changedPath)
    {
        if (string.IsNullOrEmpty(key) || !IsRelevantChange(changedPath))
        {
            return;
        }

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            if (_timers.TryGetValue(key, out var existing))
            {
                existing.Change(_delay, Timeout.InfiniteTimeSpan);
                return;
            }

            _timers[key] = new Timer(OnTimerElapsed, key, _delay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Cancels any pending debounce for the given key.
    /// </summary>
    /// <param name="key">The key to cancel.</param>
    public void Cancel(string key)
    {
        lock (_lock)
        {
            if (_timers.TryGetValue(key, out var timer))
            {
                timer.Dispose();
                _timers.Remove(key);
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
            foreach (var timer in _timers.Values)
            {
                timer.Dispose();
            }

            _timers.Clear();
        }
    }

    private void OnTimerElapsed(object? state)
    {
        var key = (string)state!;

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            if (_timers.TryGetValue(key, out var timer))
            {
                timer.Dispose();
                _timers.Remove(key);
            }
        }

        _callback(key);
    }
}
