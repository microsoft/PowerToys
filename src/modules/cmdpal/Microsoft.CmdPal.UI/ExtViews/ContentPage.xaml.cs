// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ContentPage : Page, IPageInteractionTarget
{
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
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not AsyncNavigationRequest navigationRequest)
        {
            throw new InvalidOperationException($"Invalid navigation parameter: {nameof(e.Parameter)} must be {nameof(AsyncNavigationRequest)}");
        }

        if (navigationRequest.TargetViewModel is not ContentPageViewModel contentPageViewModel)
        {
            throw new InvalidOperationException($"Invalid navigation target: AsyncNavigationRequest.{nameof(AsyncNavigationRequest.TargetViewModel)} must be {nameof(ContentPageViewModel)}");
        }

        ViewModel = contentPageViewModel;

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);

        // Clean-up event listeners
        if (e.NavigationMode != NavigationMode.New)
        {
            ViewModel?.SafeCleanup();
            CleanupHelper.Cleanup(this);
        }

        ViewModel = null;
    }

    public void NavigatePrevious()
    {
    }

    public void NavigateNext()
    {
    }

    public void NavigateLeft()
    {
    }

    public void NavigateRight()
    {
    }

    public void NavigatePageUp()
    {
    }

    public void NavigatePageDown()
    {
    }

    public void ActivatePrimary() => ViewModel?.InvokePrimaryCommandCommand?.Execute(ViewModel);

    public void ActivateSecondary() => ViewModel?.InvokeSecondaryCommandCommand?.Execute(ViewModel);
}
