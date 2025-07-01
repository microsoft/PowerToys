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

    private readonly List<ListItem> _exeItems = [];
    private readonly List<ListItem> _topLevelItems = [];
    private readonly List<ListItem> _historyItems = [];
    private List<ListItem> _pathItems = [];
    private ListItem? _uriItem;

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
            // here, we _could_ add built-in providers if we wanted. links to apps, calc, etc.
            // That would be a truly run-first experience
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

        ParseExecutableAndArgs(expanded, out var exe, out var args);
        Debug.WriteLine($"Run: expanded={expanded} -> exe,args='{exe}', '{args}'");

        var exeExists = ShellListPageHelpers.FileExistInPath(exe, out var fullExePath);
        var pathIsDir = Directory.Exists(expanded);
        Debug.WriteLine($"Run: exeExists={exeExists}, pathIsDir={pathIsDir}");

        _pathItems.Clear();

        // We want to show path items:
        // * If there's no args, AND (the path doesn't exist OR the path is a dir)
        if (string.IsNullOrEmpty(args)
            && (!exeExists || pathIsDir))
        {
            CreatePathItems(expanded, searchText);
        }

        if (exeExists)
        {
            CreateAndAddExeItems(exe, args, fullExePath);
        }
        else
        {
            _exeItems.Clear();
        }

        // Only create the URI item if we didn't make a file or exe item for it.
        if (!exeExists && !pathIsDir)
        {
            CreateUriItems(searchText);
        }
        else
        {
            _uriItem = null;
        }

        RaiseItemsChanged();
    }

    private static ListItem PathToListItem(string path, string originalPath)
    {
        return new PathListItem(path, originalPath);
    }

    public override IListItem[] GetItems()
    {
        var filteredTopLevel = ListHelpers.FilterList(_topLevelItems, SearchText);
        List<ListItem> uriItems = _uriItem != null ? [_uriItem] : [];
        return
            _exeItems
            .Concat(filteredTopLevel)
            .Concat(_historyItems)
            .Concat(_pathItems)
            .Concat(uriItems)
            .ToArray();
    }

    internal static RunExeItem CreateExeItems(string exe, string args, string fullExePath)
    {
        var exeItem = new RunExeItem(exe, args, fullExePath);

        var pathItem = PathToListItem(fullExePath, exe);
        exeItem.MoreCommands = [
            .. exeItem.MoreCommands,
            .. pathItem.MoreCommands];

        return exeItem;
    }

    private void CreateAndAddExeItems(string exe, string args, string fullExePath)
    {
        _exeItems.Clear();

        var exeItem = CreateExeItems(exe, args, fullExePath);

        _exeItems.Add(exeItem);
    }

    private void CreatePathItems(string searchPath, string originalPath)
    {
        var directoryPath = string.Empty;
        var searchPattern = string.Empty;

        var startsWithQuote = searchPath.Length > 0 && searchPath[0] == '"';
        var endsWithQuote = searchPath.Last() == '"';
        var trimmed = (startsWithQuote && endsWithQuote) ? searchPath.Substring(1, searchPath.Length - 2) : searchPath;
        var isDriveRoot = trimmed.Length == 2 && trimmed[1] == ':';

        // we should also handle just drive roots, ala c:\ or d:\
        // we need to handle this case first, because "C:" does exist, but we need to append the "\" in that case
        if (isDriveRoot)
        {
            directoryPath = trimmed + "\\";
            searchPattern = $"*";
        }

        // Easiest case: text is literally already a full directory
        else if (Directory.Exists(trimmed))
        {
            directoryPath = trimmed;
            searchPattern = $"*";
        }

        // Check if the search text is a valid path
        else if (Path.IsPathRooted(trimmed) && Path.GetDirectoryName(trimmed) is string directoryName)
        {
            directoryPath = directoryName;
            searchPattern = $"{Path.GetFileName(trimmed)}*";
        }

        // Check if the search text is a valid UNC path
        else if (trimmed.StartsWith(@"\\", System.StringComparison.CurrentCultureIgnoreCase) &&
                 trimmed.Contains(@"\\"))
        {
            directoryPath = trimmed;
            searchPattern = $"*";
        }

        var dirExists = Directory.Exists(directoryPath);
        Debug.WriteLine($"Run: dirExists({directoryPath})={dirExists}");

        // searchPath is fully expanded, and originalPath is not. We might get:
        // * original: X%Y%Z\partial
        // * search: X_foo_Z\partial
        // and we want the result `X_foo_Z\partialOne` to use the suggestion `X%Y%Z\partialOne`
        //
        // To do this:
        // * Get the directoryPath
        // * trim that out of the beginning of searchPath -> searchPathTrailer
        // * everything left from searchPath? remove searchPathTrailer from the end of originalPath
        // that gets us the expanded original dir

        // Check if the directory exists
        if (dirExists)
        {
            // Get all the files in the directory that start with the search text
            var files = Directory.GetFileSystemEntries(directoryPath, searchPattern);

            var searchPathTrailer = trimmed.Remove(0, Math.Min(directoryPath.Length, trimmed.Length));
            var originalBeginning = originalPath.Remove(originalPath.Length - searchPathTrailer.Length);
            if (isDriveRoot)
            {
                originalBeginning = string.Concat(originalBeginning, '\\');
            }

            Debug.WriteLine($"  '{trimmed}'\n->'{searchPathTrailer.PadLeft(directoryPath.Length)}'\n->'{originalBeginning}'");

            // Create a list of commands for each file
            var commands = files.Select(f => PathToListItem(f, originalBeginning)).ToList();

            // Add the commands to the list
            _pathItems = commands;
        }
        else
        {
            _pathItems.Clear();
        }
    }

    internal static void ParseExecutableAndArgs(string input, out string executable, out string arguments)
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
                executable = input.Substring(0, firstSpaceIndex);
                arguments = input[(firstSpaceIndex + 1)..].TrimStart();
            }
            else
            {
                executable = input;
            }
        }
    }

    internal void CreateUriItems(string searchText)
    {
        if (!System.Uri.TryCreate(searchText, UriKind.Absolute, out var uri))
        {
            _uriItem = null;
            return;
        }

        var command = new OpenUrlCommand(searchText) { Result = CommandResult.Dismiss() };
        _uriItem = new ListItem(command)
        {
            Title = searchText,
        };
    }
}
