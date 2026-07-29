// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// A cancellation source that can be reused across successive service load cycles.
/// A single <see cref="CancellationTokenSource"/> can only transition to canceled
/// once, so a service that shares one token between "stop" and "load again" would
/// keep handing out an already-canceled token after the first stop. This wrapper
/// hands out the live token, lets callers request a stop, and swaps in a fresh
/// source when a new load cycle begins, disposing the previous one safely.
/// </summary>
internal sealed partial class ReloadCancellation : IDisposable
{
    private readonly Lock _lock = new();
    private CancellationTokenSource _cts = new();
    private bool _disposed;

    /// <summary>
    /// Gets the token for the current load cycle. Once the wrapper has been stopped
    /// or disposed the returned token is already canceled, so callers observe the
    /// stop request without touching a disposed source.
    /// </summary>
    public CancellationToken Token
    {
        get
        {
            lock (_lock)
            {
                if (_disposed || _cts.IsCancellationRequested)
                {
                    return new CancellationToken(canceled: true);
                }

                return _cts.Token;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a stop (or dispose) has been requested for the
    /// current cycle. New work should bail out when this is true.
    /// </summary>
    public bool IsStopRequested
    {
        get
        {
            lock (_lock)
            {
                return _disposed || _cts.IsCancellationRequested;
            }
        }
    }

    /// <summary>
    /// Ensures a fresh, uncanceled token is available for a new load cycle. When the
    /// current source has already been canceled it is disposed and replaced. Returns
    /// the token for the new cycle. After the wrapper has been disposed this returns
    /// an already-canceled token instead of throwing.
    /// </summary>
    /// <returns>The token that governs the newly started cycle.</returns>
    public CancellationToken BeginCycle()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return new CancellationToken(canceled: true);
            }

            if (_cts.IsCancellationRequested)
            {
                _cts.Dispose();
                _cts = new CancellationTokenSource();
            }

            return _cts.Token;
        }
    }

    /// <summary>
    /// Requests cancellation of the current cycle without disposing the source, so
    /// in-flight callers that already captured the token observe the cancellation.
    /// The source is replaced on the next <see cref="BeginCycle"/>.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
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

            try
            {
                _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            _cts.Dispose();
        }
    }
}
