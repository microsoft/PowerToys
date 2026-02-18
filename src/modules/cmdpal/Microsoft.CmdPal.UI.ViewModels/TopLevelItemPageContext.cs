// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CmdPal.Core.ViewModels;

namespace Microsoft.CmdPal.UI.ViewModels;

internal partial class TopLevelItemPageContext : IPageContext
{
    public TaskScheduler Scheduler { get; private set; }

    public CommandProviderContext ProviderContext { get; private set; }

    TaskScheduler IPageContext.Scheduler => Scheduler;

    CommandProviderContext IPageContext.ProviderContext => ProviderContext;

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
