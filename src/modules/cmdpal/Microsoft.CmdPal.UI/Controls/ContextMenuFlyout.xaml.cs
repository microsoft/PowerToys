// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class ContextMenuFlyout : Flyout, IRecipient<CloseContextMenuMessage>
{
    private readonly ContextMenuFlyoutSession _session;

    internal ContextMenuViewModel ViewModel => MenuControl.ViewModel;

    public ContextMenuFlyout()
    {
        InitializeComponent();
        _session = new(() => IsOpen, Hide, ViewModel.Close, callback => DispatcherQueue.TryEnqueue(() => callback()));
        WeakReferenceMessenger.Default.Register<CloseContextMenuMessage>(this);
    }

    internal void ShowAt(
        FrameworkElement placementTarget,
        IContextMenuContext context,
        ContextMenuFilterLocation filterLocation,
        FlyoutShowOptions options,
        CommandContextItemViewModel? initialSubmenu = null,
        bool showFilterBox = true)
    {
        if (!CanOpen())
        {
            return;
        }

        _session.Show(() =>
        {
            if (!CanOpen())
            {
                return;
            }

            UIHelper.PreparePopupForShow(this, placementTarget);

            MenuControl.ShowFilterBox = showFilterBox;
            MenuControl.PrepareForOpen(context, filterLocation, initialSubmenu);

            // WinUI can clear the per-show placement override during a staged reopen.
            Placement = options.Placement;
            ShowAt(placementTarget, options);
        });

        return;

        bool CanOpen()
        {
            return placementTarget.IsLoaded && context.CanOpenContextMenu &&
                   (initialSubmenu is null ||
                    (initialSubmenu.HasSubmenu && context.AllCommands.Contains(initialSubmenu)));
        }
    }

    public void Receive(CloseContextMenuMessage message)
    {
        Close();
    }

    internal void Close()
    {
        _session.Hide();
    }

    private void Flyout_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
    {
        _session.Closing();
    }

    private void Flyout_Closed(object sender, object e)
    {
        // The session releases closed menus without clearing a newer open.
        _session.Closed();
    }

    private void Flyout_Opened(object sender, object e)
    {
        // Focus the filter box so the flyout captures keyboard input,
        // then fire a single consolidated Narrator announcement.
        MenuControl.FocusSearchBox();
        MenuControl.AnnounceOpened(() => IsOpen);
    }
}
