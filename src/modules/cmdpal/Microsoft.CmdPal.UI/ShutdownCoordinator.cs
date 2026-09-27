// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI;

internal sealed class ShutdownCoordinator
{
    private readonly object _lock = new();
    private readonly Func<Task> _stopExtensions;
    private readonly Func<Task> _cleanup;
    private readonly Action _exit;
    private readonly Action<Exception> _logStopFailure;
    private readonly Action<Exception> _logCleanupFailure;
    private readonly TimeSpan _stopTimeout;
    private Task? _shutdownTask;

    public ShutdownCoordinator(
        Func<Task> stopExtensions,
        Func<Task> cleanup,
        Action exit,
        Action<Exception> logStopFailure,
        Action<Exception> logCleanupFailure,
        TimeSpan stopTimeout)
    {
        _stopExtensions = stopExtensions;
        _cleanup = cleanup;
        _exit = exit;
        _logStopFailure = logStopFailure;
        _logCleanupFailure = logCleanupFailure;
        _stopTimeout = stopTimeout;
    }

    public Task RunAsync()
    {
        lock (_lock)
        {
            return _shutdownTask ??= RunCoreAsync();
        }
    }

    private async Task RunCoreAsync()
    {
        try
        {
            try
            {
                await _stopExtensions().WaitAsync(_stopTimeout);
            }
            catch (Exception ex)
            {
                _logStopFailure(ex);
            }
        }
        finally
        {
            try
            {
                await _cleanup();
            }
            catch (Exception ex)
            {
                _logCleanupFailure(ex);
            }
            finally
            {
                _exit();
            }
        }
    }
}
