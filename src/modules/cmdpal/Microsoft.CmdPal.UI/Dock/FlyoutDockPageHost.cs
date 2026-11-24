// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Microsoft.CmdPal.UI.Dock;

internal sealed partial class FlyoutDockPageHost : IDockPageHost
{
    private readonly Flyout _flyout;
    private readonly Func<nint> _ownerHwnd;
    private readonly WindowDismissalService _windowDismissal;
    private bool _isHiding;
    private bool _dismissalPending;

    public event EventHandler? Opened;

    public event EventHandler? Closed;

    public bool IsOpen => _flyout.IsOpen;

    public DockPageControl? Content
    {
        get => _flyout.Content as DockPageControl;
        set => _flyout.Content = value;
    }

    public FlyoutDockPageHost(Flyout flyout, Func<nint> ownerHwnd, WindowDismissalService windowDismissal)
    {
        _flyout = flyout;
        _ownerHwnd = ownerHwnd;
        _windowDismissal = windowDismissal;
        _windowDismissal.LastHoldReleased += WindowDismissal_LastHoldReleased;
        _flyout.Opened += OnOpened;
        _flyout.Closing += OnClosing;
        _flyout.Closed += OnClosed;
    }

    public void Show(FrameworkElement anchor, FlyoutPlacementMode placement)
    {
        _dismissalPending = false;
        UIHelper.PreparePopupForShow(_flyout, anchor);
        _flyout.ShowAt(anchor, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Standard, Placement = placement });
    }

    public void Hide()
    {
        _dismissalPending = false;
        _isHiding = true;
        try
        {
            _flyout.Hide();
        }
        finally
        {
            _isHiding = false;
        }
    }

    private void OnOpened(object? sender, object e) => Opened?.Invoke(this, EventArgs.Empty);

    private void OnClosing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
    {
        if (!_isHiding && _windowDismissal.IsSuppressed(_ownerHwnd()))
        {
            _dismissalPending = true;
            args.Cancel = true;
        }
    }

    private void WindowDismissal_LastHoldReleased(object? sender, nint ownerHwnd)
    {
        if (ownerHwnd != _ownerHwnd())
        {
            return;
        }

        _flyout.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (!_dismissalPending || !IsOpen || _windowDismissal.IsSuppressed(ownerHwnd))
            {
                return;
            }

            _dismissalPending = false;
            if (!WindowHelper.HasForegroundWindow(ownerHwnd))
            {
                _flyout.Hide();
            }
        });
    }

    private void OnClosed(object? sender, object e)
    {
        _dismissalPending = false;
        _windowDismissal.Reset(_ownerHwnd());
        Closed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _dismissalPending = false;
        _windowDismissal.LastHoldReleased -= WindowDismissal_LastHoldReleased;
        _windowDismissal.Reset(_ownerHwnd());
        _flyout.Opened -= OnOpened;
        _flyout.Closing -= OnClosing;
        _flyout.Closed -= OnClosed;
    }
}
