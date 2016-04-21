using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private static IEnumerable<PluginPair> _contextMenuPlugins;

        /// <summary>
        /// Directories that will hold Wox plugin directory
        /// </summary>
        private static readonly List<string> PluginDirectories = new List<string>();

        public static List<PluginPair> AllPlugins { get; private set; }

        public static readonly List<PluginPair> GlobalPlugins = new List<PluginPair>();

        public static readonly Dictionary<string, PluginPair> NonGlobalPlugins = new Dictionary<string, PluginPair>();

        private static IEnumerable<PluginPair> InstantQueryPlugins { get; set; }
        public static IPublicAPI API { private set; get; }

        public static readonly string PluginDirectory = Path.Combine(WoxDirectroy.Executable, DirectoryName);

        private static void SetupPluginDirectories()
        {
            PluginDirectories.Add(PluginDirectory);
            MakesurePluginDirectoriesExist();
        }

        private static void MakesurePluginDirectoriesExist()
        {
            foreach (string pluginDirectory in PluginDirectories)
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
        /// 
        public static void Initialize()
        {
            SetupPluginDirectories();

            var metadatas = PluginConfig.Parse(PluginDirectories);
            AllPlugins = new CSharpPluginLoader().LoadPlugin(metadatas).Concat(
                         new JsonRPCPluginLoader<PythonPlugin>().LoadPlugin(metadatas)).ToList();
        }

        public static void InitializePlugins(IPublicAPI api)
        {
            //load plugin i18n languages
            ResourceMerger.UpdatePluginLanguages();

            API = api;
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
                _contextMenuPlugins = GetPluginsForInterface<IContextMenu>();
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

        public static List<PluginPair> ValidPluginsForQuery(Query query)
        {
            if (NonGlobalPlugins.ContainsKey(query.ActionKeyword))
            {
                var plugin = NonGlobalPlugins[query.ActionKeyword];
                return new List<PluginPair> { plugin };
            }
            else
            {
                return GlobalPlugins;
            }
        }

        //happlebao todo prevent plugin initial when plugin is disabled
        public static void DisablePlugin(PluginPair plugin)
        {
            var actionKeywords = plugin.Metadata.ActionKeywords;
            if (actionKeywords == null || actionKeywords.Count == 0 || actionKeywords[0] == Query.GlobalPluginWildcardSign)
            {
                GlobalPlugins.Remove(plugin);
            }
            else
            {
                foreach (var actionkeyword in plugin.Metadata.ActionKeywords)
                {
                    NonGlobalPlugins.Remove(actionkeyword);
                }
            }
            AllPlugins.Remove(plugin);
        }

        public static void EnablePlugin(PluginPair plugin)
        {
            var actionKeywords = plugin.Metadata.ActionKeywords;
            if (actionKeywords == null || actionKeywords.Count == 0 || actionKeywords[0] == Query.GlobalPluginWildcardSign)
            {
                GlobalPlugins.Add(plugin);
            }
            else
            {
                foreach (var actionkeyword in plugin.Metadata.ActionKeywords)
                {
                    NonGlobalPlugins[actionkeyword] = plugin;
                }
            }
            AllPlugins.Add(plugin);
        }

        public static List<Result> QueryForPlugin(PluginPair pair, Query query)
        {
            var results = new List<Result>();
            try
            {
                var milliseconds = Stopwatch.Normal($"Plugin.Query cost for {pair.Metadata.Name}", () =>
                    {
                        results = pair.Plugin.Query(query) ?? results;
                        results.ForEach(o =>
                        {
                            o.PluginDirectory = pair.Metadata.PluginDirectory;
                            o.PluginID = pair.Metadata.ID;
                            o.OriginQuery = query;
                        });
                    });
                pair.QueryCount += 1;
                pair.AvgQueryTime = pair.QueryCount == 1 ? milliseconds : (pair.AvgQueryTime + milliseconds) / 2;
            }
            catch (Exception e)
            {
                throw new WoxPluginException(pair.Metadata.Name, "QueryForPlugin failed", e);
            }
            return results;
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
            var pluginPair = _contextMenuPlugins.FirstOrDefault(o => o.Metadata.ID == result.PluginID);
            if (pluginPair != null)
            {
                var metadata = pluginPair.Metadata;
                var plugin = (IContextMenu)pluginPair?.Plugin;

                try
                {
                    var results = plugin.LoadContextMenus(result);
                    foreach (var r in results)
                    {
                        r.PluginDirectory = metadata.PluginDirectory;
                        r.PluginID = metadata.ID;
                        r.OriginQuery = result.OriginQuery;
                    }
                    return results;
                }
                catch (Exception e)
                {
                    Log.Error(new WoxPluginException(metadata.Name, "Couldn't load plugin context menus", e));
                    return new List<Result>();
                }
            }
            else
            {
                return new List<Result>();
            }

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
