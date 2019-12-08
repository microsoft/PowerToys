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
        private static List<string> _driverNames;
        private PluginInitContext _context;

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
            return new FileSystemSettings(_context.API, _settings);
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
            InitialDriverList();
        }

        public List<Result> Query(Query query)
        {
            var results = GetUserFolderResults(query);

            string search = query.Search.ToLower();
            if (_driverNames != null && !_driverNames.Any(search.StartsWith))
                return results;

            results.AddRange(QueryInternal_Directory_Exists(query));

            // todo why was this hack here?
            foreach (var result in results)
            {
                result.Score += 10;
            }

            return results;
        }

        private Result CreateFolderResult(string title, string path, string queryActionKeyword)
        {
            return new Result
            {
                Title = title,
                IcoPath = path,
                SubTitle = "Ctrl + Enter to open the directory",
                Action = c =>
                {
                    if (c.SpecialKeyState.CtrlPressed)
                    {
                        try
                        {
                            Process.Start(path);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "Could not start " + path);
                            return false;
                        }
                    }

                    string changeTo = path.EndsWith("\\") ? path : path + "\\";
                    _context.API.ChangeQuery(queryActionKeyword + " " + changeTo);
                    return false;
                }
            };
        }

        private List<Result> GetUserFolderResults(Query query)
        {
            string search = query.Search.ToLower();
            var userFolderLinks = _settings.FolderLinks.Where(
                x => x.Nickname.StartsWith(search, StringComparison.OrdinalIgnoreCase));
            var results = userFolderLinks.Select(item => CreateFolderResult(item.Nickname, item.Path, query.ActionKeyword))
                    .ToList();
            return results;
        }

        private void InitialDriverList()
        {
            if (_driverNames == null)
            {
                _driverNames = new List<string>();
                var allDrives = DriveInfo.GetDrives();
                foreach (DriveInfo driver in allDrives)
                {
                    _driverNames.Add(driver.Name.ToLower().TrimEnd('\\'));
                }
            }
        }

        private static readonly char[] _specialSearchChars = new char[]
        {
            '?', '*', '>'
        };

        private List<Result> QueryInternal_Directory_Exists(Query query)
        {
            var search = query.Search.ToLower();
            var results = new List<Result>();
            var hasSpecial = search.IndexOfAny(_specialSearchChars) >= 0;
            string incompleteName = "";
            if (hasSpecial || !Directory.Exists(search + "\\"))
            {
                // if folder doesn't exist, we want to take the last part and use it afterwards to help the user 
                // find the right folder.
                int index = search.LastIndexOf('\\');
                if (index > 0 && index < (search.Length - 1))
                {
                    incompleteName = search.Substring(index + 1).ToLower();
                    search = search.Substring(0, index + 1);
                    if (!Directory.Exists(search))
                        return results;
                }
                else
                    return results;
            }
            else
            { // folder exist, add \ at the end of doesn't exist
                if (!search.EndsWith("\\"))
                    search += "\\";
            }

            results.Add(CreateOpenCurrentFolderResult(incompleteName, search));

            
            var directoryInfo = new DirectoryInfo(search);

            var searchOption= SearchOption.TopDirectoryOnly;
            incompleteName += "*";

            if (incompleteName.StartsWith(">")) // give the ability to search all folder when starting with >
            {
                searchOption = SearchOption.AllDirectories;
                incompleteName = incompleteName.Substring(1);
            }

            // search folder and add results
            var fileSystemInfos = directoryInfo.GetFileSystemInfos(incompleteName, searchOption);

            foreach (var fileSystemInfo in fileSystemInfos)
            {
                if ((fileSystemInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;

                var result =
                    fileSystemInfo is DirectoryInfo
                        ? CreateFolderResult(fileSystemInfo.Name, fileSystemInfo.FullName, query.ActionKeyword)
                        : CreateFileResult(fileSystemInfo.FullName);
                results.Add(result);
            }

            return results;
        }

        private static Result CreateFileResult(string filePath)
        {
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
            return result;
        }

        private static Result CreateOpenCurrentFolderResult(string incompleteName, string search)
        {
            string firstResult = "Open current directory";
            if (incompleteName.Length > 0)
                firstResult = "Open " + search;
            return new Result
            {
                Title = firstResult,
                IcoPath = search,
                Score = 10000,
                Action = c =>
                {
                    Process.Start(search);
                    return true;
                }
            };
        }

        public string GetTranslatedPluginTitle()
        {
            return _context.API.GetTranslation("wox_plugin_folder_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return _context.API.GetTranslation("wox_plugin_folder_plugin_description");
        }
    }
}