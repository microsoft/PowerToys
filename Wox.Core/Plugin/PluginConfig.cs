using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Wox.Infrastructure.Exception;
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
        /// <param name="pluginDirectories"></param>
        /// <returns></returns>
        public static List<PluginMetadata> Parse(string[] pluginDirectories)
        {
            PluginMetadatas.Clear();
            var directories = pluginDirectories.SelectMany(Directory.GetDirectories);
            ParsePluginConfigs(directories);
            return PluginMetadatas;
        }

        private static void ParsePluginConfigs(IEnumerable<string> directories)
        {
            // todo use linq when diable plugin is implmented since parallel.foreach + list is not thread saft
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
                        Log.Exception($"|PluginConfig.ParsePLuginConfigs|Can't delete <{directory}>", e);
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
                Log.Error($"|PluginConfig.GetPluginMetadata|Didn't find config file <{configPath}>");
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
            catch (Exception e)
            {
                Log.Exception($"|PluginConfig.GetPluginMetadata|invalid json for config <{configPath}>", e);
                return null;
            }


            if (!AllowedLanguage.IsAllowed(metadata.Language))
            {
                Log.Error($"|PluginConfig.GetPluginMetadata|Invalid language <{metadata.Language}> for config <{configPath}>");
                return null;
            }

            if (!File.Exists(metadata.ExecuteFilePath))
            {
                Log.Error($"|PluginConfig.GetPluginMetadata|execute file path didn't exist <{metadata.ExecuteFilePath}> for conifg <{configPath}");
                return null;
            }

            return metadata;
        }
    }
}