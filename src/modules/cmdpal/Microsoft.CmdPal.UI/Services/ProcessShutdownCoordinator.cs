// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Services;

internal sealed class ProcessShutdownCoordinator
{
    private readonly TimeSpan _timeout;
    private readonly Action<Action> _startShutdownThread;
    private readonly Action<Exception> _onError;
    private int _shutdownStarted;
    private int _exitStarted;

    internal ProcessShutdownCoordinator(TimeSpan timeout, Action<Exception> onError)
        : this(timeout, StartForegroundShutdownThread, onError)
    {
    }

    internal ProcessShutdownCoordinator(
        TimeSpan timeout,
        Action<Action> startShutdownThread,
        Action<Exception> onError)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        _timeout = timeout;
        _startShutdownThread = startShutdownThread;
        _onError = onError;
    }

    internal bool RequestExit(
        Action? closeWindow,
        Func<IEnumerable<Func<Task>>> getShutdownOperations,
        Action exitProcess)
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
        {
            return false;
        }

        try
        {
            closeWindow?.Invoke();
        }
        catch (Exception ex)
        {
            _onError(ex);
        }

        try
        {
            _startShutdownThread(() => StopAndExit(getShutdownOperations, exitProcess));
        }
        catch (Exception ex)
        {
            _onError(ex);
            ExitOnce(exitProcess);
        }

        return true;
    }

    private static void StartForegroundShutdownThread(Action action)
    {
        var shutdownThread = new Thread(() => action())
        {
            IsBackground = false,
            Name = "CmdPal extension shutdown",
        };
        shutdownThread.Start();
    }

    private void StopAndExit(
        Func<IEnumerable<Func<Task>>> getShutdownOperations,
        Action exitProcess)
    {
        try
        {
            RunShutdownOperationsAsync(getShutdownOperations)
                .WaitAsync(_timeout)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            _onError(ex);
        }
        finally
        {
            ExitOnce(exitProcess);
        }
    }

    private static async Task RunOperationAsync(Func<Task> operation)
    {
        await Task.Yield();
        await operation().ConfigureAwait(false);
    }

    private static async Task RunShutdownOperationsAsync(Func<IEnumerable<Func<Task>>> getShutdownOperations)
    {
        await Task.Yield();
        await Task.WhenAll(getShutdownOperations().Select(RunOperationAsync)).ConfigureAwait(false);
    }

    private void ExitOnce(Action exitProcess)
    {
        if (Interlocked.Exchange(ref _exitStarted, 1) == 0)
        {
            exitProcess();
        }
    }
}
