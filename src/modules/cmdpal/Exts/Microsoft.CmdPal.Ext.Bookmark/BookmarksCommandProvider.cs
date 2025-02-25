// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Bookmarks;

public partial class BookmarksCommandProvider : CommandProvider
{
    private readonly List<CommandItem> _commands = [];

    private readonly AddBookmarkPage _addNewCommand = new();

    private Bookmarks? _bookmarks;

    public static IconInfo DeleteIcon { get; private set; } = new("\uE74D"); // Delete

    public static IconInfo EditIcon { get; private set; } = new("\uE70F"); // Edit

    public BookmarksCommandProvider()
    {
        Id = "Bookmarks";
        DisplayName = "Bookmarks";
        Icon = new IconInfo("\uE718"); // Pin

        _addNewCommand.AddedCommand += AddNewCommand_AddedCommand;
    }

    private void AddNewCommand_AddedCommand(object sender, object? args)
    {
        _commands.Clear();
        LoadCommands();
        RaiseItemsChanged(0);
    }

    private void LoadCommands()
    {
        List<CommandItem> collected = [];
        collected.Add(new CommandItem(_addNewCommand));

        LoadBookmarksFromFile();

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
            Console.WriteLine($"Error loading commands: {ex.Message}");
        }
    }

    private CommandItem BookmarkToCommandItem(BookmarkData bookmark)
    {
        ICommand command = bookmark.IsPlaceholder ?
            new BookmarkPlaceholderPage(bookmark) :
            new UrlCommand(bookmark);

        var listItem = new CommandItem(command);

        List<CommandContextItem> contextMenu = [];
        var edit = new AddBookmarkPage(bookmark.Name, bookmark.Bookmark) { Icon = EditIcon };
        edit.AddedCommand += AddNewCommand_AddedCommand;
        contextMenu.Add(new CommandContextItem(edit));

        var delete = new CommandContextItem(
            title: "Delete bookmark",
            name: "Delete",
            action: () =>
            {
                if (_bookmarks != null)
                {
                    _bookmarks.Data.Remove(bookmark);
                    var jsonPath = BookmarksCommandProvider.StateJsonPath();
                    Bookmarks.WriteToFile(jsonPath, _bookmarks);
                    _commands.Clear();
                    LoadCommands();
                    RaiseItemsChanged(0);
                }
            },
            result: CommandResult.KeepOpen())
        {
            IsCritical = true,
            Icon = DeleteIcon,
        };
        contextMenu.Add(delete);

        // Add commands for folder types
        if (command is UrlCommand urlCommand)
        {
            if (urlCommand.Type == "folder")
            {
                contextMenu.Add(
                    new CommandContextItem(new OpenInTerminalCommand(urlCommand.Url)));
            }

            listItem.Subtitle = urlCommand.Url;
        }

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
