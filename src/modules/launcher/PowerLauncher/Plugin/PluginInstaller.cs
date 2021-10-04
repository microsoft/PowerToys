// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using ICSharpCode.SharpZipLib.Zip;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace PowerLauncher.Plugin
{
    internal class PluginInstaller
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;
        private static readonly IFile File = FileSystem.File;
        private static readonly IDirectory Directory = FileSystem.Directory;

        internal static void Install(string path)
        {
            if (File.Exists(path))
            {
                string tempFolder = Path.Combine(Path.GetTempPath(), "wox\\plugins");
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, true);
                }

                UnZip(path, tempFolder, true);

                string iniPath = Path.Combine(tempFolder, "plugin.json");
                if (!File.Exists(iniPath))
                {
                    MessageBox.Show("Install failed: plugin config is missing");
                    return;
                }

                PluginMetadata plugin = GetMetadataFromJson(tempFolder);
                if (plugin?.Name == null)
                {
                    MessageBox.Show("Install failed: plugin config is invalid");
                    return;
                }

                string pluginFolderPath = Constant.PluginsDirectory;

                // Using Ordinal since this is part of a path
                string newPluginName = plugin.Name
                    .Replace("/", "_", StringComparison.Ordinal)
                    .Replace("\\", "_", StringComparison.Ordinal)
                    .Replace(":", "_", StringComparison.Ordinal)
                    .Replace("<", "_", StringComparison.Ordinal)
                    .Replace(">", "_", StringComparison.Ordinal)
                    .Replace("?", "_", StringComparison.Ordinal)
                    .Replace("*", "_", StringComparison.Ordinal)
                    .Replace("|", "_", StringComparison.Ordinal)
                    + "-" + Guid.NewGuid();
                string newPluginPath = Path.Combine(pluginFolderPath, newPluginName);
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
                        // when plugin is in use, we can't delete them. That's why we need to make plugin folder a random name
                        File.Create(Path.Combine(existingPlugin.Metadata.PluginDirectory, "NeedDelete.txt")).Close();
                    }

                    UnZip(path, newPluginPath, true);
                    Directory.Delete(tempFolder, true);

                    // existing plugins could be loaded by the application,
                    // if we try to delete those kind of plugins, we will get a  error that indicate the
                    // file is been used now.
                    // current solution is to restart wox. Ugly.
                    // if (MainWindow.Initialized)
                    // {
                    //    Plugins.Initialize();
                    // }
                    if (MessageBox.Show($"You have installed plugin {plugin.Name} successfully.{Environment.NewLine} Restart Wox to take effect?", "Install plugin", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        PluginManager.API.RestartApp();
                    }
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Suppressing this to enable FxCop. We are logging the exception, and going forward general exceptions should not be caught")]
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
                metadata = JsonSerializer.Deserialize<PluginMetadata>(File.ReadAllText(configPath));
                metadata.PluginDirectory = pluginDirectory;
            }
            catch (Exception e)
            {
                string error = $"Parse plugin config {configPath} failed: json format is not valid";
                Log.Exception(error, e, MethodBase.GetCurrentMethod().DeclaringType);
#if DEBUG
                {
                    throw new Exception(error);
                }
#else
                return null;
#endif
            }

            if (!AllowedLanguage.IsAllowed(metadata.Language))
            {
                string error = $"Parse plugin config {configPath} failed: invalid language {metadata.Language}";
#if DEBUG
                {
                    throw new Exception(error);
                }
#else
                return null;
#endif
            }

            if (!File.Exists(metadata.ExecuteFilePath))
            {
                string error = $"Parse plugin config {configPath} failed: ExecuteFile {metadata.ExecuteFilePath} didn't exist";
#if DEBUG
                {
                    throw new Exception(error);
                }
#else
                return null;
#endif
            }

            return metadata;
        }

        /// <summary>
        /// unzip
        /// </summary>
        /// <param name="zippedFile">The zipped file.</param>
        /// <param name="strDirectory">The STR directory.</param>
        /// <param name="overWrite">overwrite</param>
        private static void UnZip(string zippedFile, string strDirectory, bool overWrite)
        {
            if (string.IsNullOrEmpty(strDirectory))
            {
                strDirectory = Directory.GetCurrentDirectory();
            }

            // Using Ordinal since this is a path
            if (!strDirectory.EndsWith("\\", StringComparison.Ordinal))
            {
                strDirectory += "\\";
            }

            using (ZipInputStream s = new ZipInputStream(File.OpenRead(zippedFile)))
            {
                ZipEntry theEntry;

                while ((theEntry = s.GetNextEntry()) != null)
                {
                    string directoryName = string.Empty;
                    string pathToZip = string.Empty;
                    pathToZip = theEntry.Name;

                    if (!string.IsNullOrEmpty(pathToZip))
                    {
                        directoryName = Path.GetDirectoryName(pathToZip) + "\\";
                    }

                    string fileName = Path.GetFileName(pathToZip);

                    Directory.CreateDirectory(strDirectory + directoryName);

                    if (!string.IsNullOrEmpty(fileName))
                    {
                        if ((File.Exists(strDirectory + directoryName + fileName) && overWrite) || (!File.Exists(strDirectory + directoryName + fileName)))
                        {
                            using (Stream streamWriter = File.Create(strDirectory + directoryName + fileName))
                            {
                                byte[] data = new byte[2048];
                                while (true)
                                {
                                    int size = s.Read(data, 0, data.Length);

                                    if (size > 0)
                                    {
                                        streamWriter.Write(data, 0, size);
                                    }
                                    else
                                    {
                                        break;
                                    }
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
