// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ContentPage : Page,
     IRecipient<ActivateSelectedListItemMessage>,
     IRecipient<ActivateSecondaryCommandMessage>
{
    private readonly DispatcherQueue _queue = DispatcherQueue.GetForCurrentThread();

    public ContentPageViewModel? ViewModel
    {
        get => (ContentPageViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for ViewModel.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(ContentPageViewModel), typeof(ContentPage), new PropertyMetadata(null));

    public ContentPage()
    {
        this.InitializeComponent();
        WeakReferenceMessenger.Default.Register<ActivateSelectedListItemMessage>(this);
        WeakReferenceMessenger.Default.Register<ActivateSecondaryCommandMessage>(this);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is ContentPageViewModel vm)
        {
            ViewModel = vm;
        }

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);
        WeakReferenceMessenger.Default.Unregister<ActivateSelectedListItemMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ActivateSecondaryCommandMessage>(this);

        // Clean-up event listeners
        ViewModel = null;
    }

    // this comes in on Enter keypresses in the SearchBox
    public void Receive(ActivateSelectedListItemMessage message)
    {
        ViewModel?.InvokePrimaryCommandCommand?.Execute(ViewModel);
    }

    // this comes in on Ctrl+Enter keypresses in the SearchBox
    public void Receive(ActivateSecondaryCommandMessage message)
    {
        ViewModel?.InvokeSecondaryCommandCommand?.Execute(ViewModel);
    }
}
