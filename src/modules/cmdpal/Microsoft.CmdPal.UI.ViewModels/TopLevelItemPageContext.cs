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
public partial class TopLevelItemPageContext : IPageContext
{
    public TaskScheduler Scheduler { get; private set; }

    public ICommandProviderContext ProviderContext { get; private set; }

    TaskScheduler IPageContext.Scheduler => Scheduler;

    ICommandProviderContext IPageContext.ProviderContext => ProviderContext;

    internal TopLevelItemPageContext(CommandProviderWrapper provider, TaskScheduler scheduler)
    {
        ProviderContext = provider.GetProviderContext();
        Scheduler = scheduler;
    }

    public void ShowException(Exception ex, string? extensionHint = null)
    {
        var message = DiagnosticsHelper.BuildExceptionMessage(ex, extensionHint ?? $"TopLevelItemPageContext({ProviderContext.ProviderId})");
        CommandPaletteHost.Instance.Log(message);
    }
}
