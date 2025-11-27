// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Common.Search.FuzzSearch;
using Microsoft.CmdPal.Ext.PowerToys.Classes;
using Microsoft.CmdPal.Ext.PowerToys.Commands;
using Microsoft.CmdPal.Ext.PowerToys.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using static Common.UI.SettingsDeepLink;

namespace Microsoft.CmdPal.Ext.PowerToys.Helper;

// A helper class for module items.
internal static class ModuleItemsHelper
{
    private static List<ListItem>? _allItemsCache;

    public static ListItem? CreateCommandItem(this SettingsWindow module)
    {
        switch (module)
        {
            // These modules have their own UI
            case SettingsWindow.Workspaces:
                {
                    var entry = new PowerToysModuleEntry
                    {
                        Module = module,
                    };
                    var primary = new CommandItem(new LaunchCommand(entry))
                    {
                        Title = module.ModuleName(),
                        Icon = module.ModuleIcon(),
                    };

                    var moreCommands = new List<ICommandContextItem>
                    {
                        .. entry.GetCommands(),
                        new CommandContextItem(new OpenInSettingsCommand(entry)),
                    };

                    return new ListItem(primary)
                    {
                        Icon = module.ModuleIcon(),
                        Title = module.ModuleName(),
                        MoreCommands = moreCommands.ToArray(),
                    };
                }

            case SettingsWindow.CropAndLock:
                {
                    var entry = new PowerToysModuleEntry
                    {
                        Module = module,
                    };
                    var primary = new CommandItem(new LaunchCommand(entry))
                    {
                        Title = module.ModuleName(),
                        Icon = module.ModuleIcon(),
                    };

                    var moreCommands = new List<ICommandContextItem>
                    {
                        .. entry.GetCommands(),
                        new CommandContextItem(new OpenInSettingsCommand(entry)),
                    };

                    return new ListItem(primary)
                    {
                        Icon = module.ModuleIcon(),
                        Title = module.ModuleName(),
                        MoreCommands = moreCommands.ToArray(),
                    };
                }

            case SettingsWindow.FancyZones:
            case SettingsWindow.KBM:
            case SettingsWindow.Hosts:
            case SettingsWindow.EnvironmentVariables:
            case SettingsWindow.RegistryPreview:
            case SettingsWindow.ShortcutGuide:
            case SettingsWindow.Awake:
            case SettingsWindow.ColorPicker:
            case SettingsWindow.Run:
            case SettingsWindow.ImageResizer:
            case SettingsWindow.MouseUtils:
            case SettingsWindow.PowerRename:
            case SettingsWindow.FileExplorer:
            case SettingsWindow.MeasureTool:
            case SettingsWindow.PowerOCR:
            case SettingsWindow.AdvancedPaste:
            case SettingsWindow.CmdPal:
            case SettingsWindow.ZoomIt:
            case SettingsWindow.AlwaysOnTop:
            case SettingsWindow.FileLocksmith:
            case SettingsWindow.NewPlus:
            case SettingsWindow.Peek:
            case SettingsWindow.CmdNotFound:
            case SettingsWindow.MouseWithoutBorders:
            case SettingsWindow.PowerAccent:
            case SettingsWindow.PowerLauncher:
                {
                    var entry = new PowerToysModuleEntry
                    {
                        Module = module,
                    };
                    var primary = new CommandItem(new LaunchCommand(entry))
                    {
                        Title = module.ModuleName(),
                        Icon = module.ModuleIcon(),
                    };

                    var moreCommands = new List<ICommandContextItem>
                    {
                        .. entry.GetCommands(),
                        new CommandContextItem(new OpenInSettingsCommand(entry)),
                    };

                    return new ListItem(primary)
                    {
                        Icon = module.ModuleIcon(),
                        Title = module.ModuleName(),
                        MoreCommands = moreCommands.ToArray(),
                    };
                }

            case SettingsWindow.Overview:
            case SettingsWindow.Dashboard:
            // duplicated with file explorer add on.
            case SettingsWindow.PowerPreview:
                return null;
            default:
                throw new NotImplementedException();
        }
    }

    public static List<ListItem> AllItems()
    {
        if (_allItemsCache is not null)
        {
            return _allItemsCache;
        }

        var items = new List<ListItem>();
        foreach (var module in Enum.GetValues<SettingsWindow>())
        {
            var item = module.CreateCommandItem();
            if (item != null)
            {
                items.Add(item);
            }
        }

        _allItemsCache = items;
        return items;
    }

    public static IListItem[] FilteredItems(string query)
    {
        var allItems = AllItems();

        if (string.IsNullOrWhiteSpace(query))
        {
            return [.. allItems];
        }

        var matched = new List<Tuple<int, ListItem>>();

        foreach (var item in allItems)
        {
            var matchResult = StringMatcher.FuzzyMatch(query, item.Title);
            if (matchResult.Success)
            {
                matched.Add(new Tuple<int, ListItem>(matchResult.Score, item));
            }
        }

        matched.Sort((x, y) => y.Item1.CompareTo(x.Item1));
        return [.. matched.Select(x => x.Item2)];
    }
}
