// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Bookmarks.Properties;
using Microsoft.CmdPal.Ext.Indexer;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Bookmarks;

public partial class BookmarksCommandProvider : CommandProvider
{
    private readonly List<CommandItem> _commands = [];

    private readonly AddBookmarkPage _addNewCommand = new(null);

    private readonly IBookmarkDataSource _dataSource;
    private readonly BookmarkJsonParser _parser;
    private Bookmarks? _bookmarks;

    public BookmarksCommandProvider()
        : this(new FileBookmarkDataSource(StateJsonPath()))
    {
    }

    internal BookmarksCommandProvider(IBookmarkDataSource dataSource)
    {
        _dataSource = dataSource;
        _parser = new BookmarkJsonParser();

        Id = "Bookmarks";
        DisplayName = Resources.bookmarks_display_name;
        Icon = Icons.PinIcon;

        _addNewCommand.AddedCommand += AddNewCommand_AddedCommand;
    }

    private void AddNewCommand_AddedCommand(object sender, BookmarkData args)
    {
        ExtensionHost.LogMessage($"Adding bookmark ({args.Name},{args.Bookmark})");
        _bookmarks?.Data.Add(args);

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
        try
        {
            var jsonData = _parser.SerializeBookmarks(_bookmarks);
            _dataSource.SaveBookmarkData(jsonData);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to save bookmarks: {ex.Message}");
        }

        LoadCommands();
        RaiseItemsChanged(0);
    }

    private void LoadCommands()
    {
        List<CommandItem> collected = [];
        collected.Add(new CommandItem(_addNewCommand));

        if (_bookmarks is null)
        {
            LoadBookmarksFromFile();
        }

        if (_bookmarks is not null)
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
            var jsonData = _dataSource.GetBookmarkData();
            _bookmarks = _parser.ParseBookmarks(jsonData);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message);
        }

        if (_bookmarks is null)
        {
            _bookmarks = new();
        }
    }

    private CommandItem BookmarkToCommandItem(BookmarkData bookmark)
    {
        ICommand command = bookmark.IsPlaceholder ?
            new BookmarkPlaceholderPage(bookmark) :
            new UrlCommand(bookmark);

        var listItem = new CommandItem(command) { Icon = command.Icon };

        List<CommandContextItem> contextMenu = [];

        // Add commands for folder types
        if (command is UrlCommand urlCommand)
        {
            if (!bookmark.IsWebUrl())
            {
                contextMenu.Add(
                    new CommandContextItem(new DirectoryPage(urlCommand.Url)));

                contextMenu.Add(
                    new CommandContextItem(new OpenInTerminalCommand(urlCommand.Url)));
            }
        }

        listItem.Title = bookmark.Name;
        listItem.Subtitle = bookmark.Bookmark;

        var edit = new AddBookmarkPage(bookmark) { Icon = Icons.EditIcon };
        edit.AddedCommand += Edit_AddedCommand;
        contextMenu.Add(new CommandContextItem(edit));

        var delete = new CommandContextItem(
            title: Resources.bookmarks_delete_title,
            name: Resources.bookmarks_delete_name,
            action: () =>
            {
                if (_bookmarks is not null)
                {
                    ExtensionHost.LogMessage($"Deleting bookmark ({bookmark.Name},{bookmark.Bookmark})");

                    _bookmarks.Data.Remove(bookmark);

                    SaveAndUpdateCommands();
                }
            },
            result: CommandResult.KeepOpen())
        {
            IsCritical = true,
            Icon = Icons.DeleteIcon,
        };
        contextMenu.Add(delete);

        listItem.MoreCommands = contextMenu.ToArray();

        return listItem;
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
