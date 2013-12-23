using System;
using System.Collections.Generic;
using System.IO;
using WinAlfred.Helper;
using WinAlfred.Plugin;

namespace WinAlfred.PluginLoader
{
    public abstract class BasePluginLoader
    {
        private static string PluginPath = "Plugins";
        private static string PluginConfigName = "plugin.ini";
        protected static List<PluginMetadata> pluginMetadatas = new List<PluginMetadata>();

        public abstract List<PluginPair> LoadPlugin();

        static BasePluginLoader()
        {
            ParsePlugins();
        }

        private static void ParsePlugins()
        {
            ParseDirectories();
            ParsePackagedPlugin();
        }

        private static void ParseDirectories()
        {
            string[] directories = Directory.GetDirectories(PluginPath);
            foreach (string directory in directories)
            {
                PluginMetadata metadata = GetMetadataFromIni(directory);
                if (metadata != null) pluginMetadatas.Add(metadata);
            }
        }

        private static void ParsePackagedPlugin()
        {

        }

        private static PluginMetadata GetMetadataFromIni(string directory)
        {
            string iniPath = directory + "\\" + PluginConfigName;

            if (!File.Exists(iniPath))
            {
                Log.Error(string.Format("parse plugin {0} failed: didn't find config file.", iniPath));
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
                metadata.ActionKeyword = ini.GetSetting("plugin", "ActionKeyword");
                metadata.ExecuteFile = AppDomain.CurrentDomain.BaseDirectory + directory + "\\" + ini.GetSetting("plugin", "ExecuteFile");
                metadata.PluginDirecotry = AppDomain.CurrentDomain.BaseDirectory + directory + "\\";

                if (!AllowedLanguage.IsAllowed(metadata.Language))
                {
                    string error = string.Format("Parse ini {0} failed: invalid language {1}", iniPath,
                                                 metadata.Language);
                    Log.Error(error);
#if (DEBUG)
                    {
                        throw new WinAlfredException(error);
                    }
#endif
                    return null;
                }
                if (!File.Exists(metadata.ExecuteFile))
                {
                    string error = string.Format("Parse ini {0} failed: ExecuteFile didn't exist {1}", iniPath,
                                                 metadata.ExecuteFile);
                    Log.Error(error);
#if (DEBUG)
                    {
                        throw new WinAlfredException(error);
                    }
#endif
                    return null;
                }

                return metadata;
            }
            catch (Exception e)
            {
                Log.Error(string.Format("Parse ini {0} failed: {1}", iniPath, e.Message));
#if (DEBUG)
                {
                    throw;
                }
#endif
                return null;
            }
        }
    }
}
