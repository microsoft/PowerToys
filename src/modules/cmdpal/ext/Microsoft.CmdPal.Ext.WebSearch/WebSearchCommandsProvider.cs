// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.WebSearch.Commands;
using Microsoft.CmdPal.Ext.WebSearch.Helpers;
using Microsoft.CmdPal.Ext.WebSearch.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WebSearch;

public sealed partial class WebSearchCommandsProvider : CommandProvider
{
    private readonly SettingsManager _settingsManager = new();
    private readonly FallbackExecuteSearchItem _fallbackItem;
    private readonly FallbackOpenURLItem _openUrlFallbackItem;
    private readonly WebSearchTopLevelCommandItem _webSearchTopLevelItem;
    private readonly ICommandItem[] _topLevelItems;
    private readonly IFallbackCommandItem[] _fallbackCommands;

    public WebSearchCommandsProvider()
    {
        Id = "com.microsoft.cmdpal.builtin.websearch";
        DisplayName = Resources.extension_name;
        Icon = Icons.WebSearch;
        Settings = _settingsManager.Settings;

        _fallbackItem = new FallbackExecuteSearchItem(_settingsManager);
        _openUrlFallbackItem = new FallbackOpenURLItem(_settingsManager);

        _webSearchTopLevelItem = new WebSearchTopLevelCommandItem(_settingsManager)
        {
            MoreCommands =
            [
                new CommandContextItem(Settings!.SettingsPage),
            ],
        };
        _topLevelItems = [_webSearchTopLevelItem];
        _fallbackCommands = [_openUrlFallbackItem, _fallbackItem];
    }

    public override ICommandItem[] TopLevelCommands() => _topLevelItems;

    public override IFallbackCommandItem[]? FallbackCommands() => _fallbackCommands;

    public override void Dispose()
    {
        _webSearchTopLevelItem?.Dispose();

        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
