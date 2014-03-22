using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Newtonsoft.Json;
using Wox.Helper;
using Wox.Plugin;
using Wox.Plugin.System;

namespace Wox.PluginLoader
{
    public abstract class BasePluginLoader
    {
        private static string PluginPath = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "Plugins");
        private static string PluginConfigName = "plugin.json";
        protected static List<PluginMetadata> pluginMetadatas = new List<PluginMetadata>();
        public abstract List<PluginPair> LoadPlugin();

        public static void ParsePluginsConfig()
        {
            pluginMetadatas.Clear();
            ParseSystemPlugins();
            ParseThirdPartyPlugins();

            if (Plugins.DebuggerMode != null)
            {
                PluginMetadata metadata = GetMetadataFromJson(Plugins.DebuggerMode);
                if (metadata != null) pluginMetadatas.Add(metadata);
            }
        }

        private static void ParseSystemPlugins()
        {
            PluginMetadata metadata = new PluginMetadata();
            metadata.Name = "System Plugins";
            metadata.Author = "System";
            metadata.Description = "system plugins collection";
            metadata.Language = AllowedLanguage.CSharp;
            metadata.Version = "1.0";
            metadata.PluginType = PluginType.System;
            metadata.ActionKeyword = "*";
            metadata.ExecuteFileName = "Wox.Plugin.System.dll";
            metadata.PluginDirecotry = AppDomain.CurrentDomain.BaseDirectory;
            pluginMetadatas.Add(metadata);
        }

        private static void ParseThirdPartyPlugins()
        {
            if (!Directory.Exists(PluginPath))
                Directory.CreateDirectory(PluginPath);

            string[] directories = Directory.GetDirectories(PluginPath);
            foreach (string directory in directories)
            {
                if (File.Exists((Path.Combine(directory, "NeedDelete.txt"))))
                {
                    Directory.Delete(directory,true);
                    continue;
                }
                PluginMetadata metadata = GetMetadataFromJson(directory);
                if (metadata != null) pluginMetadatas.Add(metadata);
            }
        }

        private static PluginMetadata GetMetadataFromJson(string pluginDirectory)
        {
            string configPath = Path.Combine(pluginDirectory, PluginConfigName);
            PluginMetadata metadata;

            if (!File.Exists(configPath))
            {
                Log.Error(string.Format("parse plugin {0} failed: didn't find config file.", configPath));
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
                Log.Error(error);
#if (DEBUG)
                {
                    throw new WoxException(error);
                }
#endif
                return null;
            }


            if (!AllowedLanguage.IsAllowed(metadata.Language))
            {
                string error = string.Format("Parse plugin config {0} failed: invalid language {1}", configPath,
                    metadata.Language);
                Log.Error(error);
#if (DEBUG)
                {
                    throw new WoxException(error);
                }
#endif
                return null;
            }
            if (!File.Exists(metadata.ExecuteFilePath))
            {
                string error = string.Format("Parse plugin config {0} failed: ExecuteFile {1} didn't exist", configPath,
                    metadata.ExecuteFilePath);
                Log.Error(error);
#if (DEBUG)
                {
                    throw new WoxException(error);
                }
#endif
                return null;
            }

            return metadata;
        }
    }
}
