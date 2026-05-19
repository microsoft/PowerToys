// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Ext.Run;
using Microsoft.CmdPal.Ext.Shell.Helpers;
using Microsoft.CmdPal.Ext.Shell.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell;

public partial class ShellCommandsProvider : CommandProvider, IDisposable
{
    private readonly CommandItem _shellPageItem;

    private readonly SettingsManager _settingsManager = new();
    private readonly RunListPage _runListPage;
    private readonly FallbackCommandItem _fallbackItem;
    private readonly IRunHistoryService _historyService;
    private readonly ITelemetryService _telemetryService;

    public ShellCommandsProvider(IRunHistoryService runHistoryService, ITelemetryService telemetryService)
    {
        _historyService = runHistoryService;
        _telemetryService = telemetryService;

        Id = "com.microsoft.cmdpal.builtin.run";
        DisplayName = Resources.cmd_plugin_name;
        Icon = Icons.RunV2Icon;
        Settings = _settingsManager.Settings;

        _runListPage = new RunListPage(runHistoryService, telemetryService, true);

        _fallbackItem = new FallbackExecuteItem(_historyService, _telemetryService);

        _shellPageItem = new CommandItem(_runListPage)
        {
            Icon = Icons.RunV2Icon,
            Title = Resources.shell_command_name,
            MoreCommands = [
                new CommandContextItem(Settings.SettingsPage),
            ],
        };
    }

    public override ICommandItem[] TopLevelCommands() => [_shellPageItem];

    public override IFallbackCommandItem[]? FallbackCommands() => [_fallbackItem];

    public static bool SuppressFileFallbackIf(string query) => FallbackExecuteItem.SuppressFileFallbackIf(query);

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        base.Dispose();
        _runListPage.Dispose();
    }
}
