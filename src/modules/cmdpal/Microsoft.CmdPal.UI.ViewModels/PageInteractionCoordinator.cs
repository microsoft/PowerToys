// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Messages;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed class PageInteractionCoordinator(ICommandBarInteractionTarget commandBar) : IDisposable
{
    private PageViewModel? _page;
    private IPageInteractionTarget? _target;
    private IPageInteractionEventSource? _eventSource;
    private bool _isDisposed;

    public PageViewModel? CurrentPage => _page;

    public IPageInteractionTarget? CurrentTarget => _target;

    public event EventHandler<PageDetailsChangedEventArgs>? DetailsChanged;

    public event EventHandler<PageSearchSuggestionChangedEventArgs>? SearchSuggestionChanged;

    public event EventHandler? FocusSearchRequested;

    public event EventHandler<ParameterFocusRequestedEventArgs>? ParameterFocusRequested;

    public event EventHandler<PageDragStateChangedEventArgs>? DragStateChanged;

    public void AttachPage(PageViewModel? page)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (ReferenceEquals(_page, page))
        {
            return;
        }

        DetachPage();
        _page = page;
        if (_page is null)
        {
            commandBar.SetCommandContext(null);
            return;
        }

        _page.CommandBarContextChanged += Page_CommandBarContextChanged;
        _page.DetailsChanged += Page_DetailsChanged;
        _page.SearchSuggestionChanged += Page_SearchSuggestionChanged;
        _page.FocusSearchRequested += Page_FocusSearchRequested;
        _page.ParameterFocusRequested += Page_ParameterFocusRequested;

        commandBar.SetCommandContext(GetInitialCommandContext(_page));
        DetailsChanged?.Invoke(this, new(GetInitialDetails(_page)));
        SearchSuggestionChanged?.Invoke(this, new(_page.TextToSuggest));
    }

    public void AttachTarget(IPageInteractionTarget? target)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (ReferenceEquals(_target, target))
        {
            return;
        }

        DetachTarget();
        _target = target;
        _eventSource = target as IPageInteractionEventSource;
        if (_eventSource is null)
        {
            return;
        }

        _eventSource.ContextMenuCloseRequested += Target_ContextMenuCloseRequested;
        _eventSource.FocusSearchRequested += Target_FocusSearchRequested;
        _eventSource.DragStateChanged += Target_DragStateChanged;
    }

    public void NavigatePrevious() => _target?.NavigatePrevious();

    public void NavigateNext() => _target?.NavigateNext();

    public void NavigateLeft() => _target?.NavigateLeft();

    public void NavigateRight() => _target?.NavigateRight();

    public void NavigatePageUp() => _target?.NavigatePageUp();

    public void NavigatePageDown() => _target?.NavigatePageDown();

    public void ActivatePrimary() => _target?.ActivatePrimary();

    public void ActivateSecondary() => _target?.ActivateSecondary();

    public void OpenContextMenu() => commandBar.OpenContextMenu();

    public void CloseContextMenu() => commandBar.CloseContextMenu();

    public bool TryCommandKeybinding(bool ctrl, bool alt, bool shift, bool win, VirtualKey key) =>
        commandBar.TryCommandKeybinding(ctrl, alt, shift, win, key);

    private void Page_CommandBarContextChanged(object? sender, PageCommandBarContextChangedEventArgs e)
    {
        if (ReferenceEquals(sender, _page))
        {
            commandBar.SetCommandContext(e.Context);
        }
    }

    private void Page_DetailsChanged(object? sender, PageDetailsChangedEventArgs e)
    {
        if (ReferenceEquals(sender, _page))
        {
            DetailsChanged?.Invoke(this, e);
        }
    }

    private void Page_SearchSuggestionChanged(object? sender, PageSearchSuggestionChangedEventArgs e)
    {
        if (ReferenceEquals(sender, _page))
        {
            SearchSuggestionChanged?.Invoke(this, e);
        }
    }

    private void Page_FocusSearchRequested(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _page))
        {
            FocusSearchRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Page_ParameterFocusRequested(object? sender, ParameterFocusRequestedEventArgs e)
    {
        if (ReferenceEquals(sender, _page))
        {
            ParameterFocusRequested?.Invoke(this, e);
        }
    }

    private void Target_ContextMenuCloseRequested(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _eventSource))
        {
            commandBar.CloseContextMenu();
        }
    }

    private void Target_FocusSearchRequested(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _eventSource))
        {
            FocusSearchRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Target_DragStateChanged(object? sender, PageDragStateChangedEventArgs e)
    {
        if (ReferenceEquals(sender, _eventSource))
        {
            DragStateChanged?.Invoke(this, e);
        }
    }

    private static ICommandBarContext? GetInitialCommandContext(PageViewModel page) =>
        page switch
        {
            ContentPageViewModel content => content,
            ParametersPageViewModel { HasActiveList: false, ShowCommand: true } parameters => parameters.Command,
            _ => null,
        };

    private static DetailsViewModel? GetInitialDetails(PageViewModel page) =>
        page is ContentPageViewModel content ? content.Details : null;

    private void DetachPage()
    {
        if (_page is null)
        {
            return;
        }

        _page.CommandBarContextChanged -= Page_CommandBarContextChanged;
        _page.DetailsChanged -= Page_DetailsChanged;
        _page.SearchSuggestionChanged -= Page_SearchSuggestionChanged;
        _page.FocusSearchRequested -= Page_FocusSearchRequested;
        _page.ParameterFocusRequested -= Page_ParameterFocusRequested;
        _page = null;
    }

    private void DetachTarget()
    {
        if (_eventSource is not null)
        {
            _eventSource.ContextMenuCloseRequested -= Target_ContextMenuCloseRequested;
            _eventSource.FocusSearchRequested -= Target_FocusSearchRequested;
            _eventSource.DragStateChanged -= Target_DragStateChanged;
        }

        _eventSource = null;
        _target = null;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        DetachPage();
        DetachTarget();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
