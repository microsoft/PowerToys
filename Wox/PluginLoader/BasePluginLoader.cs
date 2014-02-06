using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Wox.Helper;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.System;

namespace Wox.PluginLoader
{
    public abstract class BasePluginLoader
    {
        private static string PluginPath = AppDomain.CurrentDomain.BaseDirectory + "Plugins";
        private static string PluginConfigName = "plugin.ini";
        protected static List<PluginMetadata> pluginMetadatas = new List<PluginMetadata>();
        public abstract List<PluginPair> LoadPlugin();

        public static void ParsePluginsConfig()
        {
            pluginMetadatas.Clear();
            ParseSystemPlugins();
            ParseThirdPartyPlugins();
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
            metadata.ExecuteFilePath = AppDomain.CurrentDomain.BaseDirectory + metadata.ExecuteFileName;
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
                metadata.ExecuteFilePath = directory + "\\" + ini.GetSetting("plugin", "ExecuteFile");
                metadata.PluginDirecotry = directory + "\\";
                metadata.ExecuteFileName = ini.GetSetting("plugin", "ExecuteFile");

                if (!AllowedLanguage.IsAllowed(metadata.Language))
                {
                    string error = string.Format("Parse ini {0} failed: invalid language {1}", iniPath,
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
                    string error = string.Format("Parse ini {0} failed: ExecuteFilePath didn't exist {1}", iniPath,
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

        ///// <summary>
        ///// Change python execute file name to unique file name using GUID
        ///// this is because if two pythong plugin use the same
        ///// </summary>
        ///// <param name="metadata"></param>
        ///// <returns></returns>
        //private static PluginMetadata filterPythonMetadata(PluginMetadata metadata)
        //{

        //}
    }
}
