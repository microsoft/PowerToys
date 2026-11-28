// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.QuickAccess.Services;

internal enum QuickAccessFlyoutAction
{
    None,
    Show,
    Hide,
}

internal sealed class QuickAccessFlyoutState
{
    private enum VisibilityState
    {
        Closed,
        Opening,
        Open,
        Closing,
    }

    private VisibilityState _state;
    private bool _hideWhenOpened;
    private bool _showWhenClosed;

    public QuickAccessFlyoutAction RequestShow()
    {
        _hideWhenOpened = false;
        if (_state == VisibilityState.Closed)
        {
            _state = VisibilityState.Opening;
            return QuickAccessFlyoutAction.Show;
        }

        if (_state == VisibilityState.Closing)
        {
            _showWhenClosed = true;
        }

        return QuickAccessFlyoutAction.None;
    }

    public QuickAccessFlyoutAction RequestHide()
    {
        _showWhenClosed = false;
        if (_state == VisibilityState.Opening)
        {
            _hideWhenOpened = true;
        }
        else if (_state == VisibilityState.Open)
        {
            _state = VisibilityState.Closing;
            return QuickAccessFlyoutAction.Hide;
        }

        return QuickAccessFlyoutAction.None;
    }

    public QuickAccessFlyoutAction OnOpened()
    {
        _state = VisibilityState.Open;
        return _hideWhenOpened ? RequestHide() : QuickAccessFlyoutAction.None;
    }

    public QuickAccessFlyoutAction OnClosed()
    {
        _state = VisibilityState.Closed;
        var showWhenClosed = _showWhenClosed;
        _showWhenClosed = false;
        return showWhenClosed ? RequestShow() : QuickAccessFlyoutAction.None;
    }
}
