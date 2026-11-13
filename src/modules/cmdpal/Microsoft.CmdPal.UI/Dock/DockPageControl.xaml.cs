// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.WinUI;
using ManagedCommon;
using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Microsoft.CmdPal.UI.Dock;

public sealed partial class DockPageControl : UserControl, IDisposable, ISearchBarHost
{
    private readonly PageInteractionCoordinator _pageInteractions;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _detailsTimer;
    private IListInteractionSource? _listInteractionSource;
    private PageViewModel? _subscribedPage;
    private bool _isLoaded;
    private bool _isDisposed;

    /// <summary>
    /// Gets the page stack shown by this control. The creator owns and disposes it.
    /// </summary>
    internal DockPageNavigationViewModel Navigation { get; }

    internal PageViewModel? CurrentPage => Navigation.CurrentPage;

    internal event EventHandler? CloseRequested;

    internal DockPageControl(DockPageNavigationViewModel navigation)
    {
        Navigation = navigation;
        InitializeComponent();

        _detailsTimer = DispatcherQueue.CreateTimer();
        _pageInteractions = new(PageCommandBar);
        _pageInteractions.FocusSearchRequested += PageInteractions_FocusSearchRequested;
        _pageInteractions.DetailsChanged += PageInteractions_DetailsChanged;
        Navigation.PropertyChanged += Navigation_PropertyChanged;
        SearchBox.BackRequested += SearchBox_BackRequested;
        SearchBox.NavigationRequested += SearchBox_NavigationRequested;
        PageCommandBar.FocusSearchRequested += PageCommandBar_FocusSearchRequested;
        FiltersDropDown.FocusSearchRequested += FiltersDropDown_FocusSearchRequested;
        AddHandler(PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown), false);
        AddHandler(KeyDownEvent, new KeyEventHandler(OnKeyDown), false);
        Loaded += OnLoaded;
    }

    internal bool HasOpenTransientUi => PageCommandBar.HasOpenTransientUi;

    bool ISearchBarHost.CanFocusSearchBar => !_isDisposed && IsLoaded && XamlRoot is not null;

    internal void FocusSearch()
    {
        if (CurrentPage?.HasSearchBox == true)
        {
            SearchBox.FocusActiveControl();
        }
        else if (FocusManager.FindFirstFocusableElement(PageContent) is DependencyObject focusable)
        {
            _ = FocusManager.TryFocusAsync(focusable, FocusState.Keyboard);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        UpdatePageLevelState(useTransitions: false);
        if (!ReferenceEquals(_subscribedPage, CurrentPage))
        {
            UpdateCurrentPage();
        }
    }

    private void Navigation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DockPageNavigationViewModel.CurrentPage))
        {
            UpdateCurrentPage();
        }

        if (e.PropertyName == nameof(DockPageNavigationViewModel.HasBackButton) && _isLoaded)
        {
            UpdatePageLevelState(useTransitions: true);
        }
    }

    private void UpdatePageLevelState(bool useTransitions) =>
        VisualStateManager.GoToState(
            this,
            Navigation.HasBackButton ? "NestedPage" : "RootPage",
            useTransitions);

    private void UpdateCurrentPage()
    {
        if (_isDisposed)
        {
            return;
        }

        DetachCurrentContent();
        _subscribedPage = CurrentPage;
        _pageInteractions.AttachPage(_subscribedPage);
        if (_subscribedPage is null)
        {
            return;
        }

        IPageInteractionTarget? target = _subscribedPage switch
        {
            ListViewModel list => new ListItemsView { ViewModel = list },
            ContentPageViewModel content => new ContentPage { ViewModel = content },
            ParametersPageViewModel parameters => new ParametersPage { ViewModel = parameters },
            _ => null,
        };

        if (target is null)
        {
            Logger.LogError($"Dock pages can't show a {_subscribedPage.GetType().Name}.");
            return;
        }

        AttachInteractionTarget(target);
        PageContent.Content = target;

        var page = _subscribedPage;
        DispatcherQueue.TryEnqueue(
            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            () =>
            {
                if (!ReferenceEquals(page, CurrentPage) || _isDisposed)
                {
                    return;
                }

                FocusSearch();
                AnnounceCurrentPage();
            });
    }

    private void AttachInteractionTarget(IPageInteractionTarget? target)
    {
        if (_listInteractionSource is not null)
        {
            _listInteractionSource.ContextMenuRequested -= Page_ContextMenuRequested;
        }

        _listInteractionSource = target as IListInteractionSource;
        if (_listInteractionSource is not null)
        {
            _listInteractionSource.ContextMenuRequested += Page_ContextMenuRequested;
        }

        _pageInteractions.AttachTarget(target);
    }

    private void AnnounceCurrentPage()
    {
        var title = string.IsNullOrEmpty(CurrentPage?.Title)
            ? ResourceLoaderInstance.GetString("UntitledPageTitle")
            : CurrentPage.Title;
        var format = ResourceLoaderInstance.GetString("ScreenReader_Announcement_NavigatedToPage0");
        var announcement = string.Format(CultureInfo.CurrentCulture, format, title);
        UIHelper.AnnounceActionForAccessibility(PageContent, announcement, "DockPageNavigatedTo");
    }

    private void Page_ContextMenuRequested(object? sender, ListItemsContextMenuRequestedEventArgs e) =>
        PageCommandBar.ShowContextMenu(e.Context, e.Element, e.Placement, e.Position, e.FilterLocation);

    private void DetachCurrentContent()
    {
        PageCommandBar.CloseContextMenu();
        FiltersDropDown.CloseDropDown();
        AttachInteractionTarget(null);
        _detailsTimer.Stop();
        HideDetails();

        switch (PageContent.Content)
        {
            case ListItemsView list:
                list.ViewModel = null;
                break;
            case ContentPage content:
                content.ViewModel = null;
                break;
            case ParametersPage parameters:
                parameters.ViewModel = null;
                break;
        }

        PageContent.Content = null;
        _subscribedPage = null;
        _pageInteractions.AttachPage(null);
    }

    private void PageInteractions_FocusSearchRequested(object? sender, EventArgs e) => FocusSearch();

    private void PageCommandBar_FocusSearchRequested(object? sender, EventArgs e) => FocusSearch();

    private void FiltersDropDown_FocusSearchRequested(object? sender, EventArgs e) => FocusSearch();

    private void ToggleFilterFocus()
    {
        if (!FiltersDropDown.IsFilterVisible)
        {
            return;
        }

        if (FiltersDropDown.IsActive)
        {
            FiltersDropDown.CloseDropDownAndFocusSearch();
        }
        else
        {
            FiltersDropDown.OpenDropDown();
        }
    }

    private void PageInteractions_DetailsChanged(object? sender, PageDetailsChangedEventArgs e)
    {
        var sourcePage = _pageInteractions.CurrentPage;
        if (e.Details is null)
        {
            _detailsTimer.Debounce(
                () =>
                {
                    if (!_isDisposed && ReferenceEquals(sourcePage, _pageInteractions.CurrentPage))
                    {
                        HideDetails();
                    }
                },
                interval: TimeSpan.FromMilliseconds(150),
                immediate: false);
            return;
        }

        ShowDetails(sourcePage, e.Details);
    }

    private void ShowDetails(PageViewModel? sourcePage, DetailsViewModel details)
    {
        var wasVisible = DetailsContent.Visibility == Visibility.Visible;

        // Like ShellPage, coalesce rapid detail changes (GH #322). Show the first
        // details right away so the panel doesn't lag behind the selection.
        _detailsTimer.Debounce(ApplyDetails, interval: TimeSpan.FromMilliseconds(100), immediate: !wasVisible);

        void ApplyDetails()
        {
            // immediate=true runs synchronously, so we may not be on the UI thread yet.
            if (!DispatcherQueue.HasThreadAccess)
            {
                if (!DispatcherQueue.TryEnqueue(ApplyDetails))
                {
                    Logger.LogError("Failed to enqueue show details action on UI thread");
                }

                return;
            }

            if (_isDisposed || !ReferenceEquals(sourcePage, _pageInteractions.CurrentPage))
            {
                return;
            }

            try
            {
                DetailsContent.ChangeView(null, 0, null, true);
                DetailsContent.Width = GetDetailsWidth(details.Size);
                DetailsView.SourcePage = sourcePage;
                DetailsView.Details = details;
                DetailsContent.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to show dock page details", ex);
            }
        }
    }

    private void HideDetails()
    {
        DetailsContent.Visibility = Visibility.Collapsed;
        DetailsView.Details = null;
        DetailsView.SourcePage = null;
    }

    private static double GetDetailsWidth(ContentSize? size) => size switch
    {
        ContentSize.Medium => 360,
        ContentSize.Large => 480,
        _ => 280,
    };

    private void SearchBox_BackRequested(object? sender, SearchBarBackRequestedEventArgs e) =>
        ApplyBackAction(e.Kind, e.FromBackspace);

    private void SearchBox_NavigationRequested(object? sender, SearchBarNavigationRequestedEventArgs e)
    {
        switch (e.Direction)
        {
            case SearchBarNavigationDirection.Previous:
                _pageInteractions.NavigatePrevious();
                break;
            case SearchBarNavigationDirection.Next:
                _pageInteractions.NavigateNext();
                break;
            case SearchBarNavigationDirection.Left:
                _pageInteractions.NavigateLeft();
                break;
            case SearchBarNavigationDirection.Right:
                _pageInteractions.NavigateRight();
                break;
            case SearchBarNavigationDirection.PageUp:
                _pageInteractions.NavigatePageUp();
                break;
            case SearchBarNavigationDirection.PageDown:
                _pageInteractions.NavigatePageDown();
                break;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var modifiers = KeyModifiers.GetCurrent();
        if (e.Key == VirtualKey.Left && modifiers.OnlyAlt)
        {
            RequestBackOrClose();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.K && modifiers.OnlyCtrl)
        {
            _pageInteractions.OpenContextMenu();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.F && modifiers.OnlyAlt)
        {
            ToggleFilterFocus();
            e.Handled = true;
        }
        else if (_pageInteractions.TryCommandKeybinding(
            modifiers.Ctrl,
            modifiers.Alt,
            modifiers.Shift,
            modifiers.Win,
            e.Key))
        {
            e.Handled = true;
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var modifiers = KeyModifiers.GetCurrent();
        if (e.Key == VirtualKey.Escape)
        {
            RequestBackOrClose();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Enter && modifiers.OnlyCtrl)
        {
            _pageInteractions.ActivateSecondary();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Enter && modifiers.None)
        {
            _pageInteractions.ActivatePrimary();
            e.Handled = true;
        }
    }

    private void RequestBackOrClose() =>
        ApplyBackAction(SearchBarBackRequestKind.GoBack, fromBackspace: false);

    private void ApplyBackAction(SearchBarBackRequestKind kind, bool fromBackspace)
    {
        var backspaceGoesBack = fromBackspace &&
            App.Current.Services.GetRequiredService<ISettingsService>().Settings.BackspaceGoesBack;
        switch (DockPageBackNavigation.GetAction(kind, fromBackspace, Navigation.CanGoBack, backspaceGoesBack))
        {
            case DockPageBackAction.GoBack:
                _ = ObserveBackNavigationAsync();
                break;
            case DockPageBackAction.Close:
                CloseRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private async Task ObserveBackNavigationAsync()
    {
        try
        {
            await Navigation.GoBackAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to navigate back in a dock page.", ex);
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => RequestBackOrClose();

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _isLoaded = false;
        Loaded -= OnLoaded;
        Navigation.PropertyChanged -= Navigation_PropertyChanged;
        SearchBox.BackRequested -= SearchBox_BackRequested;
        SearchBox.NavigationRequested -= SearchBox_NavigationRequested;
        PageCommandBar.FocusSearchRequested -= PageCommandBar_FocusSearchRequested;
        FiltersDropDown.FocusSearchRequested -= FiltersDropDown_FocusSearchRequested;
        _pageInteractions.FocusSearchRequested -= PageInteractions_FocusSearchRequested;
        _pageInteractions.DetailsChanged -= PageInteractions_DetailsChanged;
        DetachCurrentContent();
        SearchBox.CurrentPageViewModel = null;
        FiltersDropDown.CurrentPageViewModel = null;
        _pageInteractions.Dispose();
        PageCommandBar.Dispose();
        GC.SuppressFinalize(this);
    }
}
