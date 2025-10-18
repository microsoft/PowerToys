// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CmdPal.Ext.Shell.Helpers;
using Microsoft.CmdPal.Ext.Shell.Pages;
using Microsoft.CmdPal.Ext.Shell.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell;

public partial class ShellCommandsProvider : CommandProvider
{
    private readonly CommandItem _shellPageItem;

    private readonly SettingsManager _settingsManager = new();
    private readonly ShellListPage _shellListPage;
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

        _shellListPage = new ShellListPage(_settingsManager, _historyService, _telemetryService);

        _fallbackItem = new FallbackExecuteItem(_settingsManager, _shellListPage.AddToHistory, _telemetryService);

        _shellPageItem = new CommandItem(_shellListPage)
        {
            Icon = Icons.RunV2Icon,
            Title = Resources.shell_command_name,
            Subtitle = Resources.cmd_plugin_description,
            MoreCommands = [
                new CommandContextItem(Settings.SettingsPage),
            ],
        };
    }

    public override ICommandItem[] TopLevelCommands() => [_shellPageItem];

    public override IFallbackCommandItem[]? FallbackCommands() => [_fallbackItem];

    public static bool SuppressFileFallbackIf(string query) => FallbackExecuteItem.SuppressFileFallbackIf(query);
}
