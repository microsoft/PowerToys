// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Microsoft.CmdPal.UI.Dock;

internal sealed partial class FlyoutDockPageHost : IDockPageHost
{
    private readonly Flyout _flyout;

    public event EventHandler? Opened;

    public event EventHandler? Closed;

    public bool IsOpen => _flyout.IsOpen;

    public DockPageControl? Content
    {
        get => _flyout.Content as DockPageControl;
        set => _flyout.Content = value;
    }

    public FlyoutDockPageHost(Flyout flyout)
    {
        _flyout = flyout;
        _flyout.Opened += OnOpened;
        _flyout.Closed += OnClosed;
    }

    public void Show(FrameworkElement anchor, FlyoutPlacementMode placement)
    {
        if (_flyout.XamlRoot != anchor.XamlRoot)
        {
            _flyout.XamlRoot = anchor.XamlRoot;
        }

        _flyout.ShowAt(anchor, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Standard, Placement = placement });
    }

    public void Hide() => _flyout.Hide();

    private void OnOpened(object? sender, object e) => Opened?.Invoke(this, EventArgs.Empty);

    private void OnClosed(object? sender, object e) => Closed?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _flyout.Opened -= OnOpened;
        _flyout.Closed -= OnClosed;
    }
}
