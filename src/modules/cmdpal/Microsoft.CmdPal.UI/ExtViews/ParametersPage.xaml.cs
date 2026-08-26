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
public sealed partial class ParametersPage : Page, IPageInteractionTarget, IListInteractionSource
{
    public ParametersPageViewModel? ViewModel
    {
        get => (ParametersPageViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for ViewModel.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(ParametersPageViewModel), typeof(ParametersPage), new PropertyMetadata(null, OnViewModelChanged));

    public event EventHandler<ListItemsSelectionChangedEventArgs>? SelectionChanged;

    public event EventHandler<ListItemsContextMenuRequestedEventArgs>? ContextMenuRequested;

    public event EventHandler? ContextMenuCloseRequested;

    public event EventHandler? FocusSearchRequested;

    public event EventHandler<PageDragStateChangedEventArgs>? DragStateChanged;

    public ParametersPage()
    {
        this.InitializeComponent();
        ActiveList.SelectionChanged += ActiveList_SelectionChanged;
        ActiveList.ContextMenuRequested += ActiveList_ContextMenuRequested;
        ActiveList.ContextMenuCloseRequested += ActiveList_ContextMenuCloseRequested;
        ActiveList.FocusSearchRequested += ActiveList_FocusSearchRequested;
        ActiveList.DragStateChanged += ActiveList_DragStateChanged;
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

    public void NavigatePrevious() => ActiveList.NavigatePrevious();

    public void NavigateNext() => ActiveList.NavigateNext();

    public void NavigateLeft() => ActiveList.NavigateLeft();

    public void NavigateRight() => ActiveList.NavigateRight();

    public void NavigatePageUp() => ActiveList.NavigatePageUp();

    public void NavigatePageDown() => ActiveList.NavigatePageDown();

    public void ActivatePrimary()
    {
        if (ViewModel?.HasActiveList == true)
        {
            ActiveList.ActivatePrimary();
        }
        else
        {
            ViewModel?.TrySubmit();
        }
    }

    public void ActivateSecondary()
    {
        if (ViewModel?.HasActiveList == true)
        {
            ActiveList.ActivateSecondary();
        }
    }

    private void ActiveList_SelectionChanged(object? sender, ListItemsSelectionChangedEventArgs e) =>
        SelectionChanged?.Invoke(this, e);

    private void ActiveList_ContextMenuRequested(object? sender, ListItemsContextMenuRequestedEventArgs e) =>
        ContextMenuRequested?.Invoke(this, e);

    private void ActiveList_ContextMenuCloseRequested(object? sender, EventArgs e) =>
        ContextMenuCloseRequested?.Invoke(this, EventArgs.Empty);

    private void ActiveList_FocusSearchRequested(object? sender, EventArgs e) =>
        FocusSearchRequested?.Invoke(this, EventArgs.Empty);

    private void ActiveList_DragStateChanged(object? sender, PageDragStateChangedEventArgs e) =>
        DragStateChanged?.Invoke(this, e);
}
