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
    private readonly ILogger _logger;

    public CommandPalettePageViewModelFactory(TaskScheduler scheduler, ILogger logger)
    {
        _scheduler = scheduler;
        _logger = logger;
    }

    public PageViewModel? TryCreatePageViewModel(IPage page, bool nested, AppExtensionHost host)
    {
        return page switch
        {
            IListPage listPage => new ListViewModel(listPage, _scheduler, host, _logger) { IsNested = nested },
            IContentPage contentPage => new CommandPaletteContentPageViewModel(contentPage, _scheduler, host),
            _ => null,
        };
    }
}
