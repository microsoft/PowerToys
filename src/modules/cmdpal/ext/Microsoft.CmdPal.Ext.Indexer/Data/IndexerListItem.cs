// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CmdPal.Core.Common.Commands;
using Microsoft.CmdPal.Ext.Indexer.Helpers;
using Microsoft.CmdPal.Ext.Indexer.Pages;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation.Metadata;
using FileAttributes = System.IO.FileAttributes;

namespace Microsoft.CmdPal.Ext.Indexer.Data;

internal sealed partial class IndexerListItem : ListItem
{
    internal static readonly bool IsActionsFeatureEnabled = GetFeatureFlag();

    private static bool GetFeatureFlag()
    {
        var env = System.Environment.GetEnvironmentVariable("CMDPAL_ENABLE_ACTIONS_LIST");
        return !string.IsNullOrEmpty(env) &&
           (env == "1" || env.Equals("true", System.StringComparison.OrdinalIgnoreCase));
    }

    internal string FilePath { get; private set; }

    public IndexerListItem(
        IndexerItem indexerItem,
        IncludeBrowseCommand browseByDefault = IncludeBrowseCommand.Include)
        : base()
    {
        FilePath = indexerItem.FullPath;

        Title = indexerItem.FileName;
        Subtitle = indexerItem.FullPath;

        DataPackage = DataPackageHelper.CreateDataPackageForPath(this, FilePath);

        var commands = FileCommands(indexerItem.FullPath, browseByDefault);
        if (commands.Any())
        {
            Command = commands.First().Command;
            MoreCommands = commands.Skip(1).ToArray();
        }
    }

    public static IEnumerable<CommandContextItem> FileCommands(string fullPath)
    {
        return FileCommands(fullPath, IncludeBrowseCommand.Include);
    }

    internal static IEnumerable<CommandContextItem> FileCommands(
        string fullPath,
        IncludeBrowseCommand browseByDefault = IncludeBrowseCommand.Include)
    {
        List<CommandContextItem> commands = [];
        if (!Path.Exists(fullPath))
        {
            return commands;
        }

        // detect whether it is a directory or file
        var attr = File.GetAttributes(fullPath);
        var isDir = (attr & FileAttributes.Directory) == FileAttributes.Directory;

        var openCommand = new OpenFileCommand(fullPath) { Name = Resources.Indexer_Command_OpenFile };
        if (isDir)
        {
            var directoryPage = new DirectoryPage(fullPath);
            if (browseByDefault == IncludeBrowseCommand.AsDefault)
            {
                // AsDefault: browse dir first, then open in explorer
                commands.Add(new CommandContextItem(directoryPage));
                commands.Add(new CommandContextItem(openCommand));
            }
            else if (browseByDefault == IncludeBrowseCommand.Include)
            {
                // AsDefault: open in explorer first, then browse
                commands.Add(new CommandContextItem(openCommand));
                commands.Add(new CommandContextItem(directoryPage));
            }
            else if (browseByDefault == IncludeBrowseCommand.Exclude)
            {
                // AsDefault: Just open in explorer
                commands.Add(new CommandContextItem(openCommand));
            }
        }
        else
        {
            commands.Add(new CommandContextItem(openCommand));
        }

        commands.Add(new CommandContextItem(new OpenWithCommand(fullPath)));
        commands.Add(new CommandContextItem(new ShowFileInFolderCommand(fullPath) { Name = Resources.Indexer_Command_ShowInFolder }) { RequestedShortcut = KeyChords.OpenFileLocation });
        commands.Add(new CommandContextItem(new CopyPathCommand(fullPath) { Name = Resources.Indexer_Command_CopyPath }) { RequestedShortcut = KeyChords.CopyFilePath });
        commands.Add(new CommandContextItem(new OpenInConsoleCommand(fullPath)) { RequestedShortcut = KeyChords.OpenInConsole });
        commands.Add(new CommandContextItem(new OpenPropertiesCommand(fullPath)));

        if (IsActionsFeatureEnabled && ApiInformation.IsApiContractPresent("Windows.AI.Actions.ActionsContract", 4))
        {
            var actionsListContextItem = new ActionsListContextItem(fullPath);
            if (actionsListContextItem.AnyActions())
            {
                commands.Add(actionsListContextItem);
            }
        }

        return commands;
    }
}

internal enum IncludeBrowseCommand
{
    AsDefault = 0,
    Include = 1,
    Exclude = 2,
}
