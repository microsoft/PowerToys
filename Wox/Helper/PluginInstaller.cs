using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using Wox.Plugin;
using Wox.PluginLoader;

namespace Wox.Helper
{
    public class PluginInstaller
    {
        public static void Install(string path)
        {
            if (File.Exists(path))
            {
                string tempFoler = Path.Combine(Path.GetTempPath(), "wox\\plugins");
                if (Directory.Exists(tempFoler))
                {
                    Directory.Delete(tempFoler, true);
                }
                UnZip(path, tempFoler, true);

                string iniPath = Path.Combine(tempFoler, "plugin.json");
                if (!File.Exists(iniPath))
                {
                    MessageBox.Show("Install failed: plugin config is missing");
                    return;
                }

                PluginMetadata plugin = GetMetadataFromJson(tempFoler);
                if (plugin == null || plugin.Name == null)
                {
                    MessageBox.Show("Install failed: plugin config is invalid");
                    return;
                }

                string pluginFolerPath = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "Plugins");
                if (!Directory.Exists(pluginFolerPath))
                {
                    Directory.CreateDirectory(pluginFolerPath);
                }

                string newPluginName = plugin.Name
                    .Replace("/", "_")
                    .Replace("\\", "_")
                    .Replace(":", "_")
                    .Replace("<", "_")
                    .Replace(">", "_")
                    .Replace("?", "_")
                    .Replace("*", "_")
                    .Replace("|", "_")
                    + "-" + Guid.NewGuid();
                string newPluginPath = Path.Combine(pluginFolerPath,newPluginName);
                string content = string.Format(
                        "Do you want to install following plugin?\r\n\r\nName: {0}\r\nVersion: {1}\r\nAuthor: {2}",
                        plugin.Name, plugin.Version, plugin.Author);
                PluginPair existingPlugin = Plugins.AllPlugins.FirstOrDefault(o => o.Metadata.ID == plugin.ID);

                if (existingPlugin != null)
                {
                        content = string.Format(
                        "Do you want to update following plugin?\r\n\r\nName: {0}\r\nOld Version: {1}\r\nNew Version: {2}\r\nAuthor: {3}",
                        plugin.Name, existingPlugin.Metadata.Version, plugin.Version, plugin.Author);
                }

                MessageBoxResult result = MessageBox.Show(content, "Install plugin",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    if (existingPlugin != null && Directory.Exists(existingPlugin.Metadata.PluginDirectory))
                    {
                        //when plugin is in use, we can't delete them. That's why we need to make plugin folder a random name
                        File.Create(Path.Combine(existingPlugin.Metadata.PluginDirectory, "NeedDelete.txt")).Close();
                    }

                    UnZip(path, newPluginPath, true);
                    Directory.Delete(tempFoler, true);

                    //exsiting plugins may be has loaded by application,
                    //if we try to delelte those kind of plugins, we will get a  error that indicate the
                    //file is been used now.
                    //current solution is to restart wox. Ugly.
                    //if (MainWindow.Initialized)
                    //{
                    //    Plugins.Init();
                    //}
                    if (MessageBox.Show("You have installed plugin " + plugin.Name + " successfully.\r\n Restart Wox to take effect?", "Install plugin",
                            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        ProcessStartInfo Info = new ProcessStartInfo();
                        Info.Arguments = "/C ping 127.0.0.1 -n 1 && \"" +
                                         System.Windows.Forms.Application.ExecutablePath + "\"";
                        Info.WindowStyle = ProcessWindowStyle.Hidden;
                        Info.CreateNoWindow = true;
                        Info.FileName = "cmd.exe";
                        Process.Start(Info);
                        App.Window.CloseApp();
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
                metadata.PluginDirectory = pluginDirectory;
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
        private static void UnZip(string zipedFile, string strDirectory, bool overWrite)
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
