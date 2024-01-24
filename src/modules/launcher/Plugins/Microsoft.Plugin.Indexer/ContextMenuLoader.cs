// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Plugin.Indexer.SearchHelper;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.Plugin.Indexer
{
    internal class ContextMenuLoader : IContextMenu
    {
        private readonly IPath _path = new FileSystem().Path;

        private readonly PluginInitContext _context;

        public enum ResultType
        {
            Folder,
            File,
        }

        // Extensions for adding run as admin and run as other user context menu item for applications
        private readonly string[] appExtensions = { ".exe", ".bat", ".appref-ms", ".lnk" };

        public ContextMenuLoader(PluginInitContext context)
        {
            _context = context;
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            var contextMenus = new List<ContextMenuResult>();
            if (selectedResult.ContextData is SearchResult record)
            {
                ResultType type = _path.HasExtension(record.Path) ? ResultType.File : ResultType.Folder;

                if (type == ResultType.File)
                {
                    contextMenus.Add(CreateOpenContainingFolderResult(record));
                }

                // Test to check if File can be Run as admin, if yes, we add a 'run as admin' context menu item
                if (CanFileBeRunAsAdmin(record.Path))
                {
                    contextMenus.Add(CreateRunAsAdminContextMenu(record));
                    contextMenus.Add(CreateRunAsUserContextMenu(record));
                }

                contextMenus.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = Properties.Resources.Microsoft_plugin_indexer_copy_path,
                    Glyph = "\xE8C8",
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    AcceleratorKey = Key.C,
                    AcceleratorModifiers = ModifierKeys.Control,

                    Action = (context) =>
                    {
                        try
                        {
                            Clipboard.SetText(record.Path);
                            return true;
                        }
                        catch (Exception e)
                        {
                            var message = Properties.Resources.Microsoft_plugin_indexer_clipboard_failed;
                            Log.Exception(message, e, GetType());

                            _context.API.ShowMsg(message);
                            return false;
                        }
                    },
                });
                contextMenus.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = Properties.Resources.Microsoft_plugin_indexer_open_in_console,
                    Glyph = "\xE756",
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    AcceleratorKey = Key.C,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,

                    Action = (context) =>
                    {
                        try
                        {
                            if (type == ResultType.File)
                            {
                                Helper.OpenInConsole(_path.GetDirectoryName(record.Path));
                            }
                            else
                            {
                                Helper.OpenInConsole(record.Path);
                            }

                            return true;
                        }
                        catch (Exception e)
                        {
                            Log.Exception($"Failed to open {record.Path} in console, {e.Message}", e, GetType());
                            return false;
                        }
                    },
                });
            }

            return contextMenus;
        }

        // Function to add the context menu item to run as admin
        private static ContextMenuResult CreateRunAsAdminContextMenu(SearchResult record)
        {
            return new ContextMenuResult
            {
                PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                Title = Properties.Resources.Microsoft_plugin_indexer_run_as_administrator,
                Glyph = "\xE7EF",
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                AcceleratorKey = Key.Enter,
                AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                Action = _ =>
                {
                    try
                    {
                        Task.Run(() => Helper.RunAsAdmin(record.Path));
                        return true;
                    }
                    catch (Exception e)
                    {
                        Log.Exception($"Failed to run {record.Path} as admin, {e.Message}", e, MethodBase.GetCurrentMethod().DeclaringType);
                        return false;
                    }
                },
            };
        }

        // Function to add the context menu item to run as admin
        private static ContextMenuResult CreateRunAsUserContextMenu(SearchResult record)
        {
            return new ContextMenuResult
            {
                PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                Title = Properties.Resources.Microsoft_plugin_indexer_run_as_user,
                Glyph = "\xE7EE",
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                AcceleratorKey = Key.U,
                AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                Action = _ =>
                {
                    try
                    {
                        Task.Run(() => Helper.RunAsUser(record.Path));
                        return true;
                    }
                    catch (Exception e)
                    {
                        Log.Exception($"Failed to run {record.Path} as different user, {e.Message}", e, MethodBase.GetCurrentMethod().DeclaringType);
                        return false;
                    }
                },
            };
        }

        // Function to test if the file can be run as admin
        private bool CanFileBeRunAsAdmin(string path)
        {
            string fileExtension = _path.GetExtension(path);
            foreach (string extension in appExtensions)
            {
                // Using OrdinalIgnoreCase since this is internal
                if (extension.Equals(fileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private ContextMenuResult CreateOpenContainingFolderResult(SearchResult record)
        {
            return new ContextMenuResult
            {
                PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                Title = Properties.Resources.Microsoft_plugin_indexer_open_containing_folder,
                Glyph = "\xE838",
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                AcceleratorKey = Key.E,
                AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                Action = _ =>
                {
                    if (!Helper.OpenInShell("explorer.exe", $"/select,\"{record.Path}\""))
                    {
                        var message = $"{Properties.Resources.Microsoft_plugin_indexer_folder_open_failed} {record.Path}";
                        _context.API.ShowMsg(message);
                        return false;
                    }

                    return true;
                },
            };
        }
    }
}
