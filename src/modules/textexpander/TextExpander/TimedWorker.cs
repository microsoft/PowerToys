// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Runs work that waits on another app, such as a UI Automation query, on a thread of its own, and waits only so long
/// for it. Work that takes longer is left to finish there, and until it does, no more is started: an app that doesn't
/// answer never holds up the caller, nor piles up threads.
/// </summary>
internal sealed class TimedWorker
{
    private readonly object _gate = new();
    private readonly string _name;
    private readonly ApartmentState _apartment;
    private readonly Action? _started;
    private Thread? _thread;
    private Action? _next;
    private int _busy;

    /// <param name="name">The thread's name.</param>
    /// <param name="apartment">The thread's COM apartment.</param>
    /// <param name="started">Runs first on the thread, such as to join COM's apartment.</param>
    public TimedWorker(string name, ApartmentState apartment, Action? started = null)
    {
        _name = name;
        _apartment = apartment;
        _started = started;
    }

    /// <summary>
    /// Runs <paramref name="work"/> on the worker's thread and waits up to <paramref name="timeoutMs"/> for it. Returns
    /// false, leaving <paramref name="result"/> unset, when it took longer or threw, or when work from an earlier call is
    /// still running, in which case <paramref name="work"/> doesn't run at all.
    /// </summary>
    public bool TryRun<T>(Func<T> work, int timeoutMs, out T? result)
    {
        result = default;
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            return false;
        }

        var done = new TaskCompletionSource<(bool Ran, T? Value)>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            lock (_gate)
            {
                EnsureStarted();
                _next = () => Run(work, done);
                Monitor.Pulse(_gate);
            }
        }
        catch (Exception ex) when (ex is ThreadStartException or OutOfMemoryException)
        {
            Volatile.Write(ref _busy, 0);
            return false;
        }

        if (!done.Task.Wait(timeoutMs) || !done.Task.Result.Ran)
        {
            return false;
        }

        result = done.Task.Result.Value;
        return true;
    }

    private void Run<T>(Func<T> work, TaskCompletionSource<(bool Ran, T? Value)> done)
    {
        (bool Ran, T? Value) outcome = (false, default);
        try
        {
            outcome = (true, work());
        }
        catch (Exception)
        {
            // The caller goes on without an answer.
        }
        finally
        {
            // Free first, so a caller that sees this answer can start more work at once.
            Volatile.Write(ref _busy, 0);
            done.TrySetResult(outcome);
        }
    }

    // Under _gate.
    private void EnsureStarted()
    {
        if (_thread is not null)
        {
            return;
        }

        var thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = _name,
        };
        thread.SetApartmentState(_apartment);
        thread.Start();
        _thread = thread;
    }

    private void Loop()
    {
        _started?.Invoke();
        while (true)
        {
            Action work;
            lock (_gate)
            {
                while (_next is null)
                {
                    Monitor.Wait(_gate);
                }

                work = _next;
                _next = null;
            }

            work();
        }
    }
}
