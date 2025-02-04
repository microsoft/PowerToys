// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.Bookmarks;

public partial class BookmarksCommandProvider : CommandProvider
{
    private readonly List<ICommand> _commands = [];
    private readonly AddBookmarkPage _addNewCommand = new();

    public BookmarksCommandProvider()
    {
        Id = "Bookmarks";
        DisplayName = "Bookmarks";
        Icon = new("\uE718"); // Pin

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
        List<ICommand> collected = [];
        collected.Add(_addNewCommand);

        try
        {
            var jsonFile = StateJsonPath();
            if (File.Exists(jsonFile))
            {
                var data = Bookmarks.ReadFromFile(jsonFile);

                if (data != null)
                {
                    var items = data?.Data;

                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            var nameToken = item.Name;
                            var urlToken = item.Bookmark;
                            var typeToken = item.Type;

                            if (nameToken == null || urlToken == null || typeToken == null)
                            {
                                continue;
                            }

                            var name = nameToken.ToString();
                            var url = urlToken.ToString();
                            var type = typeToken.ToString();

                            collected.Add((url.Contains('{') && url.Contains('}')) ?
                                new BookmarkPlaceholderPage(name, url, type) :
                                new UrlCommand(name, url, type));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // debug log error
            Console.WriteLine($"Error loading commands: {ex.Message}");
        }

        _commands.Clear();
        _commands.AddRange(collected);
    }

    public override ICommandItem[] TopLevelCommands()
    {
        if (_commands.Count == 0)
        {
            LoadCommands();
        }

        return _commands.Select(command =>
        {
            var listItem = new CommandItem(command);

            // Add commands for folder types
            if (command is UrlCommand urlCommand)
            {
                if (urlCommand.Type == "folder")
                {
                    listItem.MoreCommands = [
                        new CommandContextItem(new OpenInTerminalCommand(urlCommand.Url))
                    ];
                }

                listItem.Subtitle = urlCommand.Url;
            }

            return listItem;
        }).ToArray();
    }

    internal static string StateJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        // now, the state is just next to the exe
        return System.IO.Path.Combine(directory, "bookmarks.json");
    }
}
