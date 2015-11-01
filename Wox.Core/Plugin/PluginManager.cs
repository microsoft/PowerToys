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
        private static List<PluginMetadata> pluginMetadatas;
        private static List<KeyValuePair<PluginPair, IInstantQuery>> instantSearches;
        private static IEnumerable<PluginPair> exclusiveSearchPlugins;
        private static List<KeyValuePair<PluginPair, IContextMenu>> contextMenuPlugins;

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
                    pair.Plugin.Init(new PluginInitContext
                    {
                        CurrentPluginMetadata = pair.Metadata,
                        Proxy = HttpProxy.Instance,
                        API = API
                    });
                    sw.Stop();
                    Debug.WriteLine(string.Format("Plugin init:{0} - {1}", pair.Metadata.Name, sw.ElapsedMilliseconds));
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

        public static void QueryForAllPlugins(Query query)
        {
            query.ActionKeyword = string.Empty;
            query.Search = query.RawQuery;
            if (query.Terms.Length == 0) return;
            if (IsVailldActionKeyword(query.Terms[0]))
            {
                query.ActionKeyword = query.Terms[0];
            }
            if (!string.IsNullOrEmpty(query.ActionKeyword))
            {
                query.Search = string.Join(Query.Seperater, query.Terms.Skip(1).ToArray());
            }
            QueryDispatch(query);
        }

        private static void QueryDispatch(Query query)
        {
            var nonSystemPlugin = GetNonSystemPlugin(query);
            var pluginPairs = nonSystemPlugin != null ? new List<PluginPair> { nonSystemPlugin } : GetSystemPlugins();
            foreach (var plugin in pluginPairs)
            {
                var customizedPluginConfig = UserSettingStorage.Instance.
                    CustomizedPluginConfigs.FirstOrDefault(o => o.ID == plugin.Metadata.ID);
                if (customizedPluginConfig != null && customizedPluginConfig.Disabled)
                {
                    return;
                }
                if (query.IsIntantQuery && IsInstantSearchPlugin(plugin.Metadata))
                {
                    Debug.WriteLine(string.Format("Plugin {0} is executing instant search.", plugin.Metadata.Name));
                    using (new Timeit("  => instant search took: "))
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
        /// <param name="actionKeyword"></param>
        /// <returns></returns>
        private static bool IsVailldActionKeyword(string actionKeyword)
        {
            if (string.IsNullOrEmpty(actionKeyword) || actionKeyword == Query.ActionKeywordWildcardSign) return false;
            PluginPair pair = plugins.FirstOrDefault(o => o.Metadata.ActionKeyword == actionKeyword);
            if (pair == null) return false;
            var customizedPluginConfig = UserSettingStorage.Instance.CustomizedPluginConfigs.FirstOrDefault(o => o.ID == pair.Metadata.ID);
            return customizedPluginConfig == null || !customizedPluginConfig.Disabled;
        }

        public static bool IsSystemPlugin(PluginMetadata metadata)
        {
            return metadata.ActionKeyword == Query.ActionKeywordWildcardSign;
        }

        public static bool IsInstantQuery(string query)
        {
            return LoadInstantSearches().Any(o => o.Value.IsInstantQuery(query));
        }

        private static bool IsInstantSearchPlugin(PluginMetadata pluginMetadata)
        {
            //todo:to improve performance, any instant search plugin that takes long than 200ms will not consider a instant plugin anymore
            return pluginMetadata.Language.ToUpper() == AllowedLanguage.CSharp &&
                   LoadInstantSearches().Any(o => o.Key.Metadata.ID == pluginMetadata.ID);
        }

        private static void QueryForPlugin(PluginPair pair, Query query)
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
                Debug.WriteLine(string.Format("Plugin query: {0} - {1}", pair.Metadata.Name, sw.ElapsedMilliseconds));
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
            return plugins.FirstOrDefault(o => o.Metadata.ID == id);
        }

        private static PluginPair GetExclusivePlugin(Query query)
        {
            exclusiveSearchPlugins = exclusiveSearchPlugins ??
                plugins.Where(p => p.Plugin.GetType().GetInterfaces().Contains(typeof(IExclusiveQuery)));
            var plugin = exclusiveSearchPlugins.FirstOrDefault(p => ((IExclusiveQuery)p.Plugin).IsExclusiveQuery(query));
            return plugin;
        }

        private static PluginPair GetActionKeywordPlugin(Query query)
        {
            //if a query doesn't contain a vaild action keyword, it should not be a action keword plugin query
            if (string.IsNullOrEmpty(query.ActionKeyword)) return null;
            return plugins.FirstOrDefault(o => o.Metadata.ActionKeyword == query.ActionKeyword);
        }

        private static PluginPair GetNonSystemPlugin(Query query)
        {
            return GetExclusivePlugin(query) ?? GetActionKeywordPlugin(query);
        }

        private static List<PluginPair> GetSystemPlugins()
        {
            return plugins.Where(o => IsSystemPlugin(o.Metadata)).ToList();
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
