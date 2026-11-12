// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;

namespace FileExplorerScripts;

internal sealed class CatalogueLifetime : IDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Process _runner;
    private readonly EventWaitHandle _stop;
    private readonly RegisteredWaitHandle _stopWait;
    private bool _disposed;

    public CancellationToken Token => _shutdown.Token;

    public CatalogueLifetime(int runnerId)
    {
        _runner = Process.GetProcessById(runnerId);
        try
        {
            _stop = new EventWaitHandle(false, EventResetMode.ManualReset, @"Local\PowerToys.FileExplorerScripts.Stop." + runnerId);
            _stopWait = ThreadPool.RegisterWaitForSingleObject(_stop, (_, _) => RequestStop(), null, Timeout.Infinite, executeOnlyOnce: true);
            _runner.Exited += OnRunnerExited;
            _runner.EnableRaisingEvents = true;
            if (_stop.WaitOne(0) || _runner.HasExited)
            {
                RequestStop();
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _shutdown.Dispose();
        }

        _runner.Exited -= OnRunnerExited;
        _stopWait?.Unregister(null);
        _stop?.Dispose();
        _runner.Dispose();
    }

    private void OnRunnerExited(object? sender, EventArgs args) => RequestStop();

    private void RequestStop()
    {
        lock (_gate)
        {
            if (!_disposed)
            {
                _shutdown.Cancel();
            }
        }
    }
}
