// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.CmdPal.UI;

public sealed partial class ControlsPage : Page
{
    public ControlsPageViewModel? ViewModel
    {
        get => (ControlsPageViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(ControlsPageViewModel), typeof(ControlsPage), new PropertyMetadata(null));

    public ControlsPage()
    {
        this.InitializeComponent();
        this.Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not AsyncNavigationRequest navigationRequest)
        {
            throw new InvalidOperationException($"Invalid navigation parameter: {nameof(e.Parameter)} must be {nameof(AsyncNavigationRequest)}");
        }

        if (navigationRequest.TargetViewModel is not ControlsPageViewModel controlsPageViewModel)
        {
            throw new InvalidOperationException($"Invalid navigation target: AsyncNavigationRequest.{nameof(AsyncNavigationRequest.TargetViewModel)} must be {nameof(ControlsPageViewModel)}");
        }

        ViewModel = controlsPageViewModel;

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);

        if (e.NavigationMode != NavigationMode.New)
        {
            ViewModel?.SafeCleanup();
            CleanupHelper.Cleanup(this);
        }

        ViewModel = null;
    }
}
