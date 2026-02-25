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

    public CommandPalettePageViewModelFactory(TaskScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public PageViewModel? TryCreatePageViewModel(IPage page, bool nested, AppExtensionHost host, CommandProviderContext providerContext)
    {
        return page switch
        {
            MainListPage listPage => new ListViewModel(listPage, _scheduler, host, providerContext) { IsNested = nested, IsMainPage = true },
            IListPage listPage => new ListViewModel(listPage, _scheduler, host, providerContext) { IsNested = nested },
            IContentPage contentPage => new CommandPaletteContentPageViewModel(contentPage, _scheduler, host, providerContext),
            _ => null,
        };
    }
}
