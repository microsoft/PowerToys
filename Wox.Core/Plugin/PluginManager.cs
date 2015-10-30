using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Wox.Core.Exception;
using Wox.Core.i18n;
using Wox.Core.UI;
using Wox.Core.UserSettings;
using Wox.Infrastructure;
using Wox.Infrastructure.Logger;
using Wox.Plugin;
using Wox.Plugin.Features;

namespace Wox.Core.Plugin
{
    /// <summary>
    /// The entry for managing Wox plugins
    /// </summary>
    public static class PluginManager
    {
        public const string ActionKeywordWildcardSign = "*";
        private static List<PluginMetadata> pluginMetadatas;
        private static List<KeyValuePair<PluginPair, IInstantQuery>> instantSearches;
        private static List<KeyValuePair<PluginPair, IExclusiveQuery>> exclusiveSearchPlugins;
        private static List<KeyValuePair<PluginPair, IContextMenu>> contextMenuPlugins;

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

            pluginMetadatas = PluginConfig.Parse(pluginDirectories);
            plugins.AddRange(new CSharpPluginLoader().LoadPlugin(pluginMetadatas));
            plugins.AddRange(new JsonRPCPluginLoader<PythonPlugin>().LoadPlugin(pluginMetadatas));

            //load plugin i18n languages
            ResourceMerger.ApplyPluginLanguages();

            foreach (PluginPair pluginPair in plugins)
            {
                PluginPair pair = pluginPair;
                ThreadPool.QueueUserWorkItem(o =>
                {
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    pair.Plugin.Init(new PluginInitContext()
                    {
                        CurrentPluginMetadata = pair.Metadata,
                        Proxy = HttpProxy.Instance,
                        API = API
                    });
                    sw.Stop();
                    DebugHelper.WriteLine(string.Format("Plugin init:{0} - {1}", pair.Metadata.Name, sw.ElapsedMilliseconds));
                    pair.InitTime = sw.ElapsedMilliseconds;
                    InternationalizationManager.Instance.UpdatePluginMetadataTranslations(pair);
                });
            }

            ThreadPool.QueueUserWorkItem(o =>
            {
                LoadInstantSearches();
            });
        }

        public static void InstallPlugin(string path)
        {
            PluginInstaller.Install(path);
        }

        public static void Query(Query query)
        {
            if (!string.IsNullOrEmpty(query.RawQuery.Trim()))
            {
                query.Search = IsActionKeywordQuery(query) ? query.RawQuery.Substring(query.RawQuery.IndexOf(' ') + 1) : query.RawQuery;
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

        /// <summary>
        /// Check if a query contains valid action keyword
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public static bool IsActionKeywordQuery(Query query)
        {
            if (string.IsNullOrEmpty(query.RawQuery)) return false;
            var strings = query.RawQuery.Split(' ');
            if (strings.Length == 1) return false;

            var actionKeyword = strings[0].Trim();
            if (string.IsNullOrEmpty(actionKeyword)) return false;

            PluginPair pair = plugins.FirstOrDefault(o => o.Metadata.ActionKeyword == actionKeyword);
            if (pair != null)
            {
                var customizedPluginConfig = UserSettingStorage.Instance.CustomizedPluginConfigs.FirstOrDefault(o => o.ID == pair.Metadata.ID);
                if (customizedPluginConfig != null && customizedPluginConfig.Disabled)
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        public static bool IsGenericPlugin(PluginMetadata metadata)
        {
            return metadata.ActionKeyword == ActionKeywordWildcardSign;
        }

        public static void ActivatePluginDebugger(string path)
        {
            DebuggerMode = path;
        }

        public static bool IsInstantQuery(string query)
        {
            return LoadInstantSearches().Any(o => o.Value.IsInstantQuery(query));
        }

        public static bool IsInstantSearchPlugin(PluginMetadata pluginMetadata)
        {
            //todo:to improve performance, any instant search plugin that takes long than 200ms will not consider a instant plugin anymore
            return pluginMetadata.Language.ToUpper() == AllowedLanguage.CSharp &&
                   LoadInstantSearches().Any(o => o.Key.Metadata.ID == pluginMetadata.ID);
        }

        internal static void ExecutePluginQuery(PluginPair pair, Query query)
        {
            try
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                List<Result> results = pair.Plugin.Query(query) ?? new List<Result>();
                results.ForEach(o =>
                {
                    o.PluginID = pair.Metadata.ID;
                });
                sw.Stop();
                DebugHelper.WriteLine(string.Format("Plugin query: {0} - {1}", pair.Metadata.Name, sw.ElapsedMilliseconds));
                pair.QueryCount += 1;
                if (pair.QueryCount == 1)
                {
                    pair.AvgQueryTime = sw.ElapsedMilliseconds;
                }
                else
                {
                    pair.AvgQueryTime = (pair.AvgQueryTime + sw.ElapsedMilliseconds) / 2;
                }
                API.PushResults(query, pair.Metadata, results);
            }
            catch (System.Exception e)
            {
                throw new WoxPluginException(pair.Metadata.Name, e);
            }
        }

        private static List<KeyValuePair<PluginPair, IInstantQuery>> LoadInstantSearches()
        {
            if (instantSearches != null) return instantSearches;

            instantSearches = AssemblyHelper.LoadPluginInterfaces<IInstantQuery>();

            return instantSearches;
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

        internal static List<KeyValuePair<PluginPair, IExclusiveQuery>> LoadExclusiveSearchPlugins()
        {
            if (exclusiveSearchPlugins != null) return exclusiveSearchPlugins;
            exclusiveSearchPlugins = AssemblyHelper.LoadPluginInterfaces<IExclusiveQuery>();
            return exclusiveSearchPlugins;
        }

        internal static PluginPair GetExclusivePlugin(Query query)
        {
            KeyValuePair<PluginPair, IExclusiveQuery> plugin = LoadExclusiveSearchPlugins().FirstOrDefault(o => o.Value.IsExclusiveQuery((query)));
            return plugin.Key;
        }

        internal static PluginPair GetActionKeywordPlugin(Query query)
        {
            //if a query doesn't contain at least one space, it should not be a action keword plugin query
            if (!query.RawQuery.Contains(" ")) return null;

            PluginPair actionKeywordPluginPair = AllPlugins.FirstOrDefault(o => o.Metadata.ActionKeyword == query.GetActionKeyword());
            if (actionKeywordPluginPair != null)
            {
                var customizedPluginConfig = UserSettingStorage.Instance.
                    CustomizedPluginConfigs.FirstOrDefault(o => o.ID == actionKeywordPluginPair.Metadata.ID);
                if (customizedPluginConfig != null && customizedPluginConfig.Disabled)
                {
                    return null;
                }

                return actionKeywordPluginPair;
            }

            return null;
        }

        internal static bool IsExclusivePluginQuery(Query query)
        {
            return GetExclusivePlugin(query) != null || GetActionKeywordPlugin(query) != null;
        }

        public static List<Result> GetPluginContextMenus(Result result)
        {
            List<Result> contextContextMenus = new List<Result>();
            if (contextMenuPlugins == null)
            {
                contextMenuPlugins = AssemblyHelper.LoadPluginInterfaces<IContextMenu>();
            }

            var contextMenuPlugin = contextMenuPlugins.FirstOrDefault(o => o.Key.Metadata.ID == result.PluginID);
            if (contextMenuPlugin.Value != null)
            {
                try
                {
                    return contextMenuPlugin.Value.LoadContextMenus(result);
                }
                catch (System.Exception e)
                {
                    Log.Error(string.Format("Couldn't load plugin context menus {0}: {1}", contextMenuPlugin.Key.Metadata.Name, e.Message));
#if (DEBUG)
                    {
                        throw;
                    }
#endif
                }
            }

            return contextContextMenus;
        }
    }
}
