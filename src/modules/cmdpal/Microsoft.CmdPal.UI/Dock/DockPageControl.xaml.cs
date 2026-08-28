// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Globalization;
using ManagedCommon;
using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Microsoft.CmdPal.UI.Dock;

public sealed partial class DockPageControl : UserControl, IDisposable
{
    private readonly PageInteractionCoordinator _pageInteractions;
    private IListInteractionSource? _listInteractionSource;
    private PageViewModel? _subscribedPage;
    private bool _isLoaded;
    private bool _isDisposed;

    internal DockPageNavigationViewModel Navigation { get; }

    internal PageViewModel? CurrentPage => Navigation.CurrentPage;

    internal event EventHandler? CloseRequested;

    internal DockPageControl(DockPageNavigationViewModel navigation)
    {
        Navigation = navigation;
        InitializeComponent();

        _pageInteractions = new(PageCommandBar);
        _pageInteractions.FocusSearchRequested += PageInteractions_FocusSearchRequested;
        Navigation.PropertyChanged += Navigation_PropertyChanged;
        SearchBox.BackRequested += SearchBox_BackRequested;
        SearchBox.NavigationRequested += SearchBox_NavigationRequested;
        PageCommandBar.FocusSearchRequested += PageCommandBar_FocusSearchRequested;
        AddHandler(PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown), true);
        AddHandler(KeyDownEvent, new KeyEventHandler(OnKeyDown), false);
        Loaded += OnLoaded;
    }

    internal bool HasOpenTransientUi => PageCommandBar.HasOpenTransientUi;

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
        UpdateCurrentPage();
        FocusSearch();
    }

    private void Navigation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DockPageNavigationViewModel.CurrentPage))
        {
            UpdateCurrentPage();
        }

        if (e.PropertyName == nameof(DockPageNavigationViewModel.CanGoBack) && _isLoaded)
        {
            UpdatePageLevelState(useTransitions: true);
        }
    }

    private void UpdatePageLevelState(bool useTransitions) =>
        VisualStateManager.GoToState(
            this,
            Navigation.CanGoBack ? "NestedPage" : "RootPage",
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

        IPageInteractionTarget target = _subscribedPage switch
        {
            ListViewModel list => new ListItemsView { ViewModel = list },
            ContentPageViewModel content => new ContentPage { ViewModel = content },
            ParametersPageViewModel parameters => new ParametersPage { ViewModel = parameters },
            _ => throw new NotSupportedException(),
        };

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
        AttachInteractionTarget(null);

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

    private void SearchBox_BackRequested(object? sender, SearchBarBackRequestedEventArgs e) => RequestBackOrClose();

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
            return;
        }

        if (e.Key == VirtualKey.K && modifiers.OnlyCtrl)
        {
            _pageInteractions.OpenContextMenu();
            e.Handled = true;
            return;
        }

        e.Handled = _pageInteractions.TryCommandKeybinding(
            modifiers.Ctrl,
            modifiers.Alt,
            modifiers.Shift,
            modifiers.Win,
            e.Key);
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

    private void RequestBackOrClose()
    {
        if (Navigation.CanGoBack)
        {
            _ = ObserveBackNavigationAsync();
        }
        else
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
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
        _pageInteractions.FocusSearchRequested -= PageInteractions_FocusSearchRequested;
        DetachCurrentContent();
        SearchBox.CurrentPageViewModel = null;
        _pageInteractions.Dispose();
        PageCommandBar.Dispose();
        Navigation.Dispose();
        GC.SuppressFinalize(this);
    }
}
