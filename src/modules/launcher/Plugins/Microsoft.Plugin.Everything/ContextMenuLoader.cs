
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Wox.Infrastructure.Logger;
using Wox.Plugin;
using Wox.Plugin.Everything;
using Wox.Plugin.Everything.Everything;

namespace Microsoft.Plugin.Everything
{
    internal class ContextMenuLoader : IContextMenu
    {
        private readonly PluginInitContext _context;

        public ContextMenuLoader(PluginInitContext context)
        {
            _context = context;
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            var contextMenus = new List<ContextMenuResult>();
            if (selectedResult.ContextData is SearchResult record)
            {
                if (record.Type == ResultType.File)
                {
                    contextMenus.Add(CreateOpenContainingFolderResult(record));
                }

                contextMenus.Add(CreateCopyPathResult(record));
            }

            return contextMenus;
        }

        private ContextMenuResult CreateCopyPathResult(SearchResult record)
        {
            return new ContextMenuResult
            {
                PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                Title = "Copy full path",
                Glyph = "\xF0E3",
                FontFamily = "Segoe MDL2 Assets",
                AcceleratorKey = Key.P,
                AcceleratorModifiers = (ModifierKeys.Control | ModifierKeys.Shift),
                Action = (context) =>
                {
                    Clipboard.SetText(record.FullPath);
                    return true;
                },
            };
        }


        private ContextMenuResult CreateOpenContainingFolderResult(SearchResult record)
        {
            return new ContextMenuResult
            {
                PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                Title = "Open containing folder",
                Glyph = "\xE838",
                FontFamily = "Segoe MDL2 Assets",
                AcceleratorKey = Key.E,
                AcceleratorModifiers = (ModifierKeys.Control | ModifierKeys.Shift),
                Action = _ =>
                {
                    try
                    {
                        Process.Start("explorer.exe", $" /select,\"{record.FullPath}\"");
                    }
                    catch (Exception e)
                    {
                        var message = $"Fail to open file at {record.FullPath}";
                        LogException(message, e);
                        _context.API.ShowMsg(message);
                        return false;
                    }

                    return true;
                }
            };
        }

        public void LogException(string message, Exception e)
        {
            Log.Exception($"|Microsoft.Plugin.Everything.ContextMenu|{message}", e);
        }
    }
}
