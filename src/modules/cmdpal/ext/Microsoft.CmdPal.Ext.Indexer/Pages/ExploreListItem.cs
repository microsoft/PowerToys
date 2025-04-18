// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Indexer.Commands;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

#nullable enable
namespace Microsoft.CmdPal.Ext.Indexer;

/// <summary>
/// This is almost more of just a sample than anything.
/// </summary>
internal sealed partial class ExploreListItem : ListItem
{
    internal string FilePath { get; private set; }

    internal event TypedEventHandler<ExploreListItem, string>? PathChangeRequested;

    public ExploreListItem(IndexerItem indexerItem)
        : base(new NoOpCommand())
    {
        FilePath = indexerItem.FullPath;

        Title = indexerItem.FileName;
        Subtitle = indexerItem.FullPath;
        List<CommandContextItem> context = [];
        if (indexerItem.IsDirectory())
        {
            Command = new AnonymousCommand(
                () => { PathChangeRequested?.Invoke(this, FilePath); })
            {
                Result = CommandResult.KeepOpen(),
                Name = Resources.Indexer_Command_Browse,
            };
            context.Add(new CommandContextItem(new DirectoryPage(indexerItem.FullPath)));
        }
        else
        {
            Command = new OpenFileCommand(indexerItem);
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
