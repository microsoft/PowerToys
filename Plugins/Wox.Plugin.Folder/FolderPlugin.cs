using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace Wox.Plugin.Folder
{
    public class FolderPlugin : IPlugin, ISettingProvider, IPluginI18n
    {
        private static List<string> driverNames;
        private PluginInitContext context;

        public Control CreateSettingPanel()
        {
            return new FileSystemSettings(context.API);
        }

        public void Init(PluginInitContext context)
        {
            this.context = context;
            this.context.API.BackKeyDownEvent += ApiBackKeyDownEvent;
            this.context.API.ResultItemDropEvent += API_ResultItemDropEvent;
            InitialDriverList();
            if (FolderStorage.Instance.FolderLinks == null)
            {
                FolderStorage.Instance.FolderLinks = new List<FolderLink>();
                FolderStorage.Instance.Save();
            }
        }

        void API_ResultItemDropEvent(Result result, IDataObject dropObject, DragEventArgs e)
        {
            if (dropObject.GetDataPresent(DataFormats.FileDrop))
            {
                HanldeFilesDrop(result, dropObject);
            }
            e.Handled = true;
        }

        private void HanldeFilesDrop(Result targetResult, IDataObject dropObject)
        {
            List<string> files = ((string[])dropObject.GetData(DataFormats.FileDrop, false)).ToList();
            context.API.ShowContextMenu(context.CurrentPluginMetadata, GetContextMenusForFileDrop(targetResult, files));
        }

        private static List<Result> GetContextMenusForFileDrop(Result targetResult, List<string> files)
        {
            List<Result> contextMenus = new List<Result>();
            string folderPath = ((FolderLink) targetResult.ContextData).Path;
            contextMenus.Add(new Result()
            {
                Title = "Copy to this folder",
                IcoPath = "Images/copy.png",
                Action = _ =>
                {
                    MessageBox.Show("Copy");
                    return true;
                }
            });
            return contextMenus;
        }

        private void ApiBackKeyDownEvent(WoxKeyDownEventArgs e)
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

        public List<Result> Query(Query query)
        {
            string input = query.Search.ToLower();

            List<FolderLink> userFolderLinks = FolderStorage.Instance.FolderLinks.Where(
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
                            context.API.ChangeQuery(item.Path + (item.Path.EndsWith("\\")? "": "\\"));
                            return false;
                        },
                        ContextData = item
                    }).ToList();

            if (driverNames != null && !driverNames.Any(input.StartsWith))
                return results;

            //if (!input.EndsWith("\\"))
            //{
            //    //"c:" means "the current directory on the C drive" whereas @"c:\" means "root of the C drive"
            //    input = input + "\\";
            //}
            results.AddRange(QueryInternal_Directory_Exists(input));

            return results;
        }    private void InitialDriverList()
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
            
            string incompleteName = "";
            if (!Directory.Exists(rawQuery + "\\"))
            {
                //if the last component of the path is incomplete,
                //then make auto complete for it.
                int index = rawQuery.LastIndexOf('\\');
                if (index > 0 && index < (rawQuery.Length - 1))
                {
                    incompleteName = rawQuery.Substring(index + 1);
                    incompleteName = incompleteName.ToLower();
                    rawQuery = rawQuery.Substring(0, index + 1);
                    if (!Directory.Exists(rawQuery))
                        return results;
                }
                else
                    return results;
            }
            else
            {
                if (!rawQuery.EndsWith("\\"))
                    rawQuery += "\\";
            }

            string firstResult = "Open current directory";
            if (incompleteName.Length > 0)
                firstResult = "Open " + rawQuery;
            results.Add(new Result(firstResult, "Images/folder.png")
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

                if (incompleteName.Length != 0 && !dir.Name.ToLower().StartsWith(incompleteName))
                    continue;
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
                if (incompleteName.Length != 0 && !file.Name.ToLower().StartsWith(incompleteName))
                    continue;
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

        public string GetLanguagesFolder()
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Languages");
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