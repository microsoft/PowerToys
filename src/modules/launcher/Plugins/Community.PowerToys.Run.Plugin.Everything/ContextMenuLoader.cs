using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Community.PowerToys.Run.Plugin.Everything.SearchHelper;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.Everything
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

        // Extensions for adding run as admin context menu item for applications
        private readonly string[] appExtensions = { ".exe", ".bat", ".appref-ms", ".lnk" };

        public ContextMenuLoader(PluginInitContext context)
        {
            _context = context;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "We want to keep the process alive, and instead log and show an error message")]
        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            var contextMenus = new List<ContextMenuResult>();
            if (selectedResult.ContextData is SearchResult record)
            {
                ResultType type = _path.HasExtension(record.FullPath) ? ResultType.File : ResultType.Folder;

                if (type == ResultType.File)
                {
                    contextMenus.Add(CreateOpenContainingFolderResult(record));
                }

                // Test to check if File can be Run as admin, if yes, we add a 'run as admin' context menu item
                if (CanFileBeRunAsAdmin(record.FullPath))
                {
                    contextMenus.Add(CreateRunAsAdminContextMenu(record));
                }

                contextMenus.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = Properties.Resources.Community_plugin_everything_copy_path,
                    Glyph = "\xE8C8",
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = Key.C,
                    AcceleratorModifiers = ModifierKeys.Control,

                    Action = (context) =>
                    {
                        try
                        {
                            Clipboard.SetText(record.FullPath);
                            return true;
                        }
                        catch (System.Exception e)
                        {
                            var message = Properties.Resources.Community_plugin_everything_clipboard_failed;
                            Log.Exception(message, e, GetType());

                            _context.API.ShowMsg(message);
                            return false;
                        }
                    },
                });
                contextMenus.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = Properties.Resources.Community_plugin_everything_open_in_console,
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
                                Helper.OpenInConsole(_path.GetDirectoryName(record.FullPath));
                            }
                            else
                            {
                                Helper.OpenInConsole(record.FullPath);
                            }

                            return true;
                        }
                        catch (System.Exception e)
                        {
                            Log.Exception($"Failed to open {record.FullPath} in console, {e.Message}", e, GetType());
                            return false;
                        }
                    },
                });
            }

            return contextMenus;
        }

        // Function to add the context menu item to run as admin
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "We want to keep the process alive, and instead log the exception message")]
        private static ContextMenuResult CreateRunAsAdminContextMenu(SearchResult record)
        {
            return new ContextMenuResult
            {
                PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                Title = Properties.Resources.Community_plugin_everything_run_as_administrator,
                Glyph = "\xE7EF",
                FontFamily = "Segoe MDL2 Assets",
                AcceleratorKey = Key.Enter,
                AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                Action = _ =>
                {
                    try
                    {
                        Task.Run(() => Helper.RunAsAdmin(record.FullPath));
                        return true;
                    }
                    catch (System.Exception e)
                    {
                        Log.Exception($"Failed to run {record.FullPath} as admin, {e.Message}", e, MethodBase.GetCurrentMethod().DeclaringType);
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "We want to keep the process alive, and instead log and show an error message")]
        private ContextMenuResult CreateOpenContainingFolderResult(SearchResult record)
        {
            return new ContextMenuResult
            {
                PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                Title = Properties.Resources.Community_plugin_everything_open_containing_folder,
                Glyph = "\xE838",
                FontFamily = "Segoe MDL2 Assets",
                AcceleratorKey = Key.E,
                AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                Action = _ =>
                {
                    if (!Helper.OpenInShell("explorer.exe", $"/select,\"{record.FullPath}\""))
                    {
                        var message = $"{Properties.Resources.Community_plugin_everything_folder_open_failed} {record.FullPath}";
                        _context.API.ShowMsg(message);
                        return false;
                    }

                    return true;
                },
            };
        }
    }
}
