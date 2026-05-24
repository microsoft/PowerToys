// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Commands;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// Hosts a <see cref="ListItemsView"/> bound to a <see cref="ListViewModel"/>.
/// All list/grid rendering, selection, and navigation behavior lives in
/// <see cref="ListItemsView"/> so it can be reused (for example, by
/// <see cref="ParametersPage"/>).
/// </summary>
public sealed partial class ListPage : Page
{
    internal ListViewModel? ViewModel
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

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not AsyncNavigationRequest navigationRequest)
        {
            throw new InvalidOperationException($"Invalid navigation parameter: {nameof(e.Parameter)} must be {nameof(AsyncNavigationRequest)}");
        }

        if (navigationRequest.TargetViewModel is not ListViewModel listViewModel)
        {
            throw new InvalidOperationException($"Invalid navigation target: AsyncNavigationRequest.{nameof(AsyncNavigationRequest.TargetViewModel)} must be {nameof(ListViewModel)}");
        }

        ViewModel = listViewModel;

        if (e.NavigationMode == NavigationMode.Back)
        {
            // Mirrors the original ListPage's back-navigation behavior — the
            // embedded view also runs this on Loaded, but kicking it from here
            // covers the case where the cached visual tree already had items
            // realized but no selection.
            ListView.EnsureInitialSelection();
        }

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

        // Clean-up event listeners
        ViewModel = null;

        GC.Collect();
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListPage @this && e.NewValue is null)
        {
            Logger.LogDebug("cleared view model");
        }
    }
}
