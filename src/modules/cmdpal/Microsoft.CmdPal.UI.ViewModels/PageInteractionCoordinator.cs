// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Messages;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed class PageInteractionCoordinator(ICommandBarInteractionTarget commandBar) : IDisposable
{
    private PageViewModel? _page;
    private bool _isDisposed;

    public PageViewModel? CurrentPage => _page;

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
        commandBar.SetCommandContext(GetInitialCommandContext(_page));
    }

    private void Page_CommandBarContextChanged(object? sender, PageCommandBarContextChangedEventArgs e)
    {
        if (ReferenceEquals(sender, _page))
        {
            commandBar.SetCommandContext(e.Context);
        }
    }

    private static ICommandBarContext? GetInitialCommandContext(PageViewModel page) =>
        page switch
        {
            ContentPageViewModel content => content,
            ParametersPageViewModel { HasActiveList: false, ShowCommand: true } parameters => parameters.Command,
            _ => null,
        };

    private void DetachPage()
    {
        if (_page is null)
        {
            return;
        }

        _page.CommandBarContextChanged -= Page_CommandBarContextChanged;
        _page = null;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        DetachPage();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}