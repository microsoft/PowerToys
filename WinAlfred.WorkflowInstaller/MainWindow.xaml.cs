using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Win32;
using WinAlfred.Helper;
using WinAlfred.Plugin;
using Path = System.IO.Path;

namespace WinAlfred.WorkflowInstaller
{

    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            string[] param = Environment.GetCommandLineArgs();
            if (param.Length == 2)
            {
                string workflowPath = param[1];
                //string workflowPath = @"c:\Users\Scott\Desktop\Desktop.winalfred";
                if (workflowPath.EndsWith(".winalfred"))
                {
                    InstallWorkflow(workflowPath);
                }
            }
        }

        private void InstallWorkflow(string path)
        {
            if (File.Exists(path))
            {
                string tempFoler = System.IO.Path.GetTempPath() + "\\winalfred\\workflows";
                if (Directory.Exists(tempFoler))
                {
                    Directory.Delete(tempFoler, true);
                }
                UnZip(path, tempFoler, true);

                string iniPath = tempFoler + "\\plugin.ini";
                if (!File.Exists(iniPath))
                {
                    MessageBox.Show("Install failed: config is missing");
                    Close();
                    return;
                }

                PluginMetadata plugin = GetMetadataFromIni(tempFoler);
                if (plugin == null || plugin.Name == null)
                {
                    MessageBox.Show("Install failed: config of this workflow is invalid");
                    Close();
                    return;
                }

                string pluginFolerPath = AppDomain.CurrentDomain.BaseDirectory + "Plugins";
                if (!Directory.Exists(pluginFolerPath))
                {
                    MessageBox.Show("Install failed: cound't find workflow directory");
                    Close();
                    return;
                }

                string newPluginPath = pluginFolerPath + "\\" + plugin.Name;
                string content = string.Format(
                        "Do you want to install following workflow?\r\nName: {0}\r\nVersion: {1}\r\nAuthor: {2}",
                        plugin.Name, plugin.Version, plugin.Author);
                if (Directory.Exists(newPluginPath))
                {
                    PluginMetadata existingPlugin = GetMetadataFromIni(newPluginPath);
                    if (existingPlugin == null || existingPlugin.Name == null)
                    {
                        //maybe broken plugin, just delete it
                        Directory.Delete(newPluginPath, true);
                    }
                    else
                    {
                        content = string.Format(
                        "Do you want to update following workflow?\r\nName: {0}\r\nOld Version: {1}\r\nNew Version: {2}\r\nAuthor: {3}",
                        plugin.Name, existingPlugin.Version, plugin.Version, plugin.Author);
                    }
                }

                MessageBoxResult result = MessageBox.Show(content, "Install workflow",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    if (Directory.Exists(newPluginPath))
                    {
                        Directory.Delete(newPluginPath, true);
                    }
                    UnZip(path, newPluginPath, true);
                    Directory.Delete(tempFoler, true);


                    string winalfred = AppDomain.CurrentDomain.BaseDirectory + "WinAlfred.exe";
                    if (File.Exists(winalfred))
                    {
                        ProcessStartInfo info = new ProcessStartInfo(winalfred, "reloadWorkflows")
                        {
                            UseShellExecute = true
                        };
                        Process.Start(info);
                        MessageBox.Show("You have installed workflow " + plugin.Name + " successfully.");
                    }
                    else
                    {
                        MessageBox.Show("You have installed workflow " + plugin.Name + " successfully. Please restart your winalfred to use new workflow.");
                    }
                    Close();
                }
                else
                {
                    Close();
                }
            }
        }

        private static PluginMetadata GetMetadataFromIni(string directory)
        {
            string iniPath = directory + "\\plugin.ini";

            if (!File.Exists(iniPath))
            {
                return null;
            }

            try
            {
                PluginMetadata metadata = new PluginMetadata();
                IniParser ini = new IniParser(iniPath);
                metadata.Name = ini.GetSetting("plugin", "Name");
                metadata.Author = ini.GetSetting("plugin", "Author");
                metadata.Description = ini.GetSetting("plugin", "Description");
                metadata.Language = ini.GetSetting("plugin", "Language");
                metadata.Version = ini.GetSetting("plugin", "Version");
                metadata.PluginType = PluginType.ThirdParty;
                metadata.ActionKeyword = ini.GetSetting("plugin", "ActionKeyword");
                metadata.ExecuteFilePath = directory + "\\" + ini.GetSetting("plugin", "ExecuteFile");
                metadata.PluginDirecotry = directory + "\\";
                metadata.ExecuteFileName = ini.GetSetting("plugin", "ExecuteFile");

                if (!AllowedLanguage.IsAllowed(metadata.Language))
                {
                    string error = string.Format("Parse ini {0} failed: invalid language {1}", iniPath,
                                                 metadata.Language);
                    return null;
                }
                if (!File.Exists(metadata.ExecuteFilePath))
                {
                    string error = string.Format("Parse ini {0} failed: ExecuteFilePath didn't exist {1}", iniPath,
                                                 metadata.ExecuteFilePath);
                    return null;
                }

                return metadata;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        /// <summary>
        /// unzip 
        /// </summary>
        /// <param name="zipedFile">The ziped file.</param>
        /// <param name="strDirectory">The STR directory.</param>
        /// <param name="overWrite">overwirte</param>
        public void UnZip(string zipedFile, string strDirectory, bool overWrite)
        {
            if (strDirectory == "")
                strDirectory = Directory.GetCurrentDirectory();
            if (!strDirectory.EndsWith("\\"))
                strDirectory = strDirectory + "\\";

            using (ZipInputStream s = new ZipInputStream(File.OpenRead(zipedFile)))
            {
                ZipEntry theEntry;

                while ((theEntry = s.GetNextEntry()) != null)
                {
                    string directoryName = "";
                    string pathToZip = "";
                    pathToZip = theEntry.Name;

                    if (pathToZip != "")
                        directoryName = Path.GetDirectoryName(pathToZip) + "\\";

                    string fileName = Path.GetFileName(pathToZip);

                    Directory.CreateDirectory(strDirectory + directoryName);

                    if (fileName != "")
                    {
                        if ((File.Exists(strDirectory + directoryName + fileName) && overWrite) || (!File.Exists(strDirectory + directoryName + fileName)))
                        {
                            using (FileStream streamWriter = File.Create(strDirectory + directoryName + fileName))
                            {
                                byte[] data = new byte[2048];
                                while (true)
                                {
                                    int size = s.Read(data, 0, data.Length);

                                    if (size > 0)
                                        streamWriter.Write(data, 0, size);
                                    else
                                        break;
                                }
                                streamWriter.Close();
                            }
                        }
                    }
                }

                s.Close();
            }
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            string filePath = Directory.GetCurrentDirectory() + "\\WinAlfred.WorkflowInstaller.exe";
            string iconPath = Directory.GetCurrentDirectory() + "\\app.ico";

            SaveReg(filePath, ".winalfred", iconPath, false);
        }

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private static void SaveReg(string filePath, string fileType, string iconPath, bool overrides)
        {
            RegistryKey classRootKey = Registry.ClassesRoot.OpenSubKey("", true);
            RegistryKey winAlfredKey = classRootKey.OpenSubKey(fileType, true);
            if (winAlfredKey != null)
            {
                if (!overrides)
                {
                    return;
                }
                classRootKey.DeleteSubKeyTree(fileType);
            }
            classRootKey.CreateSubKey(fileType);
            winAlfredKey = classRootKey.OpenSubKey(fileType, true);
            winAlfredKey.SetValue("", "winalfred.winalfred");
            winAlfredKey.SetValue("Content Type", "application/winalfred");

            RegistryKey iconKey = winAlfredKey.CreateSubKey("DefaultIcon");
            iconKey.SetValue("", iconPath);

            winAlfredKey.CreateSubKey("shell");
            RegistryKey shellKey = winAlfredKey.OpenSubKey("shell", true);
            shellKey.SetValue("", "Open");
            RegistryKey openKey = shellKey.CreateSubKey("open");
            openKey.SetValue("", "Open with winalfred");

            openKey = shellKey.OpenSubKey("open", true);
            openKey.CreateSubKey("command");
            RegistryKey commandKey = openKey.OpenSubKey("command", true);
            string pathString = "\"" + filePath + "\" \"%1\"";
            commandKey.SetValue("", pathString);

            //refresh cache
            SHChangeNotify(0x8000000, 0, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
