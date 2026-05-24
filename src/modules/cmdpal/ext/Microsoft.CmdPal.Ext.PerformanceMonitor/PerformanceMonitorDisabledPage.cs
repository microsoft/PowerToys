// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CoreWidgetProvider.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

internal sealed partial class PerformanceMonitorDisabledPage : ContentPage
{
    private readonly MarkdownContent _content;

    public PerformanceMonitorDisabledPage(PerformanceMonitorCommandsProvider provider)
    {
        Id = PerformanceMonitorCommandsProvider.PageIdValue;
        Name = Resources.GetResource("Performance_Monitor_Disabled_Title");
        Title = Resources.GetResource("Performance_Monitor_Title");
        Icon = Icons.PerformanceMonitorIcon;

        _content = new MarkdownContent(Resources.GetResource("Performance_Monitor_Disabled_Body"));
        Commands =
        [
            new CommandContextItem(new ReactivatePerformanceMonitorCommand(provider)),
        ];
    }

    public override IContent[] GetContent()
    {
        return [_content];
    }

    private sealed partial class ReactivatePerformanceMonitorCommand(PerformanceMonitorCommandsProvider provider) : InvokableCommand
    {
        private readonly PerformanceMonitorCommandsProvider _provider = provider;

        public override string Id => "com.microsoft.cmdpal.performanceWidget.reactivate";

        public override IconInfo Icon => Icons.NavigateForwardIcon;

        public override string Name => Resources.GetResource("Performance_Monitor_Reenable_Title");

        public override ICommandResult Invoke()
        {
            if (_provider.TryReactivateImmediately())
            {
                return CommandResult.ShowToast(new ToastArgs
                {
                    Message = Resources.GetResource("Performance_Monitor_Reenable_Success"),
                    Result = CommandResult.GoHome(),
                });
            }

            return CommandResult.ShowToast(Resources.GetResource("Performance_Monitor_Reenable_Failed"));
        }
    }
}
