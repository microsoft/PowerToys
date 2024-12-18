// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class ActionBar : UserControl,
    IRecipient<OpenContextMenuMessage>,
    ICurrentPageAware
{
    public ActionBarViewModel ViewModel { get; set; } = new();

    public PageViewModel? CurrentPageViewModel
    {
        get => (PageViewModel?)GetValue(CurrentPageViewModelProperty);
        set => SetValue(CurrentPageViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for CurrentPage.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty CurrentPageViewModelProperty =
        DependencyProperty.Register(nameof(CurrentPageViewModel), typeof(PageViewModel), typeof(ActionBar), new PropertyMetadata(null));

    public ActionBar()
    {
        this.InitializeComponent();

        // RegisterAll isn't AOT compatible
        WeakReferenceMessenger.Default.Register<OpenContextMenuMessage>(this);
    }

    public void Receive(OpenContextMenuMessage message)
    {
        if (!ViewModel.ShouldShowContextMenu)
        {
            return;
        }

        var options = new FlyoutShowOptions
        {
            ShowMode = FlyoutShowMode.Standard,
        };
        MoreCommandsButton.Flyout.ShowAt(MoreCommandsButton, options);
        ActionsDropdown.SelectedIndex = 0;
        ActionsDropdown.Focus(FocusState.Programmatic);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "VS has a tendency to delete XAML bound methods over-agressively")]
    private void ActionListViewItem_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        if (sender is not ListViewItem listItem)
        {
            return;
        }

        if (listItem.DataContext is CommandContextItemViewModel item)
        {
            if (e.Key == VirtualKey.Enter)
            {
                ViewModel?.InvokeItemCommand.Execute(item);
                MoreCommandsButton.Flyout.Hide();
                e.Handled = true;
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "VS has a tendency to delete XAML bound methods over-agressively")]
    private void ActionListViewItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        MoreCommandsButton.Flyout.Hide();

        if (sender is not ListViewItem listItem)
        {
            return;
        }

        if (listItem.DataContext is CommandContextItemViewModel item)
        {
            ViewModel?.InvokeItemCommand.Execute(item);
            MoreCommandsButton.Flyout.Hide();
            e.Handled = true;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "VS has a tendency to delete XAML bound methods over-agressively")]
    private void PrimaryButton_Tapped(object sender, TappedRoutedEventArgs e) =>
        WeakReferenceMessenger.Default.Send<ActivateSelectedListItemMessage>();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "VS has a tendency to delete XAML bound methods over-agressively")]
    private void SecondaryButton_Tapped(object sender, TappedRoutedEventArgs e) =>
        WeakReferenceMessenger.Default.Send<ActivateSecondaryCommandMessage>();
}
