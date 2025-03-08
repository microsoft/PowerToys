// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Indexer.Commands;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Indexer.Data;

internal sealed partial class IndexerListItem : ListItem
{
    internal string FilePath { get; private set; }

    public IndexerListItem(
        IndexerItem indexerItem,
        IncludeBrowseCommand browseByDefault = IncludeBrowseCommand.Include)
        : base(new OpenFileCommand(indexerItem))
    {
        FilePath = indexerItem.FullPath;

        Title = indexerItem.FileName;
        Subtitle = indexerItem.FullPath;
        List<CommandContextItem> context = [];
        if (indexerItem.IsDirectory())
        {
            var directoryPage = new DirectoryPage(indexerItem.FullPath);
            if (browseByDefault == IncludeBrowseCommand.AsDefault)
            {
                // Swap the open file command into the context menu
                context.Add(new CommandContextItem(Command));
                Command = directoryPage;
            }
            else if (browseByDefault == IncludeBrowseCommand.Include)
            {
                context.Add(new CommandContextItem(directoryPage));
            }
        }

        MoreCommands = [
            ..context,
            new CommandContextItem(new OpenWithCommand(indexerItem)),
            new CommandContextItem(new ShowFileInFolderCommand(indexerItem.FullPath) { Name = Resources.Indexer_Command_ShowInFolder }),
            new CommandContextItem(new CopyPathCommand(indexerItem)),
            new CommandContextItem(new OpenInConsoleCommand(indexerItem)),
            new CommandContextItem(new OpenPropertiesCommand(indexerItem)),
        ];
    }
}

internal enum IncludeBrowseCommand
{
    AsDefault = 0,
    Include = 1,
    Exclude = 2,
}
