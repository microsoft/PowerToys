// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class PageViewModel
{
    private readonly Lock _lifetimeLock = new();
    private readonly TaskCompletionSource _operationsCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _activeOperations;
    private volatile bool _isDiscarded;
    private volatile bool _isActive = true;
    private Task? _cleanupTask;

    /// <summary>Gets a value indicating whether terminal cleanup has been requested.</summary>
    /// <remarks>Becomes true before cleanup finishes; the page can no longer be resumed.</remarks>
    public bool IsDiscarded => _isDiscarded;

    /// <summary>Gets a value indicating whether the page is active for navigation and shared UI presentation.</summary>
    /// <remarks>Does not indicate window visibility or whether extension work is running.</remarks>
    protected bool IsPageActive => _isActive && !_isDiscarded;

    /// <summary>Deactivates the page for navigation while retaining it for resumption.</summary>
    /// <remarks>Subscriptions and some background work may continue while suspended.</remarks>
    internal virtual void SuspendForNavigation() => _isActive = false;

    /// <summary>Reactivates a retained page unless terminal cleanup has been requested.</summary>
    /// <returns>A task representing resumption work.</returns>
    internal virtual Task ResumeAfterNavigation()
    {
        _isActive = !_isDiscarded;
        return Task.CompletedTask;
    }

    /// <summary>Permanently discards the page and cleans it up on a worker after in-flight page operations finish.</summary>
    /// <returns>The shared task that completes when cleanup finishes.</returns>
    /// <remarks>
    /// Detach the page's controls and bindings before calling this on the UI thread.
    /// Detachment does not discard the model; decide whether to retain it before requesting cleanup.
    /// </remarks>
    public Task CleanupAsync()
    {
        TaskCompletionSource completion;
        lock (_lifetimeLock)
        {
            if (_cleanupTask is not null)
            {
                return _cleanupTask;
            }

            _isDiscarded = true;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _cleanupTask = completion.Task;
            if (_activeOperations == 0)
            {
                _operationsCompleted.SetResult();
            }
        }

        try
        {
            OnCleanupRequested();
        }
        catch (Exception ex)
        {
            CoreLogger.LogError("Failed to stop discarded page work", ex);
        }

        _ = Task.Run(async () =>
        {
            await _operationsCompleted.Task.ConfigureAwait(false);
            SafeCleanup();
            completion.SetResult();
        });

        return completion.Task;
    }

    /// <summary>Stops local work immediately. Must not call extensions or wait for their work.</summary>
    protected virtual void OnCleanupRequested()
    {
    }

    /// <summary>Keeps cleanup behind a page operation without holding a lock across extension calls.</summary>
    /// <remarks>Operations may begin while suspended; terminal cleanup prevents new operations.</remarks>
    protected PageOperation? TryBeginPageOperation()
    {
        lock (_lifetimeLock)
        {
            if (_isDiscarded)
            {
                return null;
            }

            _activeOperations++;
            return new(this);
        }
    }

    private void EndPageOperation()
    {
        lock (_lifetimeLock)
        {
            if (--_activeOperations == 0 && _isDiscarded)
            {
                _operationsCompleted.SetResult();
            }
        }
    }

    protected void DoOnActivePage(Action action)
    {
        DoOnUiThread(() =>
        {
            using var operation = TryBeginPageOperation();
            if (operation is not null && IsPageActive)
            {
                action();
            }
        });
    }

    /// <summary>Releases one page operation at the end of its using scope.</summary>
    protected readonly struct PageOperation(PageViewModel owner) : IDisposable
    {
        public void Dispose() => owner.EndPageOperation();
    }
}
