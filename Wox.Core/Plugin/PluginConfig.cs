using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Wox.Core.Exception;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage.UserSettings;
using Wox.Plugin;

namespace Wox.Core.Plugin
{

    internal abstract class PluginConfig
    {
        private const string pluginConfigName = "plugin.json";
        private static List<PluginMetadata> pluginMetadatas = new List<PluginMetadata>();

        /// <summary>
        /// Parse plugin metadata in giving directories
        /// </summary>
        /// <param name="pluginDirectories"></param>
        /// <returns></returns>
        public static List<PluginMetadata> Parse(List<string> pluginDirectories)
        {
            pluginMetadatas.Clear();
            ParseSystemPlugins();
            foreach (string pluginDirectory in pluginDirectories)
            {
                ParseUserPlugins(pluginDirectory);
            }

            if (PluginManager.DebuggerMode != null)
            {
                PluginMetadata metadata = GetPluginMetadata(PluginManager.DebuggerMode);
                if (metadata != null) pluginMetadatas.Add(metadata);
            }
            return pluginMetadatas;
        }

        private static void ParseSystemPlugins()
        {
            pluginMetadatas.Add(new PluginMetadata()
            {
                Name = "System Plugins",
                Author = "System",
                Description = "system plugins collection",
                Website = "http://www.getwox.com",
                Language = AllowedLanguage.CSharp,
                Version = "1.0.0",
                PluginType = PluginType.System,
                ActionKeyword = "*",
                ExecuteFileName = "Wox.Plugin.SystemPlugins.dll",
                PluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            });
        }

        private static void ParseUserPlugins(string pluginDirectory)
        {

            string[] directories = Directory.GetDirectories(pluginDirectory);
            foreach (string directory in directories)
            {
                if (File.Exists((Path.Combine(directory, "NeedDelete.txt"))))
                {
                    Directory.Delete(directory, true);
                    continue;
                }
                PluginMetadata metadata = GetPluginMetadata(directory);
                if (metadata != null)
                {
                    pluginMetadatas.Add(metadata);
                }
            }
        }

        private static PluginMetadata GetPluginMetadata(string pluginDirectory)
        {
            string configPath = Path.Combine(pluginDirectory, pluginConfigName);
            if (!File.Exists(configPath))
            {
                Log.Warn(string.Format("parse plugin {0} failed: didn't find config file.", configPath));
                return null;
            }

            PluginMetadata metadata;
            try
            {
                metadata = JsonConvert.DeserializeObject<PluginMetadata>(File.ReadAllText(configPath));
                metadata.PluginType = PluginType.User;
                metadata.PluginDirectory = pluginDirectory;
            }
            catch (System.Exception)
            {
                string error = string.Format("Parse plugin config {0} failed: json format is not valid", configPath);
                Log.Warn(error);
#if (DEBUG)
                {
                    throw new WoxException(error);
                }
#endif
                return null;
            }


            if (!AllowedLanguage.IsAllowed(metadata.Language))
            {
                string error = string.Format("Parse plugin config {0} failed: invalid language {1}", configPath, metadata.Language);
                Log.Warn(error);
#if (DEBUG)
                {
                    throw new WoxException(error);
                }
#endif
                return null;
            }

            if (!File.Exists(metadata.ExecuteFilePath))
            {
                string error = string.Format("Parse plugin config {0} failed: ExecuteFile {1} didn't exist", configPath, metadata.ExecuteFilePath);
                Log.Warn(error);
#if (DEBUG)
                {
                    throw new WoxException(error);
                }
#endif
                return null;
            }

            //replace action keyword if user customized it.
            var customizedPluginConfig = UserSettingStorage.Instance.CustomizedPluginConfigs.FirstOrDefault(o => o.ID == metadata.ID);
            if (customizedPluginConfig != null && !string.IsNullOrEmpty(customizedPluginConfig.Actionword))
            {
                metadata.ActionKeyword = customizedPluginConfig.Actionword;
            }

            return metadata;
        }
    }
}
