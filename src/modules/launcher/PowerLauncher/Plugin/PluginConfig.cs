// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace PowerLauncher.Plugin
{
    internal abstract class PluginConfig
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;
        private static readonly IFile File = FileSystem.File;
        private static readonly IDirectory Directory = FileSystem.Directory;

        private const string PluginConfigName = "plugin.json";
        private static readonly List<PluginMetadata> PluginMetadatas = new List<PluginMetadata>();

        /// <summary>
        /// Parse plugin metadata in giving directories
        /// </summary>
        /// <param name="pluginDirectories">directories with plugins</param>
        /// <returns>List with plugin meta data</returns>
        public static List<PluginMetadata> Parse(string[] pluginDirectories)
        {
            PluginMetadatas.Clear();
            var directories = pluginDirectories.SelectMany(Directory.GetDirectories);
            ParsePluginConfigs(directories);

            return PluginMetadatas;
        }

        private static void ParsePluginConfigs(IEnumerable<string> directories)
        {
            // todo use linq when disable plugin is implemented since parallel.foreach + list is not thread saft
            foreach (var directory in directories)
            {
                if (File.Exists(Path.Combine(directory, "NeedDelete.txt")))
                {
                    try
                    {
                        Directory.Delete(directory, true);
                    }
                    catch (Exception e)
                    {
                        Log.Exception($"Can't delete <{directory}>", e, MethodBase.GetCurrentMethod().DeclaringType);
                    }
                }
                else
                {
                    PluginMetadata metadata = GetPluginMetadata(directory);
                    if (metadata != null)
                    {
                        PluginMetadatas.Add(metadata);
                    }
                }
            }
        }

        private static PluginMetadata GetPluginMetadata(string pluginDirectory)
        {
            string configPath = Path.Combine(pluginDirectory, PluginConfigName);
            if (!File.Exists(configPath))
            {
                Log.Error($"Didn't find config file <{configPath}>", MethodBase.GetCurrentMethod().DeclaringType);

                return null;
            }

            PluginMetadata metadata;
            try
            {
                metadata = JsonSerializer.Deserialize<PluginMetadata>(File.ReadAllText(configPath));
                metadata.PluginDirectory = pluginDirectory;
            }
            catch (Exception e)
            {
                Log.Exception($"|PluginConfig.GetPluginMetadata|invalid json for config <{configPath}>", e, MethodBase.GetCurrentMethod().DeclaringType);
                return null;
            }

            if (!AllowedLanguage.IsAllowed(metadata.Language))
            {
                Log.Error($"|PluginConfig.GetPluginMetadata|Invalid language <{metadata.Language}> for config <{configPath}>", MethodBase.GetCurrentMethod().DeclaringType);
                return null;
            }

            if (string.IsNullOrEmpty(metadata.IcoPathDark) || string.IsNullOrEmpty(metadata.IcoPathLight))
            {
                Log.Error($"|PluginConfig.GetPluginMetadata|couldn't get icon information for config <{configPath}>", MethodBase.GetCurrentMethod().DeclaringType);
                return null;
            }

            if (!File.Exists(metadata.ExecuteFilePath))
            {
                Log.Error($"|PluginConfig.GetPluginMetadata|execute file path didn't exist <{metadata.ExecuteFilePath}> for config <{configPath}", MethodBase.GetCurrentMethod().DeclaringType);
                return null;
            }

            return metadata;
        }
    }
}
