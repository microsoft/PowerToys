// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace Microsoft.CmdPal.UI;

public sealed partial class ListPage : Page,
    IRecipient<ActivateSecondaryCommandMessage>
{
    private ListViewModel? ViewModel
    {
        get => (ListViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for ViewModel.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(ListViewModel), typeof(ListPage), new PropertyMetadata(null, OnViewModelChanged));

    public ListPage()
    {
        this.InitializeComponent();
        this.NavigationCacheMode = NavigationCacheMode.Disabled;
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is ListViewModel lvm)
        {
            ViewModel = lvm;
        }

        if (e.NavigationMode == NavigationMode.Back
            || (e.NavigationMode == NavigationMode.New))
        {
            // Upon navigating _back_ to this page, immediately select the
            // first item in the list
            ListViewControl.ResetSelection();
        }

        // RegisterAll isn't AOT compatible
        WeakReferenceMessenger.Default.Register<ActivateSecondaryCommandMessage>(this);

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);

        WeakReferenceMessenger.Default.Unregister<ActivateSecondaryCommandMessage>(this);

        if (e.NavigationMode != NavigationMode.New)
        {
            ViewModel?.SafeCleanup();
            CleanupHelper.Cleanup(this);
        }

        // Clean-up event listeners
        ViewModel = null;

        GC.Collect();
    }

    public void Receive(ActivateSecondaryCommandMessage message)
    {
        if (ViewModel?.ShowEmptyContent ?? false)
        {
            ViewModel?.InvokeSecondaryCommandCommand.Execute(null);
        }
    }

    private void Items_OnContextCanceled(UIElement sender, RoutedEventArgs e)
    {
        _ = DispatcherQueue.TryEnqueue(() => WeakReferenceMessenger.Default.Send<CloseContextMenuMessage>());
    }
}
