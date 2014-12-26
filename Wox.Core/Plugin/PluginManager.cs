using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Wox.Infrastructure.Http;
using Wox.Plugin;

namespace Wox.Core.Plugin
{
    /// <summary>
    /// The entry for managing Wox plugins
    /// </summary>
    public static class PluginManager
    {
        public static String DebuggerMode { get; private set; }
        public static IPublicAPI API { get; private set; }

        private static List<PluginPair> plugins = new List<PluginPair>();

        /// <summary>
        /// Directories that will hold Wox plugin directory
        /// </summary>
        private static List<string> pluginDirectories = new List<string>();

        static PluginManager()
        {
            pluginDirectories.Add(
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Plugins"));

            string userProfilePath = Environment.GetEnvironmentVariable("USERPROFILE");
            if (userProfilePath != null)
            {
                pluginDirectories.Add(Path.Combine(Path.Combine(userProfilePath, ".Wox"), "Plugins"));
            }

            MakesurePluginDirectoriesExist();
        }

        private static void MakesurePluginDirectoriesExist()
        {
            foreach (string pluginDirectory in pluginDirectories)
            {
                if (!Directory.Exists(pluginDirectory))
                {
                    Directory.CreateDirectory(pluginDirectory);
                }
            }
        }

        /// <summary>
        /// Load and init all Wox plugins
        /// </summary>
        public static void Init(IPublicAPI api)
        {
            API = api;
            plugins.Clear();

            List<PluginMetadata> pluginMetadatas = PluginConfig.Parse(pluginDirectories);
            plugins.AddRange(new CSharpPluginLoader().LoadPlugin(pluginMetadatas));
            plugins.AddRange(new JsonRPCPluginLoader<PythonPlugin>().LoadPlugin(pluginMetadatas));

            foreach (PluginPair pluginPair in plugins)
            {
                PluginPair pair = pluginPair;
                ThreadPool.QueueUserWorkItem(o => pair.Plugin.Init(new PluginInitContext()
                {
                    CurrentPluginMetadata = pair.Metadata,
                    Proxy = HttpProxy.Instance,
                    API = API
                }));
            }
        }

        public static void Query(Query query)
        {
            QueryDispatcher.QueryDispatcher.Dispatch(query);
        }

        public static List<PluginPair> AllPlugins
        {
            get
            {
                return plugins;
            }
        }

        public static bool IsUserPluginQuery(Query query)
        {
            if (string.IsNullOrEmpty(query.ActionName)) return false;

            return plugins.Any(o => o.Metadata.PluginType == PluginType.User && o.Metadata.ActionKeyword == query.ActionName);
        }

        public static void ActivatePluginDebugger(string path)
        {
            DebuggerMode = path;
        }

        /// <summary>
        /// get specified plugin, return null if not found
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static PluginPair GetPlugin(string id)
        {
            return AllPlugins.FirstOrDefault(o => o.Metadata.ID == id);
        }
    }
}
