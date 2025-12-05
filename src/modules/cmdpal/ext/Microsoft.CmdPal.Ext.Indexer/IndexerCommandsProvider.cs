// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation.Metadata;

namespace Microsoft.CmdPal.Ext.Indexer;

public partial class IndexerCommandsProvider : CommandProvider
{
    private readonly FallbackOpenFileItem _fallbackFileItem = new();

    public IndexerCommandsProvider()
    {
        Id = "Files";
        DisplayName = Resources.IndexerCommandsProvider_DisplayName;
        Icon = Icons.FileExplorerIcon;

        if (IndexerListItem.IsActionsFeatureEnabled && ApiInformation.IsApiContractPresent("Windows.AI.Actions.ActionsContract", 4))
        {
            _ = ActionRuntimeManager.InstanceAsync;
        }
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return [
            new CommandItem(new IndexerPage())
            {
                Title = Resources.Indexer_Title,
            }
        ];
    }

    public override IFallbackCommandItem[] FallbackCommands() =>
        [
            _fallbackFileItem
        ];

    public void SuppressFallbackWhen(Func<string, bool> callback)
    {
        _fallbackFileItem.SuppressFallbackWhen(callback);
    }
}
