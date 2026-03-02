// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public class CommandPalettePageViewModelFactory
    : IPageViewModelFactoryService
{
    private readonly TaskScheduler _scheduler;
    private readonly IContextMenuFactory _contextMenuFactory;

    public CommandPalettePageViewModelFactory(TaskScheduler scheduler, IContextMenuFactory contextMenuFactory)
    {
        _scheduler = scheduler;
        _contextMenuFactory = contextMenuFactory;
    }

    public PageViewModel? TryCreatePageViewModel(IPage page, bool nested, AppExtensionHost host, ICommandProviderContext providerContext)
    {
        return page switch
        {
            IListPage listPage => new ListViewModel(listPage, _scheduler, host, providerContext, _contextMenuFactory) { IsRootPage = !nested },
            IContentPage contentPage => new CommandPaletteContentPageViewModel(contentPage, _scheduler, host, providerContext),
            _ => null,
        };
    }
}
