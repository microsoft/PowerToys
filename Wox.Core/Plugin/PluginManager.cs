using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Wox.Core.Resource;
using Wox.Core.UserSettings;
using Wox.Infrastructure;
using Wox.Infrastructure.Exception;
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
        private static IEnumerable<PluginPair> contextMenuPlugins;

        /// <summary>
        /// Directories that will hold Wox plugin directory
        /// </summary>
        private static List<string> pluginDirectories = new List<string>();

        public static IEnumerable<PluginPair> AllPlugins { get; private set; }

        public static List<PluginPair> GlobalPlugins { get; } = new List<PluginPair>();
        public static Dictionary<string, PluginPair> NonGlobalPlugins { get; set; } = new Dictionary<string, PluginPair>();

        private static IEnumerable<PluginPair> InstantQueryPlugins { get; set; }
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
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }
            }
        }

        /// <summary>
        /// Load and init all Wox plugins
        /// </summary>
        public static void Init(IPublicAPI api)
        {
            if (api == null) throw new WoxFatalException("api is null");

            SetupPluginDirectories();
            API = api;

            var metadatas = PluginConfig.Parse(pluginDirectories);
            AllPlugins = (new CSharpPluginLoader().LoadPlugin(metadatas)).
                Concat(new JsonRPCPluginLoader<PythonPlugin>().LoadPlugin(metadatas));

            //load plugin i18n languages
            ResourceMerger.UpdatePluginLanguages();

            foreach (PluginPair pluginPair in AllPlugins)
            {
                PluginPair pair = pluginPair;
                ThreadPool.QueueUserWorkItem(o =>
                {
                    var milliseconds = Stopwatch.Normal($"Plugin init: {pair.Metadata.Name}", () =>
                    {
                        pair.Plugin.Init(new PluginInitContext
                        {
                            CurrentPluginMetadata = pair.Metadata,
                            Proxy = HttpProxy.Instance,
                            API = API
                        });
                    });
                    pair.InitTime = milliseconds;
                    InternationalizationManager.Instance.UpdatePluginMetadataTranslations(pair);
                });
            }

            ThreadPool.QueueUserWorkItem(o =>
            {
                InstantQueryPlugins = GetPluginsForInterface<IInstantQuery>();
                contextMenuPlugins = GetPluginsForInterface<IContextMenu>();
                foreach (var plugin in AllPlugins)
                {
                    if (IsGlobalPlugin(plugin.Metadata))
                    {
                        GlobalPlugins.Add(plugin);
                    }
                    else
                    {
                        foreach (string actionKeyword in plugin.Metadata.ActionKeywords)
                        {
                            NonGlobalPlugins[actionKeyword] = plugin;
                        }
                    }
                }
            });
        }

        public static void InstallPlugin(string path)
        {
            PluginInstaller.Install(path);
        }

        public static Query QueryInit(string text) //todo is that possible to move it into type Query?
        {
            // replace multiple white spaces with one white space
            var terms = text.Split(new[] { Query.TermSeperater }, StringSplitOptions.RemoveEmptyEntries);
            var rawQuery = string.Join(Query.TermSeperater, terms);
            var actionKeyword = string.Empty;
            var search = rawQuery;
            List<string> actionParameters = terms.ToList();
            if (terms.Length == 0) return null;
            if (NonGlobalPlugins.ContainsKey(terms[0]))
            {
                actionKeyword = terms[0];
                actionParameters = terms.Skip(1).ToList();
                search = string.Join(Query.TermSeperater, actionParameters.ToArray());
            }
            return new Query
            {
                Terms = terms,
                RawQuery = rawQuery,
                ActionKeyword = actionKeyword,
                Search = search,
                // Obsolete value initialisation
                ActionName = actionKeyword,
                ActionParameters = actionParameters
            };
        }

        public static void QueryForAllPlugins(Query query)
        {
            var pluginPairs = NonGlobalPlugins.ContainsKey(query.ActionKeyword) ?
                new List<PluginPair> { NonGlobalPlugins[query.ActionKeyword] } : GlobalPlugins;
            foreach (var plugin in pluginPairs)
            {
                var customizedPluginConfig = UserSettingStorage.Instance.
                    CustomizedPluginConfigs.FirstOrDefault(o => o.ID == plugin.Metadata.ID);
                if (customizedPluginConfig != null && customizedPluginConfig.Disabled) continue;
                if (IsInstantQueryPlugin(plugin))
                {
                    Stopwatch.Normal($"Instant QueryForPlugin for {plugin.Metadata.Name}", () =>
                    {
                        QueryForPlugin(plugin, query);
                    });
                }
                else
                {
                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        Stopwatch.Normal($"Normal QueryForPlugin for {plugin.Metadata.Name}", () =>
                        {
                            QueryForPlugin(plugin, query);
                        });
                    });
                }
            }
        }

        private static void QueryForPlugin(PluginPair pair, Query query)
        {
            try
            {
                List<Result> results = new List<Result>();
                var milliseconds = Stopwatch.Normal($"Plugin.Query cost for {pair.Metadata.Name}", () =>
                    {
                        results = pair.Plugin.Query(query) ?? results;
                        results.ForEach(o => { o.PluginID = pair.Metadata.ID; });
                    });
                pair.QueryCount += 1;
                pair.AvgQueryTime = pair.QueryCount == 1 ? milliseconds : (pair.AvgQueryTime + milliseconds) / 2;
                API.PushResults(query, pair.Metadata, results);
            }
            catch (Exception e)
            {
                throw new WoxPluginException(pair.Metadata.Name, $"QueryForPlugin failed", e);
            }
        }

        private static bool IsGlobalPlugin(PluginMetadata metadata)
        {
            return metadata.ActionKeywords.Contains(Query.GlobalPluginWildcardSign);
        }

        private static bool IsInstantQueryPlugin(PluginPair plugin)
        {
            //any plugin that takes more than 200ms for AvgQueryTime won't be treated as IInstantQuery plugin anymore.
            return plugin.AvgQueryTime < 200 &&
                   plugin.Plugin is IInstantQuery &&
                   InstantQueryPlugins.Any(p => p.Metadata.ID == plugin.Metadata.ID);
        }

        /// <summary>
        /// get specified plugin, return null if not found
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static PluginPair GetPluginForId(string id)
        {
            return AllPlugins.FirstOrDefault(o => o.Metadata.ID == id);
        }

        public static IEnumerable<PluginPair> GetPluginsForInterface<T>() where T : IFeatures
        {
            return AllPlugins.Where(p => p.Plugin is T);
        }

        public static List<Result> GetContextMenusForPlugin(Result result)
        {
            var pluginPair = contextMenuPlugins.FirstOrDefault(o => o.Metadata.ID == result.PluginID);
            var plugin = (IContextMenu)pluginPair?.Plugin;
            if (plugin != null)
            {
                try
                {
                    return plugin.LoadContextMenus(result);
                }
                catch (Exception e)
                {
                    Log.Error(new WoxPluginException(pluginPair.Metadata.Name, $"Couldn't load plugin context menus", e));
                }
            }

            return new List<Result>();
        }

        public static void UpdateActionKeywordForPlugin(PluginPair plugin, string oldActionKeyword, string newActionKeyword)
        {
            var actionKeywords = plugin.Metadata.ActionKeywords;
            if (string.IsNullOrEmpty(newActionKeyword))
            {
                string msg = InternationalizationManager.Instance.GetTranslation("newActionKeywordsCannotBeEmpty");
                throw new WoxPluginException(plugin.Metadata.Name, msg);
            }
            // do nothing if they are same
            if (oldActionKeyword == newActionKeyword) return;
            if (NonGlobalPlugins.ContainsKey(newActionKeyword))
            {
                string msg = InternationalizationManager.Instance.GetTranslation("newActionKeywordsHasBeenAssigned");
                throw new WoxPluginException(plugin.Metadata.Name, msg);
            }

            // add new action keyword
            if (string.IsNullOrEmpty(oldActionKeyword))
            {
                actionKeywords.Add(newActionKeyword);
                if (newActionKeyword == Query.GlobalPluginWildcardSign)
                {
                    GlobalPlugins.Add(plugin);
                }
                else
                {
                    NonGlobalPlugins[newActionKeyword] = plugin;
                }
            }
            // update existing action keyword
            else
            {
                int index = actionKeywords.IndexOf(oldActionKeyword);
                actionKeywords[index] = newActionKeyword;
                if (oldActionKeyword == Query.GlobalPluginWildcardSign)
                {
                    GlobalPlugins.Remove(plugin);
                }
                else
                {
                    NonGlobalPlugins.Remove(oldActionKeyword);
                }
                if (newActionKeyword == Query.GlobalPluginWildcardSign)
                {
                    GlobalPlugins.Add(plugin);
                }
                else
                {
                    NonGlobalPlugins[newActionKeyword] = plugin;
                }
            }

        }

    }
}
