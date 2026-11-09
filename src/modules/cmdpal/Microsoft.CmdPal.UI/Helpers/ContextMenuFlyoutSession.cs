// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Serializes a flyout's opens across WinUI's close bookkeeping and cancels stale native queued opens.
/// </summary>
/// <remarks>Call on the UI thread and forward the flyout's Opening, Closing, and Closed events.</remarks>
/// <param name="isOpen">Reads the flyout's current open state.</param>
/// <param name="hide">Hides the flyout, including cancelling a native queued open during Opening.</param>
/// <param name="release">Releases menu context after cancellation or a completed close.</param>
/// <param name="enqueue">Queues a callback for a later UI dispatcher turn; returns false when unavailable.</param>
internal sealed class ContextMenuFlyoutSession(Func<bool> isOpen, Action hide, Action release, Func<Action, bool> enqueue)
{
    private Action? _pendingOpen;
    private bool _closing;
    private bool _openRequested;

    /// <summary>
    /// Opens when the flyout is ready, otherwise closes it and retains only the latest request.
    /// </summary>
    /// <param name="open">Prepares and shows the menu, rechecking whether its context and anchor are still valid.</param>
    public void Show(Action open)
    {
        _pendingOpen = open;
        if (_closing)
        {
            return;
        }

        if (isOpen())
        {
            hide();
            return;
        }

        RunPending();
    }

    /// <summary>
    /// Handles Opening and cancels a native queued open that was withdrawn by <see cref="Hide"/>.
    /// </summary>
    public void Opening()
    {
        if (!_openRequested)
        {
            // Hide cancels Opening even though the popup is not open yet.
            hide();
            release();
        }
    }

    /// <summary>
    /// Handles Closing and blocks replacement opens until WinUI finishes closing an open popup.
    /// </summary>
    public void Closing()
    {
        // Cancelling Opening also raises Closing, but never produces Closed.
        if (isOpen())
        {
            _closing = true;
        }
    }

    /// <summary>
    /// Handles Closed, deferring context release and the latest pending open until WinUI finishes bookkeeping.
    /// </summary>
    public void Closed()
    {
        var releasing = _closing;

        // Reopen after WinUI finishes the close handler's flyout bookkeeping.
        if (!enqueue(() =>
        {
            _closing = false;
            if (releasing && !isOpen())
            {
                release();
            }

            RunPending();
        }))
        {
            _closing = false;
            _pendingOpen = null;
            if (releasing && !isOpen())
            {
                release();
            }
        }
    }

    private void RunPending()
    {
        var open = _pendingOpen;
        _pendingOpen = null;
        if (open is not null)
        {
            _openRequested = true;
            open();
        }
    }

    /// <summary>
    /// Cancels pending opens and hides the current flyout, if open.
    /// </summary>
    public void Hide()
    {
        _pendingOpen = null;
        _openRequested = false;
        if (isOpen())
        {
            hide();
        }
    }
}
