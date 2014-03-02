using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Win32;
using Newtonsoft.Json;
using Wox.Plugin;

namespace Wox.UAC
{
    public class PluginInstaller
    {
        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        /// <summary>
        /// associate filetype with specified program
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileType"></param>
        /// <param name="iconPath"></param>
        /// <param name="overrides"></param>
        private static void SaveReg(string filePath, string fileType, string iconPath, bool overrides)
        {
            RegistryKey classRootKey = Registry.ClassesRoot.OpenSubKey("", true);
            RegistryKey woxKey = classRootKey.OpenSubKey(fileType, true);
            if (woxKey != null)
            {
                if (!overrides)
                {
                    return;
                }
                classRootKey.DeleteSubKeyTree(fileType);
            }
            classRootKey.CreateSubKey(fileType);
            woxKey = classRootKey.OpenSubKey(fileType, true);
            woxKey.SetValue("", "wox.wox");
            woxKey.SetValue("Content Type", "application/wox");

            RegistryKey iconKey = woxKey.CreateSubKey("DefaultIcon");
            iconKey.SetValue("", iconPath);

            woxKey.CreateSubKey("shell");
            RegistryKey shellKey = woxKey.OpenSubKey("shell", true);
            shellKey.SetValue("", "Open");
            RegistryKey openKey = shellKey.CreateSubKey("open");
            openKey.SetValue("", "Open with wox");

            openKey = shellKey.OpenSubKey("open", true);
            openKey.CreateSubKey("command");
            RegistryKey commandKey = openKey.OpenSubKey("command", true);
            string pathString = "\"" + filePath + "\" \"installPlugin\" \"%1\"";
            commandKey.SetValue("", pathString);

            //refresh cache
            SHChangeNotify(0x8000000, 0, IntPtr.Zero, IntPtr.Zero);
        }

        public void RegisterInstaller()
        {
            string filePath = Directory.GetCurrentDirectory() + "\\Wox.UAC.exe";
            string iconPath = Directory.GetCurrentDirectory() + "\\app.ico";

            SaveReg(filePath, ".wox", iconPath, true);
        }

        public void Install(string path)
        {
            if (File.Exists(path))
            {
                string tempFoler = System.IO.Path.GetTempPath() + "\\wox\\plugins";
                if (Directory.Exists(tempFoler))
                {
                    Directory.Delete(tempFoler, true);
                }
                UnZip(path, tempFoler, true);

                string iniPath = tempFoler + "\\plugin.json";
                if (!File.Exists(iniPath))
                {
                    MessageBox.Show("Install failed: config is missing");
                    return;
                }

                PluginMetadata plugin = GetMetadataFromJson(tempFoler);
                if (plugin == null || plugin.Name == null)
                {
                    MessageBox.Show("Install failed: config of this plugin is invalid");
                    return;
                }

                string pluginFolerPath = AppDomain.CurrentDomain.BaseDirectory + "Plugins";
                if (!Directory.Exists(pluginFolerPath))
                {
                    MessageBox.Show("Install failed: cound't find plugin directory");
                    return;
                }

                string newPluginPath = pluginFolerPath + "\\" + plugin.Name;
                string content = string.Format(
                        "Do you want to install following plugin?\r\nName: {0}\r\nVersion: {1}\r\nAuthor: {2}",
                        plugin.Name, plugin.Version, plugin.Author);
                if (Directory.Exists(newPluginPath))
                {
                    PluginMetadata existingPlugin = GetMetadataFromJson(newPluginPath);
                    if (existingPlugin == null || existingPlugin.Name == null)
                    {
                        //maybe broken plugin, just delete it
                        Directory.Delete(newPluginPath, true);
                    }
                    else
                    {
                        content = string.Format(
                        "Do you want to update following plugin?\r\nName: {0}\r\nOld Version: {1}\r\nNew Version: {2}\r\nAuthor: {3}",
                        plugin.Name, existingPlugin.Version, plugin.Version, plugin.Author);
                    }
                }

                MessageBoxResult result = MessageBox.Show(content, "Install plugin",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    if (Directory.Exists(newPluginPath))
                    {
                        Directory.Delete(newPluginPath, true);
                    }
                    UnZip(path, newPluginPath, true);
                    Directory.Delete(tempFoler, true);


                    string wox = AppDomain.CurrentDomain.BaseDirectory + "Wox.exe";
                    if (File.Exists(wox))
                    {
                        ProcessStartInfo info = new ProcessStartInfo(wox, "reloadplugin")
                        {
                            UseShellExecute = true
                        };
                        Process.Start(info);
                        MessageBox.Show("You have installed plugin " + plugin.Name + " successfully.");
                    }
                    else
                    {
                        MessageBox.Show("You have installed plugin " + plugin.Name + " successfully. Please restart your wox to use new plugin.");
                    }
                }
            }
        }

        private static PluginMetadata GetMetadataFromJson(string pluginDirectory)
       {
            string configPath = Path.Combine(pluginDirectory, "plugin.json");
            PluginMetadata metadata;

            if (!File.Exists(configPath))
            {
                return null;
            }

            try
            {
                metadata = JsonConvert.DeserializeObject<PluginMetadata>(File.ReadAllText(configPath));
                metadata.PluginType = PluginType.ThirdParty;
                metadata.PluginDirecotry = pluginDirectory;
            }
            catch (Exception)
            {
                string error = string.Format("Parse plugin config {0} failed: json format is not valid", configPath);
#if (DEBUG)
                {
                    throw new Exception(error);
                }
#endif
                return null;
            }


            if (!AllowedLanguage.IsAllowed(metadata.Language))
            {
                string error = string.Format("Parse plugin config {0} failed: invalid language {1}", configPath,
                    metadata.Language);
#if (DEBUG)
                {
                    throw new Exception(error);
                }
#endif
                return null;
            }
            if (!File.Exists(metadata.ExecuteFilePath))
            {
                string error = string.Format("Parse plugin config {0} failed: ExecuteFile {1} didn't exist", configPath,
                    metadata.ExecuteFilePath);
#if (DEBUG)
                {
                    throw new Exception(error);
                }
#endif
                return null;
            }

            return metadata;
        }

        /// <summary>
        /// unzip 
        /// </summary>
        /// <param name="zipedFile">The ziped file.</param>
        /// <param name="strDirectory">The STR directory.</param>
        /// <param name="overWrite">overwirte</param>
        private void UnZip(string zipedFile, string strDirectory, bool overWrite)
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
    }
}
