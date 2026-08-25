// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.CmdPal.Common.WinGet.Services;
using Microsoft.CmdPal.Ext.WinGet.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WinGet;

internal sealed partial class WinGetFallback : PassiveFallbackCommandItem
{
    private const string CommandId = "com.microsoft.cmdpal.winget.fallback";
    private readonly IWinGetPackageManagerService _packageManager;
    private readonly IWinGetOperationTrackerService _operationTracker;
    private readonly TaskScheduler _uiScheduler;

    internal WinGetFallback(
        IWinGetPackageManagerService packageManager,
        IWinGetOperationTrackerService operationTracker,
        TaskScheduler uiScheduler)
        : base(Properties.Resources.winget_fallback_display_title, CommandId)
    {
        _packageManager = packageManager;
        _operationTracker = operationTracker;
        _uiScheduler = uiScheduler;
        Name = Properties.Resources.winget_fallback_display_title;
        Title = Properties.Resources.winget_fallback_display_title;
        TitleTemplate = Properties.Resources.winget_fallback_title_template;
        SubtitleTemplate = Properties.Resources.winget_fallback_subtitle;
        SuggestedMinQueryLength = 1;
        Icon = Icons.WinGetIcon;
    }

    public override ICommand CreateCommand(IFallbackCommandInvocationArgs args)
    {
        var page = new WinGetExtensionPage(_packageManager, _operationTracker, _uiScheduler)
        {
            SearchText = args.Query,
        };
        return page;
    }
}
