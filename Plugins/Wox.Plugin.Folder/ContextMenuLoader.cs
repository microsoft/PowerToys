using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Wox.Plugin.Folder
{
    internal class ContextMenuLoader : IContextMenu
    {
        private readonly PluginInitContext _context;

        public ContextMenuLoader(PluginInitContext context)
        {
            _context = context;
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var contextMenus = new List<Result>();
            if (selectedResult.ContextData is SearchResult record)
            {
                string editorPath = "notepad.exe"; // TODO add the ability to create a custom editor

                var name = "Open With Editor: " + Path.GetFileNameWithoutExtension(editorPath);
                contextMenus.Add(new Result
                {
                    Title = name,
                    Action = _ =>
                    {
                        try
                        {
                            Process.Start(editorPath, record.FullPath);
                        }
                        catch
                        {
                            // TODO: update this
                            _context.API.ShowMsg(
                                string.Format(_context.API.GetTranslation("wox_plugin_everything_canot_start"),
                                    record.FullPath), string.Empty, string.Empty);
                            return false;
                        }

                        return true;
                    },
                    IcoPath = editorPath
                });

                var icoPath = (record.Type == ResultType.File) ? "Images\\file.png" : "Images\\folder.png";
                contextMenus.Add(new Result
                {
                    Title = _context.API.GetTranslation("wox_plugin_everything_copy_path"),
                    Action = (context) =>
                    {
                        Clipboard.SetText(record.FullPath);
                        return true;
                    },
                    IcoPath = icoPath
                });

                contextMenus.Add(new Result
                {
                    Title = _context.API.GetTranslation("wox_plugin_everything_copy"),
                    Action = (context) =>
                    {
                        Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection { record.FullPath });
                        return true;
                    },
                    IcoPath = icoPath
                });

                if (record.Type == ResultType.File || record.Type == ResultType.Folder)
                    contextMenus.Add(new Result
                    {
                        Title = _context.API.GetTranslation("wox_plugin_everything_delete"),
                        Action = (context) =>
                        {
                            try
                            {
                                if (record.Type == ResultType.File)
                                    System.IO.File.Delete(record.FullPath);
                                else
                                    System.IO.Directory.Delete(record.FullPath);
                            }
                            catch
                            {
                                _context.API.ShowMsg(string.Format(_context.API.GetTranslation("wox_plugin_everything_canot_delete"), record.FullPath), string.Empty, string.Empty);
                                return false;
                            }

                            return true;
                        },
                        IcoPath = icoPath
                    });

            }

            return contextMenus;
        }
    }

    public class SearchResult
    {
        public string FullPath { get; set; }
        public ResultType Type { get; set; }
    }

    public enum ResultType
    {
        Volume,
        Folder,
        File
    }
}