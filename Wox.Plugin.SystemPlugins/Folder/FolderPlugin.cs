using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Wox.Infrastructure.Storage.UserSettings;
using Control = System.Windows.Controls.Control;

namespace Wox.Plugin.SystemPlugins.Folder
{
    public class FolderPlugin : BaseSystemPlugin, ISettingProvider
    {
        #region Properties

        private static List<string> driverNames;
        private PluginInitContext context;

        public override string Description
        {
            get { return "Provide opening folder from wox directorily. You can add your favorite folders."; }
        }

        public override string ID
        {
            get { return "B4D3B69656E14D44865C8D818EAE47C4"; }
        }

        public override string Name
        {
            get { return "Folder"; }
        }

        public override string IcoPath
        {
            get { return @"Images\folder.png"; }
        }

        #endregion Properties

        public Control CreateSettingPanel()
        {
            return new FileSystemSettings();
        }

        protected override void InitInternal(PluginInitContext context)
        {
            this.context = context;
            this.context.API.BackKeyDownEvent += ApiBackKeyDownEvent;
            InitialDriverList();
            if (UserSettingStorage.Instance.FolderLinks == null)
            {
                UserSettingStorage.Instance.FolderLinks = new List<FolderLink>();
                UserSettingStorage.Instance.Save();
            }
        }

        private void ApiBackKeyDownEvent(object sender, WoxKeyDownEventArgs e)
        {
            string query = e.Query;
            if (Directory.Exists(query))
            {
                if (query.EndsWith("\\"))
                {
                    query = query.Remove(query.Length - 1);
                }

                if (query.Contains("\\"))
                {
                    int index = query.LastIndexOf("\\");
                    query = query.Remove(index) + "\\";
                }

                context.API.ChangeQuery(query);
            }
        }

        protected override List<Result> QueryInternal(Query query)
        {
            string input = query.RawQuery.ToLower();

            List<FolderLink> userFolderLinks = UserSettingStorage.Instance.FolderLinks.Where(
                x => x.Nickname.StartsWith(input, StringComparison.OrdinalIgnoreCase)).ToList();
            List<Result> results =
                userFolderLinks.Select(
                    item => new Result(item.Nickname, "Images/folder.png", "Ctrl + Enter to open the directory")
                    {
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
                            context.API.ChangeQuery(item.Path);
                            return false;
                        }
                    }).ToList();

            if (!driverNames.Any(input.StartsWith))
                return results;

            if (!input.EndsWith("\\"))
            {
                //"c:" means "the current directory on the C drive" whereas @"c:\" means "root of the C drive"
                input = input + "\\";
            }
            results.AddRange(QueryInternal_Directory_Exists(input));

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

        private List<Result> QueryInternal_Directory_Exists(string rawQuery)
        {
            var results = new List<Result>();
            if (!Directory.Exists(rawQuery)) return results;

            results.Add(new Result("Open current directory", "Images/folder.png")
            {
                Score = 10000,
                Action = c =>
                {
                    Process.Start(rawQuery);
                    return true;
                }
            });

            //Add children directories
            DirectoryInfo[] dirs = new DirectoryInfo(rawQuery).GetDirectories();
            foreach (DirectoryInfo dir in dirs)
            {
                if ((dir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;

                DirectoryInfo dirCopy = dir;
                var result = new Result(dir.Name, "Images/folder.png", "Ctrl + Enter to open the directory")
                {
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
                        context.API.ChangeQuery(dirCopy.FullName + "\\");
                        return false;
                    }
                };

                results.Add(result);
            }

            //Add children files
            FileInfo[] files = new DirectoryInfo(rawQuery).GetFiles();
            foreach (FileInfo file in files)
            {
                if ((file.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;

                string filePath = file.FullName;
                var result = new Result(Path.GetFileName(filePath), "Images/file.png")
                {
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
    }
}