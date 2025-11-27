// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Common.Search.FuzzSearch;
using Common.UI;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Pages;

namespace PowerToysExtension.Helpers;

/// <summary>
/// Builds the list of PowerToys module entries and supports basic fuzzy filtering.
/// </summary>
internal static class ModuleItemsHelper
{
    private static List<ListItem>? _cache;

    public static IListItem[] FilteredItems(string query)
    {
        var all = AllItems();
        if (string.IsNullOrWhiteSpace(query))
        {
            return [.. all];
        }

        var matched = new List<Tuple<int, ListItem>>();
        foreach (var item in all)
        {
            var result = StringMatcher.FuzzyMatch(query, item.Title);
            if (result.Success)
            {
                matched.Add(new Tuple<int, ListItem>(result.Score, item));
            }
        }

        matched.Sort((x, y) => y.Item1.CompareTo(x.Item1));
        return [.. matched.Select(x => x.Item2)];
    }

    private static List<ListItem> AllItems()
    {
        if (_cache is not null)
        {
            return _cache;
        }

        var items = new List<ListItem>();
        foreach (var module in Enum.GetValues<SettingsDeepLink.SettingsWindow>())
        {
            var item = CreateItem(module);
            if (item is not null)
            {
                items.Add(item);
            }
        }

        _cache = items;
        return items;
    }

    private static ListItem? CreateItem(SettingsDeepLink.SettingsWindow module)
    {
        // Skip purely internal pages.
        if (module is SettingsDeepLink.SettingsWindow.Dashboard)
        {
            return null;
        }

        var icon = module.ModuleIcon();
        var title = module.ModuleDisplayName();

        var settingsCommand = new OpenInSettingsCommand(module, title);

        var more = new List<ICommandContextItem>();

        switch (module)
        {
            case SettingsDeepLink.SettingsWindow.Awake:
                more.Add(new CommandContextItem(new StartAwakeCommand("Awake: Keep awake indefinitely", () => "-m indefinite", "Awake set to indefinite")));
                more.Add(new CommandContextItem(new StartAwakeCommand("Awake: Keep awake for 30 minutes", () => "-m timed -t 30", "Awake set for 30 minutes")));
                more.Add(new CommandContextItem(new StartAwakeCommand("Awake: Keep awake for 2 hours", () => "-m timed -t 120", "Awake set for 2 hours")));
                more.Add(new CommandContextItem(new StopAwakeCommand()));
                break;

            case SettingsDeepLink.SettingsWindow.Workspaces:
                more.Add(new CommandContextItem(new WorkspacesListPage()));
                more.Add(new CommandContextItem(new OpenWorkspaceEditorCommand()));
                break;

            case SettingsDeepLink.SettingsWindow.ColorPicker:
                more.Add(new CommandContextItem(new CopyColorCommand()));
                break;

            default:
                break;
        }

        var command = new CommandItem(settingsCommand)
        {
            Title = title,
            Icon = icon,
            MoreCommands = more.Count > 0 ? [.. more] : [],
        };

        return new ListItem(command);
    }
}
