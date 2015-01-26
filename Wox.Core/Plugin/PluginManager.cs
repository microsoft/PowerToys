using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Wox.Core.Exception;
using Wox.Core.UI;
using Wox.Core.UserSettings;
using Wox.Infrastructure;
using Wox.Infrastructure.Http;
using Wox.Infrastructure.Logger;
using Wox.Plugin;

namespace Wox.Core.Plugin
{
    /// <summary>
    /// The entry for managing Wox plugins
    /// </summary>
    public static class PluginManager
    {
        public const string ActionKeywordWildcardSign = "*";

        public static String DebuggerMode { get; private set; }
        public static IPublicAPI API { get; private set; }

        private static List<PluginPair> plugins = new List<PluginPair>();

        /// <summary>
        /// Directories that will hold Wox plugin directory
        /// </summary>
        private static List<string> pluginDirectories = new List<string>();


        private static void SetupPluginDirectories()
        {
            pluginDirectories.Add(PluginDirectory);
            MakesurePluginDirectoriesExist();
        }

        public static string PluginDirectory
        {
            get { return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Plugins"); }
        }

        private static void MakesurePluginDirectoriesExist()
        {
            foreach (string pluginDirectory in pluginDirectories)
            {
                if (!Directory.Exists(pluginDirectory))
                {
                    try
                    {
                        Directory.CreateDirectory(pluginDirectory);
                    }
                    catch (System.Exception e)
                    {
                        Log.Error(e.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Load and init all Wox plugins
        /// </summary>
        public static void Init(IPublicAPI api)
        {
            if (api == null) throw new WoxCritialException("api is null");

            SetupPluginDirectories();
            API = api;
            plugins.Clear();

            List<PluginMetadata> pluginMetadatas = PluginConfig.Parse(pluginDirectories);
            plugins.AddRange(new CSharpPluginLoader().LoadPlugin(pluginMetadatas));
            plugins.AddRange(new JsonRPCPluginLoader<PythonPlugin>().LoadPlugin(pluginMetadatas));

            //load plugin i18n languages
            ResourceMerger.ApplyPluginLanguages();

            foreach (PluginPair pluginPair in plugins)
            {
                PluginPair pair = pluginPair;
                ThreadPool.QueueUserWorkItem(o =>
                {
                    using (new Timeit(string.Format("Init {0}", pair.Metadata.Name)))
                    {
                        pair.Plugin.Init(new PluginInitContext()
                        {
                            CurrentPluginMetadata = pair.Metadata,
                            Proxy = HttpProxy.Instance,
                            API = API
                        });
                    }
                });
            }
        }

        public static void InstallPlugin(string path)
        {
            PluginInstaller.Install(path);
        }

        public static void Query(Query query)
        {
            if (!string.IsNullOrEmpty(query.RawQuery.Trim()))
            {
                QueryDispatcher.QueryDispatcher.Dispatch(query);
            }
        }

        public static List<PluginPair> AllPlugins
        {
            get
            {
                return plugins.OrderBy(o => o.Metadata.Name).ToList();
            }
        }

        public static bool IsUserPluginQuery(Query query)
        {
            if (string.IsNullOrEmpty(query.RawQuery)) return false;
            var strings = query.RawQuery.Split(' ');
            if(strings.Length == 1) return false;

            var actionKeyword = strings[0].Trim();
            if (string.IsNullOrEmpty(actionKeyword)) return false;

            return plugins.Any(o => o.Metadata.PluginType == PluginType.User && o.Metadata.ActionKeyword == actionKeyword);
        }

        public static bool IsSystemPlugin(PluginMetadata metadata)
        {
            return metadata.ActionKeyword == ActionKeywordWildcardSign;
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
