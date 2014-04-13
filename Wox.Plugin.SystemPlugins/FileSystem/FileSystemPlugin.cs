using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Wox.Infrastructure;
using Wox.Infrastructure.Storage.UserSettings;

namespace Wox.Plugin.SystemPlugins.FileSystem
{
    public class FileSystemPlugin : BaseSystemPlugin, ISettingProvider
    {
        private PluginInitContext context;
        private static  List<string> driverNames = null;
        private static Dictionary<string, DirectoryInfo[]> parentDirectories = new Dictionary<string, DirectoryInfo[]>();

        protected override List<Result> QueryInternal(Query query)
        {
            //TODO: Consider always clearing the cache
            List<Result> results = new List<Result>();
            if (string.IsNullOrEmpty(query.RawQuery))
            {
                // clear the cache
                if (parentDirectories.Count > 0)
                    parentDirectories.Clear();

                return results;
            }

            InitialDriverList();

            var input = query.RawQuery.ToLower();
            var inputName = input.Split(new string[] { @"\" }, StringSplitOptions.None).First().ToLower();

            var link = UserSettingStorage.Instance.FolderLinks.FirstOrDefault(x =>
                x.Nickname.Equals(inputName, StringComparison.OrdinalIgnoreCase));
            var currentPath = link != null ? link.Path : null;

            foreach (var item in UserSettingStorage.Instance.FolderLinks)
            {
                var Name = item.Nickname;

                if (Name.StartsWith(input, StringComparison.OrdinalIgnoreCase) && Name.Length != input.Length)
                {
                    Result result = new Result
                    {
                        Title = Name,
                        IcoPath = "Images/folder.png",
                        Action = (c) =>
                        {
                            context.ChangeQuery(item.Nickname);
                            return false;
                        }
                    };

                    results.Add(result);
                }
            }

            if (currentPath == null)
            {
                if (!driverNames.Any(input.StartsWith))
                    return results;

                currentPath = input;
            }
            else
                currentPath += input.Remove(0, inputName.Length);

            if (Directory.Exists(currentPath))
            {
                // show all child directory
                if (input.EndsWith("\\") || input.EndsWith("/"))
                {
                    var dirInfo = new DirectoryInfo(currentPath);
                    var dirs = dirInfo.GetDirectories();

                    var parentDirKey = input.TrimEnd('\\', '/');
                    if (!parentDirectories.ContainsKey(parentDirKey))
                        parentDirectories.Add(parentDirKey, dirs);

                    foreach (var dir in dirs)
                    {
                        if ((dir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                            continue;

                        var dirPath = dir.FullName;
                        Result result = new Result
                        {
                            Title = dir.Name,
                            IcoPath = "Images/folder.png",
                            Action = (c) =>
                            {
                                context.ChangeQuery(dirPath);
                                return false;
                            }
                        };
                        results.Add(result);
                    }

                    if (results.Count == 0)
                    {
                        Result result = new Result
                        {
                            Title = "Open this directory",
                            SubTitle = "No files in this directory",
                            IcoPath = "Images/folder.png",
                            Action = (c) =>
                            {
                                Process.Start(currentPath);
                                return true;
                            }
                        };
                        results.Add(result);
                    }
                }
                else
                {
                    Result result = new Result
                    {
                        Title = "Open this directory",
                        SubTitle = string.Format("path: {0}", currentPath),
                        Score = 50,
                        IcoPath = "Images/folder.png",
                        Action = (c) =>
                        {
                            Process.Start(currentPath);
                            return true;
                        }
                    };
                    results.Add(result);
                }

            }

            // change to search in current directory
            var parentDir = Path.GetDirectoryName(input);
            if (!string.IsNullOrEmpty(parentDir) && results.Count == 0)
            {
                parentDir = parentDir.TrimEnd('\\', '/');
                if (parentDirectories.ContainsKey(parentDir))
                {
                    
                    var dirs = parentDirectories[parentDir];
                    var queryFileName = Path.GetFileName(currentPath).ToLower();
                    var fuzzy = FuzzyMatcher.Create(queryFileName);
                    foreach (var dir in dirs)
                    {
                        if ((dir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                            continue;

                        var matchResult = fuzzy.Evaluate(dir.Name);
                        if (!matchResult.Success)
                            continue;

                        var dirPath = dir.FullName;
                        Result result = new Result
                        {
                            Title = dir.Name,
                            IcoPath = "Images/folder.png",
                            Score = matchResult.Score,
                            Action = (c) =>
                            {
                                context.ChangeQuery(dirPath);
                                return false;
                            }
                        };
                        results.Add(result);
                    }
                }
            }
            

            return results;
        }

        private void InitialDriverList()
        {
            if (driverNames == null)
            {
                driverNames = new List<string>();
                var allDrives = DriveInfo.GetDrives();
                foreach (var driver in allDrives)
                {
                    driverNames.Add(driver.Name.ToLower().TrimEnd('\\'));
                }
            }
        }

        protected override void InitInternal(PluginInitContext context)
        {
            this.context = context;

            if (UserSettingStorage.Instance.FolderLinks == null)
            {
                UserSettingStorage.Instance.FolderLinks = new List<FolderLink>();
                UserSettingStorage.Instance.Save();
            }
        }

        public override string Name
        {
            get { return "File System"; }
        }

        public override string IcoPath
        {
            get { return @"Images\folder.png"; }
        }


        public System.Windows.Controls.Control CreateSettingPanel()
        {		
            return new FileSystemSettings();
        }

        public override string Description
        {
            get { return base.Description; }
        }
    }
}
