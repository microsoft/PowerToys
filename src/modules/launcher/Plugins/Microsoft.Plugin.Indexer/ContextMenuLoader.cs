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
using System.Linq;

namespace Microsoft.Plugin.Indexer
{
    public class ContextMenuLoader : IContextMenu
    {
        private readonly IPublicAPI _api;

        public enum ResultType
        {
            Folder,
            File
        }

        // Extensions for applications
        private readonly string[]  appExtensions = { ".exe", ".bat", ".appref-ms", ".lnk" };

        public ContextMenuLoader(IPublicAPI api)
        {
            _api = api;
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
                if(CanFileBeRunAsAdmin(record.Path))
                {
                    contextMenus.Add(CreateRunAsAdminContextMenu(record));
                }

                var fileOrFolder = (type == ResultType.File) ? "file" : "folder";
                contextMenus.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = _api.GetTranslation("Microsoft_plugin_indexer_copy_path"),
                    Glyph = "\xE8C8",
                    FontFamily = "Segoe MDL2 Assets",
                    SubTitle = $"Copy the current {fileOrFolder} path to clipboard",
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
                            _api.ShowMsg(message);
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
                Title = _api.GetTranslation("Microsoft_plugin_indexer_run_as_admin"),
                Glyph = "\xE7EF",
                FontFamily = "Segoe MDL2 Assets",
                AcceleratorKey = Key.Enter,
                AcceleratorModifiers = (ModifierKeys.Control | ModifierKeys.Shift),
                Action = _ =>
                {
                    var info = new ProcessStartInfo
                    {
                        FileName = record.Path,
                        WorkingDirectory = Path.GetDirectoryName(record.Path),
                        Verb = "runas",
                        UseShellExecute = true
                    };

                    Task.Run(() => StartProcess(Process.Start, info));

                    return true;
                }
            };
        }

        // Function to start and process and log errors if the process doesn't start as expected
        private void StartProcess(Func<ProcessStartInfo, Process> runProcess, ProcessStartInfo info)
        {
            try
            {
                runProcess(info);
            }
            catch (Exception)
            {
                var name = "Plugin: Indexer";
                var message = $"Unable to start: {info.FileName}";
                _api.ShowMsg(name, message, string.Empty);
            }
        }

        // Function to test if the file can be run as admin
        private bool CanFileBeRunAsAdmin(string path)
        {
            string fileExtension = Path.GetExtension(path);
            foreach(string extension in appExtensions)
            {
                if(extension.Equals(fileExtension, StringComparison.OrdinalIgnoreCase))
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
                Title = _api.GetTranslation("Microsoft_plugin_indexer_open_containing_folder"),
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
                    catch(Exception e)
                    {
                        var message = $"Fail to open file at {record.Path}";
                        LogException(message, e);
                        _api.ShowMsg(message);
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