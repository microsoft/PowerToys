// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell;

internal sealed partial class RunMainPage : DynamicListPage
{
    private readonly List<ListItem> _pathItems = new();

    public RunMainPage()
    {
        Name = "Open"; // LOC!
        Title = "Run commands"; // LOC!
        Icon = Icons.RunV2;

        EmptyContent = new CommandItem()
        {
            Title = "Run commands",
            Icon = Icons.RunV2,
        };
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        // If the search text is the start of a path to a file (it might be a unc path), then we want to list all the files that start with that text:

        // 1. Check if the search text is a valid path
        // 2. If it is, then list all the files that start with that text
        var searchText = newSearch.Trim();
        if (string.IsNullOrEmpty(searchText) || string.IsNullOrWhiteSpace(searchText))
        {
            _pathItems.Clear();
            RaiseItemsChanged();
            return;
        }

        // Check if the search text is a valid path
        if (Path.IsPathRooted(searchText) && Path.GetDirectoryName(searchText) is string directoryName)
        {
            // Check if the directory exists
            if (Directory.Exists(directoryName))
            {
                // Get all the files in the directory that start with the search text
                var files = Directory.GetFileSystemEntries(directoryName, $"{Path.GetFileName(searchText)}*");

                // Create a list of commands for each file
                var commands = files.Select(PathToListItem).ToList();

                // Add the commands to the list
                ListHelpers.InPlaceUpdateList(_pathItems, commands);
            }
        }

        // we should also handle just drive roots, ala c:\ or d:\
        else if (searchText.Length == 2 && searchText[1] == ':')
        {
            // Check if the drive exists
            if (Directory.Exists(searchText + "\\"))
            {
                // Get all the files in the directory that start with the search text
                var files = Directory.GetFileSystemEntries(searchText + "\\", "*");

                // Create a list of commands for each file
                var commands = files.Select(PathToListItem).ToList();

                // Add the commands to the list
                ListHelpers.InPlaceUpdateList(_pathItems, commands);
            }
        }

        // Check if the search text is a valid UNC path
        else if (searchText.StartsWith(@"\\", System.StringComparison.CurrentCultureIgnoreCase) && searchText.Contains(@"\\"))
        {
            // Check if the directory exists
            if (Directory.Exists(searchText))
            {
                // Get all the files in the directory that start with the search text
                var files = Directory.GetFileSystemEntries(searchText, "*");

                // Create a list of commands for each file
                var commands = files.Select(PathToListItem).ToList();

                // Add the commands to the list
                ListHelpers.InPlaceUpdateList(_pathItems, commands);
            }
        }

        RaiseItemsChanged();
    }

    private ListItem PathToListItem(string path)
    {
        // var iconStream = ThumbnailHelper.GetThumbnail(path).Result;
        // var icon = iconStream != null ? IconInfo.FromStream(iconStream) : Icons.RunV2;
        // var fileName = Path.GetFileName(path);
        // var isDirectory = Directory.Exists(path);
        // if (isDirectory)
        // {
        //    path = path + "\\";
        //    fileName = fileName + "\\";
        //    icon = Icons.Folder;
        // }
        return new PathListItem(path);
    }

    public override IListItem[] GetItems()
    {
        return _pathItems.ToArray();
    }
}
