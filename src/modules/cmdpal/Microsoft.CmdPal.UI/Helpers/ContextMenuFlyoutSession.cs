// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed class ContextMenuFlyoutSession(Func<bool> isOpen, Action hide, Action release, Func<Action, bool> enqueue)
{
    private Action? _pendingOpen;
    private bool _closing;

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

    public void Closing()
    {
        _closing = true;
    }

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
        open?.Invoke();
    }

    public void Hide()
    {
        _pendingOpen = null;
        if (isOpen())
        {
            hide();
        }
    }
}
