using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using WinAlfred.Helper;
using WinAlfred.Plugin;
using log4net;

namespace WinAlfred.PluginLoader
{
    public abstract class BasePluginLoader
    {
        private static string PluginPath = "Plugins";
        private static string PluginConfigName = "plugin.ini";
        protected static List<PluginMetadata> pluginMetadatas = new List<PluginMetadata>();

        public abstract List<IPlugin> LoadPlugin();

        static BasePluginLoader()
        {
            ParsePlugins();
        }

        protected static void ParsePlugins()
        {
            ParseDirectories();
            ParsePackagedPlugin();
        }

        private static void ParseDirectories()
        {
            string[] directories = Directory.GetDirectories(PluginPath);
            foreach (string directory in directories)
            {
                string iniPath = directory + "\\" + PluginConfigName;
                PluginMetadata metadata = GetMetadataFromIni(iniPath);
                if (metadata != null) pluginMetadatas.Add(metadata);
            }
        }

        private static void ParsePackagedPlugin()
        {

        }

        private static PluginMetadata GetMetadataFromIni(string iniPath)
        {
            if (!File.Exists(iniPath))
            {
                Log.FileLogger.Error(string.Format("parse plugin {0} failed: didn't find config file.", iniPath));
                return null;
            }


            try
            {
                PluginMetadata metadata = new PluginMetadata();
                IniParser ini = new IniParser(iniPath);
                metadata.Name = ini.GetSetting("plugin", "Name");
                metadata.Author = ini.GetSetting("plugin", "Author");
                metadata.Description = ini.GetSetting("plugin", "Description");
                metadata.Language = ini.GetSetting("plugin", "Language");
                metadata.Version = ini.GetSetting("plugin", "Version");

                if (!AllowedLanguage.IsAllowed(metadata.Language))
                {
                    Log.FileLogger.Error(string.Format("Parse ini {0} failed: invalid language {1}", iniPath, metadata.Language));
                    return null;
                }

                return metadata;
            }
            catch (Exception e)
            {
                Log.FileLogger.Error(string.Format("Parse ini {0} failed: {1}", iniPath, e.Message));
                return null;
            }
        }
    }
}
