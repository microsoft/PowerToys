// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Commands;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// Hosts a parameter run, optionally embedding a <see cref="ListItemsView"/> when
/// a list parameter is active. List rendering, selection, and keyboard navigation
/// are handled by the embedded <see cref="ListItemsView"/>.
/// </summary>
public sealed partial class ParametersPage : Page
{
    public ParametersPageViewModel? ViewModel
    {
        get => (ParametersPageViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for ViewModel.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(ParametersPageViewModel), typeof(ParametersPage), new PropertyMetadata(null, OnViewModelChanged));

    public ParametersPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not AsyncNavigationRequest navigationRequest)
        {
            throw new InvalidOperationException($"Invalid navigation parameter: {nameof(e.Parameter)} must be {nameof(AsyncNavigationRequest)}");
        }

        if (navigationRequest.TargetViewModel is not ParametersPageViewModel page)
        {
            throw new InvalidOperationException($"Invalid navigation target: AsyncNavigationRequest.{nameof(AsyncNavigationRequest.TargetViewModel)} must be {nameof(ParametersPageViewModel)}");
        }

        ViewModel = page;

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);

        // Clean-up event listeners
        ViewModel = null;
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ParametersPage && e.NewValue is null)
        {
            CoreLogger.LogDebug("cleared view model");
        }
    }
}
