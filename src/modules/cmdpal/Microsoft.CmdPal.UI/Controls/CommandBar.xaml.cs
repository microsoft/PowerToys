// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.System;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class CommandBar : UserControl,
    IRecipient<OpenContextMenuMessage>,
    IRecipient<CloseContextMenuMessage>,
    IRecipient<TryCommandKeybindingMessage>,
    ICurrentPageAware
{
    private readonly ContextMenu contextMenuControl;
    private CommandBarViewModel viewModel;

    public PageViewModel? CurrentPageViewModel
    {
        get => (PageViewModel?)GetValue(CurrentPageViewModelProperty);
        set => SetValue(CurrentPageViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for CurrentPage.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty CurrentPageViewModelProperty =
        DependencyProperty.Register(nameof(CurrentPageViewModel), typeof(PageViewModel), typeof(CommandBar), new PropertyMetadata(null));

    public CommandBar(CommandBarViewModel commandBarViewModel, ContextMenu contextMenu)
    {
        this.InitializeComponent();
        this.viewModel = commandBarViewModel;
        contextMenuControl = contextMenu;

        ContextMenuFlyout.Content = contextMenuControl;

        // RegisterAll isn't AOT compatible
        WeakReferenceMessenger.Default.Register<OpenContextMenuMessage>(this);
        WeakReferenceMessenger.Default.Register<CloseContextMenuMessage>(this);
        WeakReferenceMessenger.Default.Register<TryCommandKeybindingMessage>(this);
    }

    public void Receive(OpenContextMenuMessage message)
    {
        if (message.Element is null)
        {
            // This is invoked from the "More" button on the command bar
            if (!viewModel.ShouldShowContextMenu)
            {
                return;
            }

            _ = DispatcherQueue.TryEnqueue(
                () =>
                {
                    ContextMenuFlyout.ShowAt(
                        MoreCommandsButton,
                        new FlyoutShowOptions()
                        {
                            ShowMode = FlyoutShowMode.Standard,
                            Placement = FlyoutPlacementMode.TopEdgeAlignedRight,
                        });
                });
        }
        else
        {
            // This is invoked from a specific element
            _ = DispatcherQueue.TryEnqueue(
            () =>
            {
                ContextMenuFlyout.ShowAt(
                    message.Element!,
                    new FlyoutShowOptions()
                    {
                        ShowMode = FlyoutShowMode.Standard,
                        Placement = (FlyoutPlacementMode)message.FlyoutPlacementMode!,
                        Position = message.Point,
                    });
            });
        }
    }

    public void Receive(CloseContextMenuMessage message)
    {
        if (ContextMenuFlyout.IsOpen)
        {
            ContextMenuFlyout.Hide();
        }
    }

    public void Receive(TryCommandKeybindingMessage msg)
    {
        if (!viewModel.ShouldShowContextMenu)
        {
            return;
        }

        var result = viewModel?.CheckKeybinding(msg.Ctrl, msg.Alt, msg.Shift, msg.Win, msg.Key);

        if (result == ContextKeybindingResult.Hide)
        {
            msg.Handled = true;
        }
        else if (result == ContextKeybindingResult.KeepOpen)
        {
            WeakReferenceMessenger.Default.Send<OpenContextMenuMessage>(new OpenContextMenuMessage(null, null, null, ContextMenuFilterLocation.Bottom));
            msg.Handled = true;
        }
        else if (result == ContextKeybindingResult.Unhandled)
        {
            msg.Handled = false;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "VS has a tendency to delete XAML bound methods over-aggressively")]
    private void PrimaryButton_Clicked(object sender, RoutedEventArgs e)
    {
        viewModel.InvokePrimaryCommand();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "VS has a tendency to delete XAML bound methods over-aggressively")]
    private void SecondaryButton_Clicked(object sender, RoutedEventArgs e)
    {
        viewModel.InvokeSecondaryCommand();
    }

    private void SettingsIcon_Clicked(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new OpenSettingsMessage());
    }

    private void MoreCommandsButton_Clicked(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send<OpenContextMenuMessage>(new OpenContextMenuMessage(null, null, null, ContextMenuFilterLocation.Bottom));
    }

    /// <summary>
    /// Sets focus to the "More" button after closing the context menu,
    /// keeping keyboard navigation intuitive.
    /// </summary>
    public void FocusMoreCommandsButton()
    {
        MoreCommandsButton?.Focus(FocusState.Programmatic);
    }

    private void ContextMenuFlyout_Opened(object sender, object e)
    {
        // We need to wait until our flyout is opened to try and toss focus
        // at its search box. The control isn't in the UI tree before that
        contextMenuControl.FocusSearchBox();
    }
}
