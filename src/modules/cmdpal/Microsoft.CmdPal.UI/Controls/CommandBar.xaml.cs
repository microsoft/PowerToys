// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class CommandBar : UserControl,
    IRecipient<OpenContextMenuMessage>,
    IRecipient<CloseContextMenuMessage>,
    IRecipient<TryCommandKeybindingMessage>,
    ICurrentPageAware
{
    public CommandBarViewModel ViewModel { get; } = new();

    public PageViewModel? CurrentPageViewModel
    {
        get => (PageViewModel?)GetValue(CurrentPageViewModelProperty);
        set => SetValue(CurrentPageViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for CurrentPage.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty CurrentPageViewModelProperty =
        DependencyProperty.Register(nameof(CurrentPageViewModel), typeof(PageViewModel), typeof(CommandBar), new PropertyMetadata(null));

    public CommandBar()
    {
        this.InitializeComponent();

        // RegisterAll isn't AOT compatible
        WeakReferenceMessenger.Default.Register<OpenContextMenuMessage>(this);
        WeakReferenceMessenger.Default.Register<CloseContextMenuMessage>(this);
        WeakReferenceMessenger.Default.Register<TryCommandKeybindingMessage>(this);
    }

    public void Receive(OpenContextMenuMessage message)
    {
        if (!ViewModel.ShouldShowContextMenu)
        {
            return;
        }

        if (message.Element == null)
        {
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
        if (!ViewModel.ShouldShowContextMenu)
        {
            return;
        }

        var result = ViewModel?.CheckKeybinding(msg.Ctrl, msg.Alt, msg.Shift, msg.Win, msg.Key);

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
        ViewModel.InvokePrimaryCommand();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "VS has a tendency to delete XAML bound methods over-aggressively")]
    private void SecondaryButton_Clicked(object sender, RoutedEventArgs e)
    {
        ViewModel.InvokeSecondaryCommand();
    }

    private void SettingsIcon_Clicked(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send<OpenSettingsMessage>();
    }

    private void MoreCommandsButton_Clicked(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send<OpenContextMenuMessage>(new OpenContextMenuMessage(null, null, null, ContextMenuFilterLocation.Bottom));
    }

    private void ContextMenuFlyout_Opened(object sender, object e)
    {
        // We need to wait until our flyout is opened to try and toss focus
        // at its search box. The control isn't in the UI tree before that
        ContextControl.FocusSearchBox();
    }
}
