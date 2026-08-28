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
        // A page from a fallback result stays valid only while its snapshot is open.
        // Take the reference before construction. If we built the view-model first, we
        // would have to tear it down again, and it subscribes to host events in its
        // constructor.
        IDisposable? snapshotLease = null;
        if (fallbackContext is not null && !fallbackContext.TryAcquireSnapshotLease(out snapshotLease))
        {
            return null;
        }

        PageViewModel? viewModel = page switch
        {
            MainListPage listPage => new ListViewModel(listPage, _scheduler, host, providerContext, _contextMenuFactory) { IsRootPage = !nested, IsMainPage = true },
            IListPage listPage => new ListViewModel(listPage, _scheduler, host, providerContext, _contextMenuFactory) { IsRootPage = !nested },
            IContentPage contentPage => new CommandPaletteContentPageViewModel(contentPage, _scheduler, host, providerContext),
            IParametersPage paramsPage => new ParametersPageViewModel(paramsPage, _scheduler, host, providerContext, _contextMenuFactory, fallbackContext),
            _ => null,
        };

        if (viewModel is null)
        {
            snapshotLease?.Dispose();
            return null;
        }

        viewModel.AttachFallbackContext(fallbackContext, snapshotLease);
        return viewModel;
    }
}
