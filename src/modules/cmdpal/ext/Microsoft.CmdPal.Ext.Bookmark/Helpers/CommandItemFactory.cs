// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Bookmarks.Command;
using Microsoft.CmdPal.Ext.Bookmarks.Models;
using Microsoft.CmdPal.Ext.Bookmarks.Properties;
using Microsoft.CmdPal.Ext.Indexer;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Diagnostics.Utilities;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Bookmarks.Helpers;

public static partial class CommandItemFactory
{
    private static readonly Dictionary<BookmarkType, Func<BookmarkData, TypedEventHandler<object, BookmarkData>, Action, CommandItem>> _factory = new()
    {
        { Models.BookmarkType.Folder, CreateShellCommand },
        { Models.BookmarkType.Web, CreateUrlCommand },
        { Models.BookmarkType.File, CreateShellCommand },
        { Models.BookmarkType.Command, CreateShellCommand },
    };

    public static bool TryCreateBookmarkCommand(BookmarkData data, TypedEventHandler<object, BookmarkData> addBookmarkFunc, Action deleteAction, out CommandItem command)
    {
        if (data.IsPlaceholder)
        {
            command = CreatePlaceholderCommand(data, addBookmarkFunc, deleteAction);
            return true;
        }

        if (_factory.TryGetValue(data.Type, out var factory))
        {
            command = factory(data, addBookmarkFunc, deleteAction);

            return true;
        }

        command = new ListItem(new NoOpCommand());
        return false;
    }

    private static CommandItem CreatePlaceholderCommand(BookmarkData bookmark, TypedEventHandler<object, BookmarkData> addBookmarkFunc, Action deleteAction)
    {
        var command = new BookmarkPlaceholderPage(bookmark);
        var listItem = new CommandItem(command) { Icon = command.Icon };
        List<CommandContextItem> contextMenu = [];

        var edit = new AddBookmarkPage(bookmark) { Icon = IconHelper.EditIcon };
        edit.AddedCommand += addBookmarkFunc;
        contextMenu.Add(new CommandContextItem(edit));
        var delete = new CommandContextItem(
            title: Resources.bookmarks_delete_title,
            name: Resources.bookmarks_delete_name,
            action: deleteAction,
            result: CommandResult.KeepOpen())
        {
            IsCritical = true,
            Icon = IconHelper.DeleteIcon,
        };
        contextMenu.Add(delete);
        listItem.MoreCommands = contextMenu.ToArray();
        return listItem;
    }

    private static CommandItem CreateUrlCommand(BookmarkData bookmark, TypedEventHandler<object, BookmarkData> addBookmarkFunc, Action deleteAction)
    {
        UrlCommand command = new UrlCommand(bookmark);
        var listItem = new CommandItem(command) { Icon = command.Icon };

        List<CommandContextItem> contextMenu = [];

        if (command.Type == BookmarkType.Folder)
        {
            contextMenu.Add(
                new CommandContextItem(new DirectoryPage(command.Url)));

            contextMenu.Add(
                new CommandContextItem(new OpenInTerminalCommand(command.Url)));
        }

        listItem.Subtitle = command.Url;

        var edit = new AddBookmarkPage(bookmark) { Icon = IconHelper.EditIcon };
        edit.AddedCommand += addBookmarkFunc;
        contextMenu.Add(new CommandContextItem(edit));

        var delete = new CommandContextItem(
            title: Resources.bookmarks_delete_title,
            name: Resources.bookmarks_delete_name,
            action: deleteAction,
            result: CommandResult.KeepOpen())
        {
            IsCritical = true,
            Icon = IconHelper.DeleteIcon,
        };
        contextMenu.Add(delete);

        listItem.MoreCommands = contextMenu.ToArray();

        return listItem;
    }

    private static CommandItem CreateShellCommand(BookmarkData bookmark, TypedEventHandler<object, BookmarkData> addBookmarkFunc, Action deleteAction)
    {
        var invokableCommand = new ShellCommand(bookmark);
        var listItem = new CommandItem(invokableCommand) { Icon = invokableCommand.Icon };

        List<CommandContextItem> contextMenu = [];

        listItem.Subtitle = invokableCommand.BookmarkData.Bookmark;

        var edit = new AddBookmarkPage(bookmark) { Icon = IconHelper.EditIcon };
        edit.AddedCommand += addBookmarkFunc;
        contextMenu.Add(new CommandContextItem(edit));

        var delete = new CommandContextItem(
            title: Resources.bookmarks_delete_title,
            name: Resources.bookmarks_delete_name,
            action: deleteAction,
            result: CommandResult.KeepOpen())
        {
            IsCritical = true,
            Icon = IconHelper.DeleteIcon,
        };
        contextMenu.Add(delete);

        listItem.MoreCommands = contextMenu.ToArray();

        return listItem;
    }
}
