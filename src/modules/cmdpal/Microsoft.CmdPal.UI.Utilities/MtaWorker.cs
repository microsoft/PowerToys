// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using Windows.Win32;
using Windows.Win32.System.Com;

namespace Microsoft.CmdPal.UI.Utilities;

/// <summary>
/// Serializes COM work and cleanup on a windowless MTA thread.
/// Disposal stops accepting work without blocking the calling thread.
/// </summary>
internal sealed partial class MtaWorker : IDisposable, IAsyncDisposable
{
    private readonly BlockingCollection<Action> _work = new();
    private readonly object _gate = new();
    private readonly Action _cleanup;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Exception? _initializationError;
    private bool _disposed;

    public MtaWorker(Action cleanup)
    {
        _cleanup = cleanup;
        var thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "Taskbar UI Automation",
        };
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
    }

    public Task<T> InvokeAsync<T>(Func<T> action)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            _work.Add(() =>
            {
                try
                {
                    if (_initializationError is not null)
                    {
                        completion.SetException(_initializationError);
                        return;
                    }

                    completion.SetResult(action());
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            });
            return completion.Task;
        }
    }

    private unsafe void Run()
    {
        var result = PInvoke.CoInitializeEx(null, COINIT.COINIT_MULTITHREADED);
        Exception? error = null;
        try
        {
            try
            {
                result.ThrowOnFailure();
            }
            catch (Exception ex)
            {
                _initializationError = ex;
            }

            foreach (var action in _work.GetConsumingEnumerable())
            {
                action();
            }

            _cleanup();
        }
        catch (Exception ex)
        {
            error = ex;
        }
        finally
        {
            if (result.Succeeded)
            {
                PInvoke.CoUninitialize();
            }

            _work.Dispose();
        }

        if (error is null)
        {
            _completion.SetResult();
        }
        else
        {
            _completion.SetException(error);
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
            _work.CompleteAdding();
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return new ValueTask(_completion.Task);
    }
}
