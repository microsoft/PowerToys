// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels;

public class PageViewModelFactory : IPageViewModelFactoryService
{
    private readonly TaskScheduler _scheduler;

    public PageViewModelFactory(TaskScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public PageViewModel? TryCreatePageViewModel(IPage page, bool nested, AppExtensionHost host)
    {
        return page switch
        {
            IListPage listPage => new ListViewModel(listPage, _scheduler, host) { IsNested = nested },
            IContentPage contentPage => new ContentPageViewModel(contentPage, _scheduler, host),
            _ => null,
        };
    }
}
