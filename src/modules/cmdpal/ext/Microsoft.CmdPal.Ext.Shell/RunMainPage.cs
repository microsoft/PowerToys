// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CmdPal.Ext.Shell.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell;

internal sealed partial class RunMainPage : DynamicListPage
{
    private readonly ShellListPageHelpers _helper;
    private readonly List<ListItem> _historyItems = new();
    private List<ListItem> _pathItems = new();

    public RunMainPage(SettingsManager settingsManager)
    {
        Name = "Open"; // LOC!
        Title = "Run commands"; // LOC!
        Icon = Icons.RunV2;
        _helper = new(settingsManager);

        EmptyContent = new CommandItem()
        {
            Title = "Run commands",
            Icon = Icons.RunV2,
        };
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        // If the search text is the start of a path to a file (it might be a
        // UNC path), then we want to list all the files that start with that text:

        // 1. Check if the search text is a valid path
        // 2. If it is, then list all the files that start with that text
        var searchText = newSearch.Trim();
        if (string.IsNullOrEmpty(searchText) || string.IsNullOrWhiteSpace(searchText))
        {
            _pathItems.Clear();
            RaiseItemsChanged();
            return;
        }

        var directoryPath = string.Empty;
        var searchPattern = string.Empty;

        // Check if the search text is a valid path
        if (Path.IsPathRooted(searchText) && Path.GetDirectoryName(searchText) is string directoryName)
        {
            directoryPath = directoryName;
            searchPattern = $"{Path.GetFileName(searchText)}*";
        }

        // we should also handle just drive roots, ala c:\ or d:\
        else if (searchText.Length == 2 && searchText[1] == ':')
        {
            directoryPath = searchText + "\\";
            searchPattern = $"*";
        }

        // Check if the search text is a valid UNC path
        else if (searchText.StartsWith(@"\\", System.StringComparison.CurrentCultureIgnoreCase) && searchText.Contains(@"\\"))
        {
            directoryPath = searchText;
            searchPattern = $"*";
        }

        // Check if the directory exists
        if (Directory.Exists(directoryPath))
        {
            // Get all the files in the directory that start with the search text
            var files = Directory.GetFileSystemEntries(directoryPath, searchPattern);

            // Create a list of commands for each file
            var commands = files.Select(PathToListItem).ToList();

            // Add the commands to the list
            _pathItems = commands;

            // ListHelpers.InPlaceUpdateList(_pathItems, commands);
        }
        else
        {
            _pathItems.Clear();

            // Phase 2:
            // Try to parse the search text as a commandline.
            // Is there an executable that the user typed (possibly with args)?

            // If so, then we should add it to the list of commands.

            // Parse the search text as a commandline
            // var commandLine = new CommandLine(searchText);
            // var executablePath = commandLine.ExecutablePath;
            // var arguments = commandLine.Arguments;
            _historyItems.Clear();

            // if (!string.IsNullOrEmpty(executablePath))
            {
                var commands = _helper.Query(searchText);
                if (commands != null && commands.Count > 0)
                {
                    _historyItems.AddRange(commands);
                }

                // var openCommand = new OpenInShellCommand(executablePath, arguments);
                // var item = new ListItem(openCommand)
                // {
                //    Icon = Icons.RunV2,
                //    Title = searchText,
                //    Subtitle = "Run this commnand", // LOC!

                // };
                // If the executable path is empty, then we don't have a commandline to add
                RaiseItemsChanged();
                return;
            }
        }

        RaiseItemsChanged();
    }

    private ListItem PathToListItem(string path)
    {
        return new PathListItem(path);
    }

    public override IListItem[] GetItems()
    {
        return _historyItems.Concat(_pathItems).ToArray();
    }
}
