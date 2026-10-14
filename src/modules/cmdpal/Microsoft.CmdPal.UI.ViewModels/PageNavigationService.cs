// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

internal sealed class PageNavigationService(
    IPageViewModelFactoryService pageFactory,
    IAppHostService appHostService)
{
    public AppExtensionHost ResolveHost(PerformCommandMessage message, PageViewModel? currentPage) =>
        appHostService.GetHostForCommand(
            message.CommandContext,
            message.Context?.ExtensionHost ?? currentPage?.ExtensionHost);

    public ICommandProviderContext ResolveProviderContext(PerformCommandMessage message, PageViewModel? currentPage) =>
        appHostService.GetProviderContextForCommand(
            message.CommandContext,
            message.Context?.ProviderContext ?? currentPage?.ProviderContext);

    public PageViewModel? TryPreparePage(
        IPage page,
        bool nested,
        AppExtensionHost host,
        ICommandProviderContext providerContext,
        ListPageLaunchOptions? launchOptions)
    {
        if (launchOptions is { } options && (options.IsEmpty || page is not IListPage))
        {
            throw new NotSupportedException("List page launch options can only be applied to list pages and must contain a query or filter.");
        }

        var viewModel = pageFactory.TryCreatePageViewModel(page, nested, host, providerContext);
        if (viewModel is null)
        {
            return null;
        }

        if (launchOptions is not null)
        {
            if (viewModel is not ListViewModel listViewModel)
            {
                viewModel.SafeCleanup();
                throw new NotSupportedException("List page launch options can only be applied to list pages.");
            }

            listViewModel.SetLaunchOptions(launchOptions);
        }

        viewModel.IsRootPage = !nested;
        viewModel.HasBackButton = nested;
        return viewModel;
    }

    public static Task InitializePageAsync(PageViewModel page, CancellationToken cancellationToken)
    {
        if (page.IsInitialized || page.InitializeCommand is null)
        {
            return Task.CompletedTask;
        }

        // Scheduling can be canceled, but an extension call already in progress must
        // finish before the caller can safely release the page.
        return Task.Run(
            async () =>
            {
                page.InitializeCommand.Execute(null);
                if (page.InitializeCommand.ExecutionTask is Task executionTask)
                {
                    await executionTask;
                }
            },
            cancellationToken);
    }
}
