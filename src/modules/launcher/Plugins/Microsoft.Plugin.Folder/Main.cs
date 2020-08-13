// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.PowerToys.Settings.UI.Lib;
using Wox.Infrastructure;
using Wox.Infrastructure.Storage;
using Wox.Plugin;

namespace Microsoft.Plugin.Folder
{
    public class Main : IPlugin, ISettingProvider, IPluginI18n, ISavable, IContextMenu, IDisposable
    {
        public const string FolderImagePath = "Images\\folder.dark.png";
        public const string FileImagePath = "Images\\file.dark.png";
        public const string DeleteFileFolderImagePath = "Images\\delete.dark.png";
        public const string CopyImagePath = "Images\\copy.dark.png";

        private const string _fileExplorerProgramName = "explorer";
        private static readonly PluginJsonStorage<FolderSettings> _storage = new PluginJsonStorage<FolderSettings>();
        private static readonly FolderSettings _settings = _storage.Load();
        private static List<string> _driverNames;
        private static PluginInitContext _context;
        private IContextMenu _contextMenuLoader;
        private static string warningIconPath;
        private bool _disposed = false;

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
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _contextMenuLoader = new ContextMenuLoader(context);
            InitialDriverList();

            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());
        }

        private static void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                warningIconPath = "Images/Warning.light.png";
            }
            else
            {
                warningIconPath = "Images/Warning.dark.png";
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "The parameter is unused")]
        private void OnThemeChanged(Theme _, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Do not want to change the behavior of the application, but want to enforce static analysis")]
        public List<Result> Query(Query query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(paramName: nameof(query));
            }

            var results = GetFolderPluginResults(query);

            // todo why was this hack here?
            foreach (var result in results)
            {
                result.Score += 10;
            }

            return results;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Do not want to change the behavior of the application, but want to enforce static analysis")]
        public static List<Result> GetFolderPluginResults(Query query)
        {
            var results = GetUserFolderResults(query);
            string search = query.Search.ToLower(CultureInfo.InvariantCulture);

            if (!IsDriveOrSharedFolder(search))
            {
                return results;
            }

            results.AddRange(QueryInternalDirectoryExists(query));
            return results;
        }

        private static bool IsDriveOrSharedFolder(string search)
        {
            if (search == null)
            {
                throw new ArgumentNullException(nameof(search));
            }

            if (search.StartsWith(@"\\", StringComparison.InvariantCulture))
            { // share folder
                return true;
            }

            if (_driverNames != null && _driverNames.Any(search.StartsWith))
            { // normal drive letter
                return true;
            }

            if (_driverNames == null && search.Length > 2 && char.IsLetter(search[0]) && search[1] == ':')
            { // when we don't have the drive letters we can try...
                return true; // we don't know so let's give it the possibility
            }

            return false;
        }

        private static Result CreateFolderResult(string title, string subtitle, string path, Query query)
        {
            return new Result
            {
                Title = title,
                IcoPath = path,
                SubTitle = "Folder: " + subtitle,
                QueryTextDisplay = path,
                TitleHighlightData = StringMatcher.FuzzySearch(query.Search, title).MatchData,
                ContextData = new SearchResult { Type = ResultType.Folder, FullPath = path },
                Action = c =>
                {
                    Process.Start(_fileExplorerProgramName, path);
                    return true;
                },
            };
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Do not want to change the behavior of the application, but want to enforce static analysis")]
        private static List<Result> GetUserFolderResults(Query query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(paramName: nameof(query));
            }

            string search = query.Search.ToLower(CultureInfo.InvariantCulture);
            var userFolderLinks = _settings.FolderLinks.Where(
                x => x.Nickname.StartsWith(search, StringComparison.OrdinalIgnoreCase));
            var results = userFolderLinks.Select(item =>
                CreateFolderResult(item.Nickname, item.Path, item.Path, query)).ToList();
            return results;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Do not want to change the behavior of the application, but want to enforce static analysis")]
        private static void InitialDriverList()
        {
            if (_driverNames == null)
            {
                _driverNames = new List<string>();
                var allDrives = DriveInfo.GetDrives();
                foreach (DriveInfo driver in allDrives)
                {
                    _driverNames.Add(driver.Name.ToLower(CultureInfo.InvariantCulture).TrimEnd('\\'));
                }
            }
        }

        private static readonly char[] _specialSearchChars = new char[]
        {
            '?', '*', '>',
        };

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Do not want to change the behavior of the application, but want to enforce static analysis")]
        private static List<Result> QueryInternalDirectoryExists(Query query)
        {
            var search = query.Search;
            var results = new List<Result>();
            var hasSpecial = search.IndexOfAny(_specialSearchChars) >= 0;
            string incompleteName = string.Empty;
            if (hasSpecial || !Directory.Exists(search + "\\"))
            {
                // if folder doesn't exist, we want to take the last part and use it afterwards to help the user
                // find the right folder.
                int index = search.LastIndexOf('\\');
                if (index > 0 && index < (search.Length - 1))
                {
                    incompleteName = search.Substring(index + 1).ToLower(CultureInfo.InvariantCulture);
                    search = search.Substring(0, index + 1);
                    if (!Directory.Exists(search))
                    {
                        return results;
                    }
                }
                else
                {
                    return results;
                }
            }
            else
            {
                // folder exist, add \ at the end of doesn't exist
                if (!search.EndsWith("\\", StringComparison.InvariantCulture))
                {
                    search += "\\";
                }
            }

            results.Add(CreateOpenCurrentFolderResult(search));

            var searchOption = SearchOption.TopDirectoryOnly;
            incompleteName += "*";

            // give the ability to search all folder when starting with >
            if (incompleteName.StartsWith(">", StringComparison.InvariantCulture))
            {
                searchOption = SearchOption.AllDirectories;

                // match everything before and after search term using supported wildcard '*', ie. *searchterm*
                incompleteName = "*" + incompleteName.Substring(1);
            }

            var folderList = new List<Result>();
            var fileList = new List<Result>();

            try
            {
                // search folder and add results
                var directoryInfo = new DirectoryInfo(search);
                var fileSystemInfos = directoryInfo.GetFileSystemInfos(incompleteName, searchOption);

                foreach (var fileSystemInfo in fileSystemInfos)
                {
                    if ((fileSystemInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                    {
                        continue;
                    }

                    if (fileSystemInfo is DirectoryInfo)
                    {
                        var folderSubtitleString = fileSystemInfo.FullName;

                        folderList.Add(CreateFolderResult(fileSystemInfo.Name, folderSubtitleString, fileSystemInfo.FullName, query));
                    }
                    else
                    {
                        fileList.Add(CreateFileResult(fileSystemInfo.FullName, query));
                    }
                }
            }
            catch (Exception e)
            {
                if (e is UnauthorizedAccessException || e is ArgumentException)
                {
                    results.Add(new Result { Title = e.Message, Score = 501 });

                    return results;
                }

                throw;
            }

            results = results.Concat(folderList.OrderBy(x => x.Title).Take(_settings.MaxFolderResults)).Concat(fileList.OrderBy(x => x.Title).Take(_settings.MaxFileResults)).ToList();

            // Show warning message if result has been truncated
            if (folderList.Count > _settings.MaxFolderResults || fileList.Count > _settings.MaxFileResults)
            {
                var preTruncationCount = folderList.Count + fileList.Count;
                var postTruncationCount = Math.Min(folderList.Count, _settings.MaxFolderResults) + Math.Min(fileList.Count, _settings.MaxFileResults);
                results.Add(CreateTruncatedItemsResult(search, preTruncationCount, postTruncationCount));
            }

            return results.ToList();
        }

        private static Result CreateTruncatedItemsResult(string search, int preTruncationCount, int postTruncationCount)
        {
            return new Result
            {
                Title = _context.API.GetTranslation("Microsoft_plugin_folder_truncation_warning_title"),
                QueryTextDisplay = search,
                SubTitle = string.Format(CultureInfo.InvariantCulture, _context.API.GetTranslation("Microsoft_plugin_folder_truncation_warning_subtitle"), postTruncationCount, preTruncationCount),
                IcoPath = warningIconPath,
            };
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "We want to keep the process alve and instead inform the user of the error")]
        private static Result CreateFileResult(string filePath, Query query)
        {
            var result = new Result
            {
                Title = Path.GetFileName(filePath),
                SubTitle = "Folder: " + filePath,
                IcoPath = filePath,
                TitleHighlightData = StringMatcher.FuzzySearch(query.Search, Path.GetFileName(filePath)).MatchData,
                Action = c =>
                {
                    try
                    {
                        Process.Start(_fileExplorerProgramName, filePath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Could not start " + filePath);
                    }

                    return true;
                },
                ContextData = new SearchResult { Type = ResultType.File, FullPath = filePath },
            };
            return result;
        }

        private static Result CreateOpenCurrentFolderResult(string search)
        {
            var firstResult = "Open " + search;

            var folderName = search.TrimEnd('\\').Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.None).Last();
            var sanitizedPath = Regex.Replace(search, @"[\/\\]+", "\\");

            // A network path must start with \\
            if (sanitizedPath.StartsWith("\\", StringComparison.InvariantCulture))
            {
                sanitizedPath = sanitizedPath.Insert(0, "\\");
            }

            return new Result
            {
                Title = firstResult,
                QueryTextDisplay = search,
                SubTitle = $"Folder: Use > to search within the directory. Use * to search for file extensions. Or use both >*.",
                IcoPath = search,
                Score = 500,
                Action = c =>
                {
                    Process.Start(_fileExplorerProgramName, sanitizedPath);
                    return true;
                },
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

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            return _contextMenuLoader.LoadContextMenus(selectedResult);
        }

        public void UpdateSettings(PowerLauncherSettings settings)
        {
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _context.API.ThemeChanged -= OnThemeChanged;
                    _disposed = true;
                }
            }
        }
    }
}
