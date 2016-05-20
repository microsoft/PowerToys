using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Wox.Infrastructure.Storage;

namespace Wox.Plugin.Folder
{
    public class Main : IPlugin, ISettingProvider, IPluginI18n, ISavable
    {
        private static List<string> driverNames;
        private PluginInitContext context;

        private readonly Settings _settings;
        private readonly PluginJsonStorage<Settings> _storage;

        public Main()
        {
            _storage = new PluginJsonStorage<Settings>();
            _settings = _storage.Load();
        }

        public void Save()
        {
            _storage.Save();
        }

        public Control CreateSettingPanel()
        {
            return new FileSystemSettings(context.API, _settings);
        }

        public void Init(PluginInitContext context)
        {
            this.context = context;
            InitialDriverList();
            if (_settings.FolderLinks == null)
            {
                _settings.FolderLinks = new List<FolderLink>();
            }
        }

        public List<Result> Query(Query query)
        {
            string search = query.Search.ToLower();

            List<FolderLink> userFolderLinks = _settings.FolderLinks.Where(
                x => x.Nickname.StartsWith(search, StringComparison.OrdinalIgnoreCase)).ToList();
            List<Result> results =
                userFolderLinks.Select(
                    item => new Result()
                    {
                        Title = item.Nickname,
                        IcoPath = item.Path,
                        SubTitle = "Ctrl + Enter to open the directory",
                        Action = c =>
                        {
                            if (c.SpecialKeyState.CtrlPressed)
                            {
                                try
                                {
                                    Process.Start(item.Path);
                                    return true;
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show(ex.Message, "Could not start " + item.Path);
                                    return false;
                                }
                            }
                            context.API.ChangeQuery($"{query.ActionKeyword} {item.Path}{(item.Path.EndsWith("\\") ? "" : "\\")}");
                            return false;
                        },
                        ContextData = item,
                    }).ToList();

            if (driverNames != null && !driverNames.Any(search.StartsWith))
                return results;

            //if (!input.EndsWith("\\"))
            //{
            //    //"c:" means "the current directory on the C drive" whereas @"c:\" means "root of the C drive"
            //    input = input + "\\";
            //}
            results.AddRange(QueryInternal_Directory_Exists(query));

            // todo temp hack for scores
            foreach (var result in results)
            {
                result.Score += 10;
            }

            return results;
        }
        private void InitialDriverList()
        {
            if (driverNames == null)
            {
                driverNames = new List<string>();
                DriveInfo[] allDrives = DriveInfo.GetDrives();
                foreach (DriveInfo driver in allDrives)
                {
                    driverNames.Add(driver.Name.ToLower().TrimEnd('\\'));
                }
            }
        }

        private List<Result> QueryInternal_Directory_Exists(Query query)
        {
            var search = query.Search.ToLower();
            var results = new List<Result>();

            string incompleteName = "";
            if (!Directory.Exists(search + "\\"))
            {
                //if the last component of the path is incomplete,
                //then make auto complete for it.
                int index = search.LastIndexOf('\\');
                if (index > 0 && index < (search.Length - 1))
                {
                    incompleteName = search.Substring(index + 1);
                    incompleteName = incompleteName.ToLower();
                    search = search.Substring(0, index + 1);
                    if (!Directory.Exists(search))
                        return results;
                }
                else
                    return results;
            }
            else
            {
                if (!search.EndsWith("\\"))
                    search += "\\";
            }

            string firstResult = "Open current directory";
            if (incompleteName.Length > 0)
                firstResult = "Open " + search;
            results.Add(new Result
            {
                Title = firstResult,
                IcoPath = search,
                Score = 10000,
                Action = c =>
                {
                    Process.Start(search);
                    return true;
                }
            });

            //Add children directories
            DirectoryInfo[] dirs = new DirectoryInfo(search).GetDirectories();
            foreach (DirectoryInfo dir in dirs)
            {
                if ((dir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;

                if (incompleteName.Length != 0 && !dir.Name.ToLower().StartsWith(incompleteName))
                    continue;
                DirectoryInfo dirCopy = dir;
                var result = new Result
                {
                    Title = dir.Name,
                    IcoPath = dir.FullName,
                    SubTitle = "Ctrl + Enter to open the directory",
                    Action = c =>
                    {
                        if (c.SpecialKeyState.CtrlPressed)
                        {
                            try
                            {
                                Process.Start(dirCopy.FullName);
                                return true;
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.Message, "Could not start " + dirCopy.FullName);
                                return false;
                            }
                        }
                        context.API.ChangeQuery($"{query.ActionKeyword} {dirCopy.FullName}\\");
                        return false;
                    }
                };

                results.Add(result);
            }

            //Add children files
            FileInfo[] files = new DirectoryInfo(search).GetFiles();
            foreach (FileInfo file in files)
            {
                if ((file.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;
                if (incompleteName.Length != 0 && !file.Name.ToLower().StartsWith(incompleteName))
                    continue;
                string filePath = file.FullName;
                var result = new Result
                {
                    Title = Path.GetFileName(filePath),
                    IcoPath = filePath,
                    Action = c =>
                    {
                        try
                        {
                            Process.Start(filePath);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "Could not start " + filePath);
                        }

                        return true;
                    }
                };

                results.Add(result);
            }

            return results;
        }

        public string GetTranslatedPluginTitle()
        {
            return context.API.GetTranslation("wox_plugin_folder_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return context.API.GetTranslation("wox_plugin_folder_plugin_description");
        }
    }
}