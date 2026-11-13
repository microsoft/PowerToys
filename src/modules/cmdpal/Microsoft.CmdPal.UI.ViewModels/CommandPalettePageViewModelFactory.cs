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

    public PageViewModel? TryCreatePageViewModel(
        IPage page,
        bool nested,
        AppExtensionHost host,
        ICommandProviderContext providerContext,
        FallbackQueryContext? fallbackContext = null)
    {
        // Take the reference while the result is still current, before the page
        // subscribes to host events in its constructor.
        IDisposable? snapshotLease = null;
        if (fallbackContext is not null
            && (!fallbackContext.CanInvoke || !fallbackContext.TryAcquireSnapshotLease(out snapshotLease)))
        {
            return null;
        }

        // Once navigation is accepted, the page owns its snapshot reference. A new
        // search must not disable its forms, output commands, or nested pages.
        var pageFallbackContext = fallbackContext is null
            ? null
            : fallbackContext with { QueryToken = CancellationToken.None };

        PageViewModel? viewModel = page switch
        {
            MainListPage listPage => new ListViewModel(listPage, _scheduler, host, providerContext, _contextMenuFactory) { IsRootPage = !nested, IsMainPage = true },
            IListPage listPage => new ListViewModel(listPage, _scheduler, host, providerContext, _contextMenuFactory) { IsRootPage = !nested },
            IContentPage contentPage => new CommandPaletteContentPageViewModel(contentPage, _scheduler, host, providerContext),
            IParametersPage paramsPage => new ParametersPageViewModel(paramsPage, _scheduler, host, providerContext, _contextMenuFactory, pageFallbackContext),
            _ => null,
        };

        if (viewModel is null)
        {
            snapshotLease?.Dispose();
            return null;
        }

        viewModel.AttachFallbackContext(pageFallbackContext, snapshotLease);
        return viewModel;
    }
}
