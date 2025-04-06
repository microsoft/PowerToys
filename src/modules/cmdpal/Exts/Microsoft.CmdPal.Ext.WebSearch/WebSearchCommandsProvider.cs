// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WebSearch.Commands;
using Microsoft.CmdPal.Ext.WebSearch.Helpers;
using Microsoft.CmdPal.Ext.WebSearch.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WebSearch;

public partial class WebSearchCommandsProvider : CommandProvider
{
    private readonly SettingsManager _settingsManager = new();
    private readonly FallbackExecuteSearchItem _fallbackItem;

    public WebSearchCommandsProvider()
    {
        Id = "WebSearch";
        DisplayName = Resources.extension_name;
        Icon = IconHelpers.FromRelativePath("Assets\\WebSearch.png");
        Settings = _settingsManager.Settings;

        _fallbackItem = new FallbackExecuteSearchItem(_settingsManager);
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return [new WebSearchTopLevelCommandItem(_settingsManager)
        {
            MoreCommands = [
                new CommandContextItem(Settings!.SettingsPage),
            ],
        }
        ];
    }

    public override IFallbackCommandItem[]? FallbackCommands() => [_fallbackItem];
}
