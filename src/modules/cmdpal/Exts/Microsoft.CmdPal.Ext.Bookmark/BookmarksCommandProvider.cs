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
        DisplayName = "Bookmarks";

        _addNewCommand.AddedAction += AddNewCommand_AddedAction;
    }

    private void AddNewCommand_AddedAction(object sender, object? args)
    {
        _addNewCommand.AddedAction += AddNewCommand_AddedAction;
        _commands.Clear();
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

                            collected.Add((url.Contains('{') && url.Contains('}')) ? new BookmarkPlaceholderPage(name, url, type) : new UrlAction(name, url, type));
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

    public override IListItem[] TopLevelCommands()
    {
        if (_commands.Count == 0)
        {
            LoadCommands();
        }

        return _commands.Select(action =>
        {
            var listItem = new ListItem(action);

            // Add actions for folder types
            if (action is UrlAction urlAction)
            {
                if (urlAction.Type == "folder")
                {
                    listItem.MoreCommands = [
                        new CommandContextItem(new OpenInTerminalAction(urlAction.Url))
                    ];
                }

                listItem.Subtitle = urlAction.Url;
            }

            if (action is not AddBookmarkPage)
            {
                listItem.Tags = [
                    new Tag()
                    {
                        Text = "Bookmark",

                        // Icon = new("🔗"),
                        // Color=Windows.UI.Color.FromArgb(255, 255, 0, 255)
                    },

                    // new Tag() {
                    //    Text = "A test",
                    //    //Icon = new("🔗"),
                    //    Color=Windows.UI.Color.FromArgb(255, 255, 0, 0)
                    // }
                ];
            }

            return listItem;
        }).ToArray();
    }

    internal static string StateJsonPath()
    {
        // Get the path to our exe
        var path = System.Reflection.Assembly.GetExecutingAssembly().Location;

        // Get the directory of the exe
        var directory = System.IO.Path.GetDirectoryName(path) ?? string.Empty;

        // now, the state is just next to the exe
        return System.IO.Path.Combine(directory, "state.json");
    }
}
