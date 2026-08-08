// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public class CommandPalettePageViewModelFactory
    : IPageViewModelFactoryService
{
    private readonly TaskScheduler _scheduler;
    private readonly IContextMenuFactory _contextMenuFactory;
    private readonly ISettingsService _settingsService;

    public CommandPalettePageViewModelFactory(TaskScheduler scheduler, IContextMenuFactory contextMenuFactory, ISettingsService settingsService)
    {
        _scheduler = scheduler;
        _contextMenuFactory = contextMenuFactory;
        _settingsService = settingsService;
    }

    public PageViewModel? TryCreatePageViewModel(IPage page, bool nested, AppExtensionHost host, ICommandProviderContext providerContext)
    {
        return page switch
        {
            MainListPage listPage => new ListViewModel(listPage, _scheduler, host, providerContext, _contextMenuFactory, _settingsService) { IsRootPage = !nested, IsMainPage = true },
            IListPage listPage => new ListViewModel(listPage, _scheduler, host, providerContext, _contextMenuFactory, _settingsService) { IsRootPage = !nested },
            IContentPage contentPage => new CommandPaletteContentPageViewModel(contentPage, _scheduler, host, providerContext),
            IParametersPage paramsPage => new ParametersPageViewModel(paramsPage, _scheduler, host, providerContext, _contextMenuFactory, _settingsService),
            _ => null,
        };
    }
}
