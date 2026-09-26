// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.AdaptiveCards.IncrementalRendering;

internal sealed class LatestWinsUpdateQueue<T>
{
    private readonly object _sync = new();
    private readonly Func<T, Task> _processAsync;
    private WorkItem? _pending;
    private bool _isProcessing;

    public LatestWinsUpdateQueue(Func<T, Task> processAsync)
    {
        ArgumentNullException.ThrowIfNull(processAsync);
        _processAsync = processAsync;
    }

    public Task EnqueueAsync(T update)
    {
        var workItem = new WorkItem(update);
        WorkItem? superseded = null;
        var startProcessing = false;
        lock (_sync)
        {
            if (_isProcessing)
            {
                superseded = _pending;
                _pending = workItem;
            }
            else
            {
                _isProcessing = true;
                startProcessing = true;
            }
        }

        superseded?.Complete();
        if (startProcessing)
        {
            _ = ProcessAsync(workItem);
        }

        return workItem.Task;
    }

    public void ClearPending()
    {
        WorkItem? pending;
        lock (_sync)
        {
            pending = _pending;
            _pending = null;
        }

        pending?.Complete();
    }

    private async Task ProcessAsync(WorkItem workItem)
    {
        while (true)
        {
            try
            {
                await _processAsync(workItem.Update);
                workItem.Complete();
            }
            catch (OperationCanceledException ex)
            {
                workItem.Cancel(ex.CancellationToken);
            }
            catch (Exception ex)
            {
                workItem.Fail(ex);
            }

            lock (_sync)
            {
                if (_pending is not null)
                {
                    workItem = _pending;
                    _pending = null;
                    continue;
                }

                _isProcessing = false;
                return;
            }
        }
    }

    private sealed class WorkItem(T update)
    {
        private readonly TaskCompletionSource _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public T Update { get; } = update;

        public Task Task => _completion.Task;

        public void Complete() => _completion.TrySetResult();

        public void Cancel(CancellationToken cancellationToken) =>
            _completion.TrySetCanceled(cancellationToken);

        public void Fail(Exception exception) => _completion.TrySetException(exception);
    }
}
