// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Indexer.Commands;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.Indexer.Data;

internal sealed partial class IndexerListItem : ListItem
{
    private readonly IndexerItem _indexerItem;

    public IndexerListItem(IndexerItem indexerItem)
        : base(new OpenFileCommand(indexerItem))
    {
        _indexerItem = indexerItem;
        Title = indexerItem.FileName;
        Subtitle = indexerItem.FullPath;

        MoreCommands = [
            new CommandContextItem(new OpenWithCommand(indexerItem)),
            new CommandContextItem(new ShowFileInFolderCommand(indexerItem)),
            new CommandContextItem(new CopyPathCommand(indexerItem)),
            new CommandContextItem(new OpenInConsoleCommand(indexerItem)),
            new CommandContextItem(new OpenPropertiesCommand(indexerItem)),
        ];
    }
}
