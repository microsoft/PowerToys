// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Indexer;

public partial class IndexerCommandsProvider : CommandProvider
{
    private static readonly SettingsManager _settingsManager = new();
    private readonly FallbackOpenFileItem _fallbackFileItem = new(_settingsManager);

    public IndexerCommandsProvider()
    {
        Id = "Files";
        DisplayName = Resources.IndexerCommandsProvider_DisplayName;
        Icon = Icons.FileExplorer;
        Settings = _settingsManager.Settings;
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return [
            new CommandItem(new IndexerPage(_settingsManager))
            {
                Title = Resources.Indexer_Title,
                Subtitle = Resources.Indexer_Subtitle,
                MoreCommands = [new CommandContextItem(_settingsManager.Settings.SettingsPage)],
            }
        ];
    }

    public override IFallbackCommandItem[] FallbackCommands() =>
        [
            _fallbackFileItem
        ];
}
