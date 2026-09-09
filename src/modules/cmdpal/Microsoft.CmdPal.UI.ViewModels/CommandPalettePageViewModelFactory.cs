// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.MainPage;
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
            MainListPage listPage => new ListViewModel(listPage, _scheduler, host, providerContext, _contextMenuFactory) { IsRootPage = !nested, IsMainPage = true },
            IListPage listPage => new ListViewModel(listPage, _scheduler, host, providerContext, _contextMenuFactory) { IsRootPage = !nested },

            // ITabbedPage must be matched before IContentPage: the Toolkit
            // TabbedPage base implements both, and only newer hosts render the
            // tab strip (older hosts fall back to the IContentPage message).
            ITabbedPage tabbedPage => new TabbedPageViewModel(tabbedPage, _scheduler, host, providerContext, this) { IsRootPage = !nested },
            IContentPage contentPage => new CommandPaletteContentPageViewModel(contentPage, _scheduler, host, providerContext),
            IParametersPage paramsPage => new ParametersPageViewModel(paramsPage, _scheduler, host, providerContext, _contextMenuFactory),
            _ => null,
        };
    }
}
