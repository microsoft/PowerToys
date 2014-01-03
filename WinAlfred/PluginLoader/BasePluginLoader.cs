using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using WinAlfred.Helper;
using WinAlfred.Plugin;
using WinAlfred.Plugin.System;

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
            ParseSystemPlugins();
            ParseThirdPartyPlugins();
        }

        private static void ParseSystemPlugins()
        {
            try
            {
                Assembly asm = Assembly.GetAssembly(typeof (CMD));
                List<Type> types = asm.GetTypes().Where(o => o.GetInterfaces().Contains(typeof(ISystemPlugin))).ToList();
                foreach (Type type in types)
                {
                    ISystemPlugin sysPlugin = Activator.CreateInstance(types[0]) as ISystemPlugin;
                    PluginMetadata metadata = new PluginMetadata();
                }
            }
            catch (Exception e)
            {
                Log.Error(string.Format("Cound't load system plugin: {0}", e.Message));
#if (DEBUG)
                {
                    throw;
                }
#endif
            } 
        }

        private static void ParseThirdPartyPlugins()
        {
            string[] directories = Directory.GetDirectories(PluginPath);
            foreach (string directory in directories)
            {
                PluginMetadata metadata = GetMetadataFromIni(directory);
                if (metadata != null) pluginMetadatas.Add(metadata);
            }
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
                metadata.PluginType = PluginType.ThirdParty;
                metadata.ActionKeyword = ini.GetSetting("plugin", "ActionKeyword");
                metadata.ExecuteFilePath = AppDomain.CurrentDomain.BaseDirectory + directory + "\\" + ini.GetSetting("plugin", "ExecuteFile");
                metadata.PluginDirecotry = AppDomain.CurrentDomain.BaseDirectory + directory + "\\";
                metadata.ExecuteFileName = ini.GetSetting("plugin", "ExecuteFile");

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
                if (!File.Exists(metadata.ExecuteFilePath))
                {
                    string error = string.Format("Parse ini {0} failed: ExecuteFilePath didn't exist {1}", iniPath,
                                                 metadata.ExecuteFilePath);
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
