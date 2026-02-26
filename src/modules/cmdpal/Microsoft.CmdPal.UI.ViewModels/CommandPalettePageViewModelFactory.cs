// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.UI.ViewModels;

public class CommandPalettePageViewModelFactory
    : IPageViewModelFactoryService
{
    private readonly TaskScheduler _scheduler;
    private readonly ILoggerFactory _loggerFactory;

    public CommandPalettePageViewModelFactory(
        TaskScheduler scheduler,
        ILoggerFactory loggerFactory)
    {
        _scheduler = scheduler;
        _loggerFactory = loggerFactory;
    }

    public PageViewModel? TryCreatePageViewModel(IPage page, bool nested, AppExtensionHost host, CommandProviderContext providerContext)
    {
        return page switch
        {
            IListPage listPage => new ListViewModel(listPage, _scheduler, host, providerContext, _loggerFactory) { IsNested = nested },
            IContentPage contentPage => new CommandPaletteContentPageViewModel(contentPage, _scheduler, host, providerContext),
            _ => null,
        };
    }
}
