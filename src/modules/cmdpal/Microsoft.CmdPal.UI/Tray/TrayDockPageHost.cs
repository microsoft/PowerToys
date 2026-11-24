// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Dock;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Microsoft.CmdPal.UI.Tray;

internal sealed partial class TrayDockPageHost(ContentControl host) : IDockPageHost
{
    public event EventHandler? Opened;

    public event EventHandler? Closed;

    public bool IsOpen { get; private set; }

    public DockPageControl? Content
    {
        get => host.Content as DockPageControl;
        set => host.Content = value;
    }

    public void Show(FrameworkElement anchor, FlyoutPlacementMode placement)
    {
        IsOpen = true;
        host.Visibility = Visibility.Visible;
        Opened?.Invoke(this, EventArgs.Empty);
    }

    public void Hide()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        host.Visibility = Visibility.Collapsed;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => Hide();
}
