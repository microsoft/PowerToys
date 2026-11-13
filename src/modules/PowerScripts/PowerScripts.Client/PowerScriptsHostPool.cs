// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace PowerScripts.Client;

internal sealed class PowerScriptsHostPool(Func<ProcessStartInfo> createStartInfo) : IDisposable
{
    private const int MaximumIdleHosts = 2;
    private readonly object _gate = new();
    private readonly HashSet<PowerScriptsHostSession> _sessions = [];
    private readonly Stack<PowerScriptsHostSession> _idle = [];
    private bool _disposed;

    internal async Task<(int ExitCode, string StdOut, string StdErr)> InvokeAsync(
        IEnumerable<string> arguments,
        string? input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PowerScriptsHostSession session;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_idle.TryPop(out var available))
            {
                session = available;
            }
            else
            {
                session = new PowerScriptsHostSession(createStartInfo);
                _sessions.Add(session);
            }
        }

        try
        {
            return await session.InvokeAsync(arguments, input, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            var release = false;
            lock (_gate)
            {
                if (!_disposed && _idle.Count < MaximumIdleHosts)
                {
                    _idle.Push(session);
                }
                else
                {
                    _sessions.Remove(session);
                    release = true;
                }
            }

            if (release)
            {
                session.Dispose();
            }
        }
    }

    public void Dispose()
    {
        PowerScriptsHostSession[] sessions;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            sessions = [.. _sessions];
            _sessions.Clear();
            _idle.Clear();
        }

        foreach (var session in sessions)
        {
            session.Dispose();
        }
    }
}
