// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Helpers;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Used as the PageContext for top-level items. Top level items are displayed
/// on the MainListPage, which _we_ own. We need to have a placeholder page
/// context for each provider that still connects those top-level items to the
/// CommandProvider they came from.
/// </summary>
public partial class TopLevelItemPageContext : IPageContext, ICommandContextSource
{
    private readonly CommandProviderWrapper _provider;

    public TaskScheduler Scheduler { get; private set; }

    public ICommandProviderContext ProviderContext { get; private set; }

    TaskScheduler IPageContext.Scheduler => Scheduler;

    ICommandProviderContext IPageContext.ProviderContext => ProviderContext;

    // The wrapper creates this context before it creates its host, so read the host on demand.
    AppExtensionHost? ICommandContextSource.ExtensionHost => _provider.ExtensionHost;

    ICommandProviderContext? ICommandContextSource.ProviderContext => ProviderContext;

    internal TopLevelItemPageContext(CommandProviderWrapper provider, TaskScheduler scheduler)
    {
        _provider = provider;
        ProviderContext = provider.GetProviderContext();
        Scheduler = scheduler;
    }

    public void ShowException(Exception ex, string? extensionHint = null)
    {
        var message = DiagnosticsHelper.BuildExceptionMessage(ex, extensionHint ?? $"TopLevelItemPageContext({ProviderContext.ProviderId})");
        CommandPaletteHost.Instance.Log(message);
    }
}
