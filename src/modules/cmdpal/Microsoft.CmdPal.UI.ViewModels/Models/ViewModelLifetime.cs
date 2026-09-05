// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.ExceptionServices;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

internal sealed class ViewModelLifetime
{
    private readonly Lock _gate = new();
    private Queue<Action>? _pending;
    private bool _running;
    private volatile bool _closed;

    public bool IsClosed => _closed;

    public void Run(Action action) => Enqueue(action, close: false);

    public void Close(Action cleanup) => Enqueue(cleanup, close: true);

    private void Enqueue(Action action, bool close)
    {
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            if (close)
            {
                _closed = true;
                _pending?.Clear();
            }

            if (_running)
            {
                (_pending ??= new()).Enqueue(action);
                return;
            }

            _running = true;
        }

        // Extension callbacks can reenter from another thread, so never hold the gate during RPC.
        List<Exception>? failures = null;
        while (true)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                (failures ??= []).Add(ex);
            }

            lock (_gate)
            {
                if (_pending is null || !_pending.TryDequeue(out action!))
                {
                    _running = false;
                    break;
                }
            }
        }

        if (failures is { Count: 1 })
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }
        else if (failures is not null)
        {
            throw new AggregateException(failures);
        }
    }
}
