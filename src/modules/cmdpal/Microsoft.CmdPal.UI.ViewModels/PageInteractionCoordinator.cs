// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Messages;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed class PageInteractionCoordinator(ICommandBarInteractionTarget commandBar) : IDisposable
{
    private PageViewModel? _page;
    private IPageInteractionTarget? _target;
    private bool _isDisposed;

    public PageViewModel? CurrentPage => _page;

    public IPageInteractionTarget? CurrentTarget => _target;

    public event EventHandler<PageDetailsChangedEventArgs>? DetailsChanged;

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
        commandBar.SetCommandContext(GetInitialCommandContext(_page));
        DetailsChanged?.Invoke(this, new(GetInitialDetails(_page)));
    }

    public void AttachTarget(IPageInteractionTarget? target)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        _target = target;
    }

    public void NavigatePrevious() => _target?.NavigatePrevious();

    public void NavigateNext() => _target?.NavigateNext();

    public void NavigateLeft() => _target?.NavigateLeft();

    public void NavigateRight() => _target?.NavigateRight();

    public void NavigatePageUp() => _target?.NavigatePageUp();

    public void NavigatePageDown() => _target?.NavigatePageDown();

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
        _page = null;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        DetachPage();
        _target = null;
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}