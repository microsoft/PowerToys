using System;
using System.Collections.Generic;
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

namespace Wox.Core.Plugin
{
    /// <summary>
    /// The entry for managing Wox plugins
    /// </summary>
    public static class PluginManager
    {
        public const string DirectoryName = "Plugins";
        private static List<PluginMetadata> pluginMetadatas;
        private static IEnumerable<PluginPair> instantQueryPlugins;
        private static IEnumerable<PluginPair> exclusiveSearchPlugins;
        private static IEnumerable<PluginPair> contextMenuPlugins;
        private static List<PluginPair> plugins;

        /// <summary>
        /// Directories that will hold Wox plugin directory
        /// </summary>
        private static List<string> pluginDirectories = new List<string>();

        public static List<PluginPair> AllPlugins
        {
            get { return plugins; }
            private set { plugins = value.OrderBy(o => o.Metadata.Name).ToList(); }
        }

        public static IPublicAPI API { private set; get; }

        public static string PluginDirectory
        {
            get { return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), DirectoryName); }
        }

        private static void SetupPluginDirectories()
        {
            pluginDirectories.Add(PluginDirectory);
            MakesurePluginDirectoriesExist();
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
            AllPlugins?.Clear();

            pluginMetadatas = PluginConfig.Parse(pluginDirectories);
            AllPlugins = (new CSharpPluginLoader().LoadPlugin(pluginMetadatas)).
                Concat(new JsonRPCPluginLoader<PythonPlugin>().LoadPlugin(pluginMetadatas)).
                ToList();

            //load plugin i18n languages
            ResourceMerger.ApplyPluginLanguages();

            foreach (PluginPair pluginPair in AllPlugins)
            {
                PluginPair pair = pluginPair;
                ThreadPool.QueueUserWorkItem(o =>
                {
                    using (var time = new Timeit($"Plugin init: {pair.Metadata.Name}"))
                    {
                        pair.Plugin.Init(new PluginInitContext
                        {
                            CurrentPluginMetadata = pair.Metadata,
                            Proxy = HttpProxy.Instance,
                            API = API
                        });
                        pair.InitTime = time.Current;
                    }
                    InternationalizationManager.Instance.UpdatePluginMetadataTranslations(pair);
                });
            }

            ThreadPool.QueueUserWorkItem(o =>
            {
                GetInstantSearchesPlugins();
            });
        }

        public static void InstallPlugin(string path)
        {
            PluginInstaller.Install(path);
        }

        public static void QueryForAllPlugins(Query query)
        {
            query.ActionKeyword = String.Empty;
            query.Search = query.RawQuery;
            if (query.Terms.Length == 0) return;
            if (IsVailldActionKeyword(query.Terms[0]))
            {
                query.ActionKeyword = query.Terms[0];
            }
            if (!String.IsNullOrEmpty(query.ActionKeyword))
            {
                query.Search = String.Join(Query.Seperater, query.Terms.Skip(1).ToArray());
            }
            QueryDispatch(query);
        }

        private static void QueryDispatch(Query query)
        {
            var pluginPairs = GetNonSystemPlugin(query) != null ?
                new List<PluginPair> { GetNonSystemPlugin(query) } : GetSystemPlugins();
            foreach (var plugin in pluginPairs)
            {
                var customizedPluginConfig = UserSettingStorage.Instance.
                    CustomizedPluginConfigs.FirstOrDefault(o => o.ID == plugin.Metadata.ID);
                if (customizedPluginConfig != null && customizedPluginConfig.Disabled) return;
                if (IsInstantQueryPlugin(plugin))
                {
                    using (new Timeit($"Plugin {plugin.Metadata.Name} is executing instant search"))
                    {
                        QueryForPlugin(plugin, query);
                    }
                }
                else
                {
                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        QueryForPlugin(plugin, query);
                    });
                }
            }
        }

        private static void QueryForPlugin(PluginPair pair, Query query)
        {
            try
            {
                using (var time = new Timeit($"Query For {pair.Metadata.Name}"))
                {
                    var results = pair.Plugin.Query(query) ?? new List<Result>();
                    results.ForEach(o => { o.PluginID = pair.Metadata.ID; });
                    var seconds = time.Current;
                    pair.QueryCount += 1;
                    pair.AvgQueryTime = pair.QueryCount == 1 ? seconds : (pair.AvgQueryTime + seconds) / 2;
                    API.PushResults(query, pair.Metadata, results);
                }
            }
            catch (System.Exception e)
            {
                throw new WoxPluginException(pair.Metadata.Name, e);
            }
        }

        /// <summary>
        /// Check if a query contains valid action keyword
        /// </summary>
        /// <param name="actionKeyword"></param>
        /// <returns></returns>
        private static bool IsVailldActionKeyword(string actionKeyword)
        {
            if (String.IsNullOrEmpty(actionKeyword) || actionKeyword == Query.WildcardSign) return false;
            PluginPair pair = AllPlugins.FirstOrDefault(o => o.Metadata.ActionKeyword == actionKeyword);
            if (pair == null) return false;
            var customizedPluginConfig = UserSettingStorage.Instance.
                CustomizedPluginConfigs.FirstOrDefault(o => o.ID == pair.Metadata.ID);
            return customizedPluginConfig == null || !customizedPluginConfig.Disabled;
        }

        public static bool IsSystemPlugin(PluginMetadata metadata)
        {
            return metadata.ActionKeyword == Query.WildcardSign;
        }

        private static bool IsInstantQueryPlugin(PluginPair plugin)
        {
            //any plugin that takes more than 200ms for AvgQueryTime won't be treated as IInstantQuery plugin anymore.
            return plugin.AvgQueryTime < 200 &&
                   plugin.Metadata.Language.ToUpper() == AllowedLanguage.CSharp &&
                   GetInstantSearchesPlugins().Any(p => p.Metadata.ID == plugin.Metadata.ID);
        }

        private static IEnumerable<PluginPair> GetInstantSearchesPlugins()
        {
            instantQueryPlugins = instantQueryPlugins ?? GetPlugins<IInstantQuery>();
            return instantQueryPlugins;
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

        public static IEnumerable<PluginPair> GetPlugins<T>() where T : IFeatures
        {
            return AllPlugins.Where(p => p.Plugin is T);
        }

        private static PluginPair GetExclusivePlugin(Query query)
        {
            exclusiveSearchPlugins = exclusiveSearchPlugins ?? GetPlugins<IExclusiveQuery>();
            var plugin = exclusiveSearchPlugins.FirstOrDefault(p => ((IExclusiveQuery)p.Plugin).IsExclusiveQuery(query));
            return plugin;
        }

        private static PluginPair GetActionKeywordPlugin(Query query)
        {
            //if a query doesn't contain a vaild action keyword, it should not be a action keword plugin query
            if (string.IsNullOrEmpty(query.ActionKeyword)) return null;
            return AllPlugins.FirstOrDefault(o => o.Metadata.ActionKeyword == query.ActionKeyword);
        }

        private static PluginPair GetNonSystemPlugin(Query query)
        {
            return GetExclusivePlugin(query) ?? GetActionKeywordPlugin(query);
        }

        private static List<PluginPair> GetSystemPlugins()
        {
            return AllPlugins.Where(o => IsSystemPlugin(o.Metadata)).ToList();
        }

        public static List<Result> GetPluginContextMenus(Result result)
        {
            contextMenuPlugins = contextMenuPlugins ?? GetPlugins<IContextMenu>();

            var pluginPair = contextMenuPlugins.FirstOrDefault(o => o.Metadata.ID == result.PluginID);
            var plugin = (IContextMenu)pluginPair?.Plugin;
            if (plugin != null)
            {
                try
                {
                    return plugin.LoadContextMenus(result);
                }
                catch (System.Exception e)
                {
                    Log.Error($"Couldn't load plugin context menus {pluginPair.Metadata.Name}: {e.Message}");
#if (DEBUG)
                    {
                        throw;
                    }
#endif
                }
            }

            return new List<Result>();
        }
    }
}
