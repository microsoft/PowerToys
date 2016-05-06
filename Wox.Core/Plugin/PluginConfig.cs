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
                        Log.Fatal(e);
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
                Log.Warn($"parse plugin {configPath} failed: didn't find config file.");
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
                string msg = $"Parse plugin config {configPath} failed: json format is not valid";
                Log.Error(new WoxException(msg));
                return null;
            }


            if (!AllowedLanguage.IsAllowed(metadata.Language))
            {
                string msg = $"Parse plugin config {configPath} failed: invalid language {metadata.Language}";
                Log.Error(new WoxException(msg));
                return null;
            }

            if (!File.Exists(metadata.ExecuteFilePath))
            {
                string msg = $"Parse plugin config {configPath} failed: ExecuteFile {metadata.ExecuteFilePath} didn't exist";
                Log.Error(new WoxException(msg));
                return null;
            }

            return metadata;
        }
    }
}