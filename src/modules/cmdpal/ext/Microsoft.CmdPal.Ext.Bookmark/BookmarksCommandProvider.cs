// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Models;
using Microsoft.CmdPal.Ext.Bookmarks.Properties;
using Microsoft.CmdPal.Ext.Indexer;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Bookmarks;

public partial class BookmarksCommandProvider : CommandProvider
{
    private readonly List<CommandItem> _commands = [];

    private readonly AddBookmarkPage _addNewCommand = new(null);

    private Bookmarks? _bookmarks;

    public BookmarksCommandProvider()
    {
        Id = "Bookmarks";
        DisplayName = Resources.bookmarks_display_name;
        Icon = new IconInfo("\uE718"); // Pin

        _addNewCommand.AddedCommand += AddNewCommand_AddedCommand;
    }

    private void AddNewCommand_AddedCommand(object sender, BookmarkData args)
    {
        ExtensionHost.LogMessage($"Adding bookmark ({args.Name},{args.Bookmark})");
        if (_bookmarks != null)
        {
            _bookmarks.Data.Add(args);
        }

        SaveAndUpdateCommands();
    }

    // In the edit path, `args` was already in _bookmarks, we just updated it
    private void Edit_AddedCommand(object sender, BookmarkData args)
    {
        ExtensionHost.LogMessage($"Edited bookmark ({args.Name},{args.Bookmark})");

        SaveAndUpdateCommands();
    }

    private void SaveAndUpdateCommands()
    {
        if (_bookmarks != null)
        {
            var jsonPath = BookmarksCommandProvider.StateJsonPath();
            Bookmarks.WriteToFile(jsonPath, _bookmarks);
        }

        LoadCommands();
        RaiseItemsChanged(0);
    }

    private void LoadCommands()
    {
        List<CommandItem> collected = [];
        collected.Add(new CommandItem(_addNewCommand));

        if (_bookmarks == null)
        {
            LoadBookmarksFromFile();
        }

        if (_bookmarks != null)
        {
            collected.AddRange(_bookmarks.Data.Select(BookmarkToCommandItem));
        }

        _commands.Clear();
        _commands.AddRange(collected);
    }

    private void LoadBookmarksFromFile()
    {
        try
        {
            var jsonFile = StateJsonPath();
            if (File.Exists(jsonFile))
            {
                _bookmarks = Bookmarks.ReadFromFile(jsonFile);
            }
        }
        catch (Exception ex)
        {
            // debug log error
            Debug.WriteLine($"Error loading commands: {ex.Message}");
        }

        if (_bookmarks == null)
        {
            _bookmarks = new();
        }
    }

    private CommandItem BookmarkToCommandItem(BookmarkData bookmark)
    {
        var deleteAction = () =>
        {
            if (_bookmarks != null)
            {
                ExtensionHost.LogMessage($"Deleting bookmark ({bookmark.Name},{bookmark.Bookmark})");

                _bookmarks.Data.Remove(bookmark);

                SaveAndUpdateCommands();
            }
        };

        if (CommandItemFactory.TryCreateBookmarkCommand(bookmark, Edit_AddedCommand, deleteAction, out var commandItem))
        {
            return commandItem;
        }

        ExtensionHost.LogMessage($"Failed to create command for bookmark ({bookmark.Name},{bookmark.Bookmark})");
        return new ListItem(new NoOpCommand());
    }

    public override ICommandItem[] TopLevelCommands()
    {
        if (_commands.Count == 0)
        {
            LoadCommands();
        }

        return _commands.ToArray();
    }

    internal static string StateJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        // now, the state is just next to the exe
        return System.IO.Path.Combine(directory, "bookmarks.json");
    }
}
