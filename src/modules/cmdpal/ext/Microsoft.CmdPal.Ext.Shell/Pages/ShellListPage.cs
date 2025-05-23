// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.CmdPal.Ext.Shell.Helpers;
using Microsoft.CmdPal.Ext.Shell.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell.Pages;

internal sealed partial class ShellListPage : DynamicListPage
{
    private readonly ShellListPageHelpers _helper;

    private readonly List<ListItem> _exeItems = new();
    private readonly List<ListItem> _topLevelItems = new();
    private readonly List<ListItem> _historyItems = new();
    private List<ListItem> _pathItems = new();

    public ShellListPage(SettingsManager settingsManager, bool addBuiltins = false)
    {
        Icon = Icons.RunV2;
        Id = "com.microsoft.cmdpal.shell";
        Name = Resources.cmd_plugin_name;
        PlaceholderText = Resources.list_placeholder_text;
        _helper = new(settingsManager);

        EmptyContent = new CommandItem()
        {
            Title = Resources.cmd_plugin_name,
            Icon = Icons.RunV2,
            Subtitle = Resources.list_placeholder_text,
        };

        if (addBuiltins)
        {
            var allAppsCommandItem = new ListItem(new NoOpCommand()
            {
                Name = "Open",
                Icon = IconHelpers.FromRelativePath("Assets\\AllApps.svg"),
            })
            {
                Title = "All apps",
                Subtitle = "Search installed apps",
            };
            var calculatorCommandItem = new ListItem(new NoOpCommand()
            {
                Name = "Open",
                Icon = IconHelpers.FromRelativePath("Assets\\Calculator.png"),
            })
            {
                Title = "Calculator",
                Subtitle = "Press = to type an equation",
            };
        }
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        Debug.WriteLine($"Run: update search \"{oldSearch}\" -> \"{newSearch}\"");

        // If the search text is the start of a path to a file (it might be a
        // UNC path), then we want to list all the files that start with that text:

        // 1. Check if the search text is a valid path
        // 2. If it is, then list all the files that start with that text
        var searchText = newSearch.Trim();

        var expanded = Environment.ExpandEnvironmentVariables(searchText);
        Debug.WriteLine($"Run: searchText={searchText} -> expanded={expanded}");
        searchText = expanded;

        // _historyItems = _helper.Query(searchText);
        // _historyItems.ForEach(i =>
        // {
        //    i.Icon = Icons.RunV2;
        //    i.Subtitle = string.Empty;
        // });

        // TODO we can be smarter about only re-reading the filesystem if the
        // new search is just the oldSearch+some chars
        if (string.IsNullOrEmpty(searchText) || string.IsNullOrWhiteSpace(searchText))
        {
            _pathItems.Clear();
            _exeItems.Clear();
            RaiseItemsChanged();
            return;
        }

        ParseExecutableAndArgs(searchText, out var exe, out var args);

        var exeExists = ShellListPageHelpers.FileExistInPath(exe, out var fullExePath);
        var pathIsDir = Directory.Exists(exe);
        Debug.WriteLine($"Run: exeExists={exeExists}, pathIsDir={pathIsDir}");

        _pathItems.Clear();

        // We want to show path items:
        // * If there's no args, AND (the path doesn't exist OR the path is a dir)
        if (string.IsNullOrEmpty(args)
            && (!exeExists || pathIsDir))
        {
            CreatePathItems(exe);
        }

        if (exeExists)
        {
            CreateExeItems(exe, args, fullExePath);
        }
        else
        {
            _exeItems.Clear();
        }

        RaiseItemsChanged();
    }

    private ListItem PathToListItem(string path)
    {
        return new PathListItem(path);
    }

    public override IListItem[] GetItems()
    {
        var filteredTopLevel = ListHelpers.FilterList(_topLevelItems, SearchText);
        return
            _exeItems
            .Concat(filteredTopLevel)
            .Concat(_historyItems)
            .Concat(_pathItems)
            .ToArray();
    }

    private void CreateExeItems(string exe, string args, string fullExePath)
    {
        _exeItems.Clear();

        // var command = new AnonymousCommand(() => { ShellHelpers.OpenInShell(exe, args); }) { Result = CommandResult.Dismiss() };
        var exeItem = new RunExeItem(exe, args, fullExePath);

        var pathItem = PathToListItem(fullExePath);
        exeItem.MoreCommands = [
            .. exeItem.MoreCommands,

            // new CommandContextItem(pathItem.Command!),
            .. pathItem.MoreCommands];

        _exeItems.Add(exeItem);
    }

    private void CreatePathItems(string searchPath)
    {
        var directoryPath = string.Empty;
        var searchPattern = string.Empty;

        // Check if the search text is a valid path
        if (Path.IsPathRooted(searchPath) && Path.GetDirectoryName(searchPath) is string directoryName)
        {
            directoryPath = directoryName;
            searchPattern = $"{Path.GetFileName(searchPath)}*";
        }

        // we should also handle just drive roots, ala c:\ or d:\
        else if (searchPath.Length == 2 && searchPath[1] == ':')
        {
            directoryPath = searchPath + "\\";
            searchPattern = $"*";
        }

        // Check if the search text is a valid UNC path
        else if (searchPath.StartsWith(@"\\", System.StringComparison.CurrentCultureIgnoreCase) &&
                 searchPath.Contains(@"\\"))
        {
            directoryPath = searchPath;
            searchPattern = $"*";
        }

        var dirExists = Directory.Exists(directoryPath);
        Debug.WriteLine($"Run: dirExists({directoryPath})={dirExists}");

        // Check if the directory exists
        if (dirExists)
        {
            // Get all the files in the directory that start with the search text
            var files = Directory.GetFileSystemEntries(directoryPath, searchPattern);

            // Create a list of commands for each file
            var commands = files.Select(PathToListItem).ToList();

            // Add the commands to the list
            _pathItems = commands;
        }
        else
        {
            _pathItems.Clear();
        }
    }

    private static void ParseExecutableAndArgs(string input, out string executable, out string arguments)
    {
        input = input.Trim();
        executable = string.Empty;
        arguments = string.Empty;

        if (string.IsNullOrEmpty(input))
        {
            return;
        }

        if (input.StartsWith("\"", System.StringComparison.InvariantCultureIgnoreCase))
        {
            // Find the closing quote
            var closingQuoteIndex = input.IndexOf('\"', 1);
            if (closingQuoteIndex > 0)
            {
                executable = input.Substring(1, closingQuoteIndex - 1);
                if (closingQuoteIndex + 1 < input.Length)
                {
                    arguments = input.Substring(closingQuoteIndex + 1).TrimStart();
                }
            }
        }
        else
        {
            // Executable ends at first space
            var firstSpaceIndex = input.IndexOf(' ');
            if (firstSpaceIndex > 0)
            {
                executable = string.Concat("\"", input.AsSpan(0, firstSpaceIndex), "\"");
                arguments = input[(firstSpaceIndex + 1)..].TrimStart();
            }
            else
            {
                executable = input;
            }
        }
    }
}
