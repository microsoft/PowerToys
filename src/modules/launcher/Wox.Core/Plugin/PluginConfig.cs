// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Wox.Infrastructure.Logger;
using Wox.Plugin;

namespace Wox.Core.Plugin
{
    internal abstract class PluginConfig
    {
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
            // todo use linq when diable plugin is implemented since parallel.foreach + list is not thread saft
            foreach (var directory in directories)
            {
                if (File.Exists(Path.Combine(directory, "NeedDelete.txt")))
                {
                    try
                    {
                        Directory.Delete(directory, true);
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
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
                metadata = JsonConvert.DeserializeObject<PluginMetadata>(File.ReadAllText(configPath));
                metadata.PluginDirectory = pluginDirectory;

                // for plugins which doesn't has ActionKeywords key
                metadata.ActionKeywords = metadata.ActionKeywords ?? new List<string> { metadata.ActionKeyword };

                // for plugin still use old ActionKeyword
                metadata.ActionKeyword = metadata.ActionKeywords?[0];
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Log.Exception($"|PluginConfig.GetPluginMetadata|invalid json for config <{configPath}>", e, MethodBase.GetCurrentMethod().DeclaringType);
                return null;
            }

            if (!AllowedLanguage.IsAllowed(metadata.Language))
            {
                Log.Error($"|PluginConfig.GetPluginMetadata|Invalid language <{metadata.Language}> for config <{configPath}>", MethodBase.GetCurrentMethod().DeclaringType);
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
