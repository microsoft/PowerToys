using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Wox.Infrastructure.Logger;
using Wox.Plugin;
using Microsoft.Plugin.Indexer.SearchHelper;
using System.Windows.Input;
using System.Reflection;
using System.Threading.Tasks;
using Wox.Infrastructure;

namespace Microsoft.Plugin.Indexer
{
    internal class ContextMenuLoader : IContextMenu
    {
        private readonly PluginInitContext _context;

        public enum ResultType
        {
            Folder,
            File
        }

        // Extensions for adding run as admin context menu item for applications
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
                ResultType type = Path.HasExtension(record.Path) ? ResultType.File : ResultType.Folder;

                if (type == ResultType.File)
                {
                    contextMenus.Add(CreateOpenContainingFolderResult(record));
                }

                // Test to check if File can be Run as admin, if yes, we add a 'run as admin' context menu item
                if (CanFileBeRunAsAdmin(record.Path))
                {
                    contextMenus.Add(CreateRunAsAdminContextMenu(record));
                }

                contextMenus.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = _context.API.GetTranslation("Microsoft_plugin_indexer_copy_path"),
                    Glyph = "\xE8C8",
                    FontFamily = "Segoe MDL2 Assets",
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
                            var message = "Fail to set text in clipboard";
                            LogException(message, e);
                            _context.API.ShowMsg(message);
                            return false;
                        }
                    }
                });
                contextMenus.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = _context.API.GetTranslation("Microsoft_plugin_indexer_open_in_console"),
                    Glyph = "\xE756",
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = Key.C,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,

                    Action = (context) =>
                    {
                        try
                        {
                            if (type == ResultType.File)
                            {
                                Helper.OpenInConsole(Path.GetDirectoryName(record.Path));
                            }
                            else
                            {
                                Helper.OpenInConsole(record.Path);
                            }

                            return true;
                        }
                        catch (Exception e)
                        {
                            Log.Exception($"|Microsoft.Plugin.Indexer.ContextMenuLoader.LoadContextMenus| Failed to open {record.Path} in console, {e.Message}", e);
                            return false;
                        }
                    }
                });
            }

            return contextMenus;
        }

        // Function to add the context menu item to run as admin
        private ContextMenuResult CreateRunAsAdminContextMenu(SearchResult record)
        {
            return new ContextMenuResult
            {
                PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                Title = _context.API.GetTranslation("Microsoft_plugin_indexer_run_as_administrator"),
                Glyph = "\xE7EF",
                FontFamily = "Segoe MDL2 Assets",
                AcceleratorKey = Key.Enter,
                AcceleratorModifiers = (ModifierKeys.Control | ModifierKeys.Shift),
                Action = _ =>
                {
                    try
                    {
                        Task.Run(() => Helper.RunAsAdmin(record.Path));
                        return true;
                    }
                    catch (Exception e)
                    {
                        Log.Exception($"|Microsoft.Plugin.Indexer.ContextMenu| Failed to run {record.Path} as admin, {e.Message}", e);
                        return false;
                    }

                }
            };
        }

        // Function to test if the file can be run as admin
        private bool CanFileBeRunAsAdmin(string path)
        {
            string fileExtension = Path.GetExtension(path);
            foreach (string extension in appExtensions)
            {
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
                Title = _context.API.GetTranslation("Microsoft_plugin_indexer_open_containing_folder"),
                Glyph = "\xE838",
                FontFamily = "Segoe MDL2 Assets",
                AcceleratorKey = Key.E,
                AcceleratorModifiers = (ModifierKeys.Control | ModifierKeys.Shift),
                Action = _ =>
                {
                    try
                    {
                        Process.Start("explorer.exe", $" /select,\"{record.Path}\"");
                    }
                    catch (Exception e)
                    {
                        var message = $"Fail to open file at {record.Path}";
                        LogException(message, e);
                        _context.API.ShowMsg(message);
                        return false;
                    }

                    return true;
                },
            };
        }

        public void LogException(string message, Exception e)
        {
            Log.Exception($"|Microsoft.Plugin.Folder.ContextMenu|{message}", e);
        }
    }

}