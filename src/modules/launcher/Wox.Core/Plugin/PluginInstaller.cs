using System;
using System.IO;
using System.Windows;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using Wox.Plugin;

namespace Wox.Core.Plugin
{
    internal class PluginInstaller
    {
        internal static void Install(string path)
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

                string pluginFolerPath = Infrastructure.Constant.PluginsDirectory;

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
                string newPluginPath = Path.Combine(pluginFolerPath, newPluginName);
                string content = $"Do you want to install following plugin?{Environment.NewLine}{Environment.NewLine}" +
                                 $"Name: {plugin.Name}{Environment.NewLine}" +
                                 $"Version: {plugin.Version}{Environment.NewLine}" +
                                 $"Author: {plugin.Author}";
                PluginPair existingPlugin = PluginManager.GetPluginForId(plugin.ID);

                if (existingPlugin != null)
                {
                    content = $"Do you want to update following plugin?{Environment.NewLine}{Environment.NewLine}" +
                              $"Name: {plugin.Name}{Environment.NewLine}" +
                              $"Old Version: {existingPlugin.Metadata.Version}" +
                              $"{Environment.NewLine}New Version: {plugin.Version}" +
                              $"{Environment.NewLine}Author: {plugin.Author}";
                }

                var result = MessageBox.Show(content, "Install plugin", MessageBoxButton.YesNo, MessageBoxImage.Question);
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
                    //    Plugins.Initialize();
                    //}
                    if (MessageBox.Show($"You have installed plugin {plugin.Name} successfully.{Environment.NewLine}" +
                                        "Restart Wox to take effect?",
                                        "Install plugin", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        PluginManager.API.RestarApp();
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
                metadata.PluginDirectory = pluginDirectory;
            }
            catch (Exception)
            {
                string error = $"Parse plugin config {configPath} failed: json format is not valid";
#if (DEBUG)
                {
                    throw new Exception(error);
                }
#endif
                return null;
            }


            if (!AllowedLanguage.IsAllowed(metadata.Language))
            {
                string error = $"Parse plugin config {configPath} failed: invalid language {metadata.Language}";
#if (DEBUG)
                {
                    throw new Exception(error);
                }
#endif
                return null;
            }
            if (!File.Exists(metadata.ExecuteFilePath))
            {
                string error = $"Parse plugin config {configPath} failed: ExecuteFile {metadata.ExecuteFilePath} didn't exist";
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
