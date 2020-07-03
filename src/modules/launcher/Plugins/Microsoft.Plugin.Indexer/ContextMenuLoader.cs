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
using Wox.Infrastructure;

namespace Microsoft.Plugin.Indexer
{
    internal class ContextMenuLoader : IContextMenu
    {
        private readonly IPublicAPI _API;

        public enum ResultType
        {
            Folder,
            File
        }

        public ContextMenuLoader(IPublicAPI API)
        {
            _API = API;
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

                var fileOrFolder = (type == ResultType.File) ? "file" : "folder";
                contextMenus.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = _API.GetTranslation("Microsoft_plugin_indexer_copy_path"),
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
                            _API.ShowMsg(message);
                            return false;
                        }
                    }
                });
                contextMenus.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = _API.GetTranslation("Microsoft_plugin_indexer_open_in_console"),
                    Glyph = "\xE756",
                    FontFamily = "Segoe MDL2 Assets",
                    SubTitle = $"Open current {fileOrFolder} path in console",
                    AcceleratorKey = Key.C,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,

                    Action = (context) =>
                    {
                        try
                        {
                            if (fileOrFolder == "file")
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
                            Log.Exception(e.Message, e);
                            return false;
                        }
                    }
                });
            }

            return contextMenus;
        }

        private ContextMenuResult CreateOpenContainingFolderResult(SearchResult record)
        {
            return new ContextMenuResult
            {
                PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                Title = _API.GetTranslation("Microsoft_plugin_indexer_open_containing_folder"),
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
                        _API.ShowMsg(message);
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