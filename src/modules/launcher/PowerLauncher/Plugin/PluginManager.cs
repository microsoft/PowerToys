// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Wox.Infrastructure;
using Wox.Infrastructure.Storage;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace PowerLauncher.Plugin
{
    /// <summary>
    /// The entry for managing Wox plugins
    /// </summary>
    public static class PluginManager
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IDirectory Directory = FileSystem.Directory;

        private static IEnumerable<PluginPair> _contextMenuPlugins = new List<PluginPair>();

        private static List<PluginPair> _allPlugins;

        // should be only used in tests
        public static void SetAllPlugins(List<PluginPair> plugins)
        {
            _allPlugins = plugins;
        }

        /// <summary>
        /// Gets directories that will hold Wox plugin directory
        /// </summary>
        public static List<PluginPair> AllPlugins
        {
            get
            {
                if (_allPlugins == null)
                {
                    _allPlugins = PluginsLoader.Plugins(PluginConfig.Parse(Directories));
                }

                return _allPlugins;
            }
        }

        public static IPublicAPI API { get; private set; }

        public static List<PluginPair> GlobalPlugins
        {
            get
            {
                return AllPlugins.Where(x => x.Metadata.IsGlobal).ToList();
            }
        }

        public static IEnumerable<PluginPair> NonGlobalPlugins
        {
            get
            {
                return AllPlugins.Where(x => !string.IsNullOrWhiteSpace(x.Metadata.ActionKeyword));
            }
        }

        private static readonly string[] Directories = { Constant.PreinstalledDirectory, Constant.PluginsDirectory };

        private static void ValidateUserDirectory()
        {
            if (!Directory.Exists(Constant.PluginsDirectory))
            {
                Directory.CreateDirectory(Constant.PluginsDirectory);
            }
        }

        public static void Save()
        {
            foreach (var plugin in AllPlugins)
            {
                var savable = plugin.Plugin as ISavable;
                savable?.Save();
            }
        }

        public static void ReloadData()
        {
            foreach (var plugin in AllPlugins)
            {
                var reloadablePlugin = plugin.Plugin as IReloadable;
                reloadablePlugin?.ReloadData();
            }
        }

        static PluginManager()
        {
            ValidateUserDirectory();
        }

        /// <summary>
        /// Call initialize for all plugins
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Suppressing this to enable FxCop. We are logging the exception, and going forward general exceptions should not be caught")]
        public static void InitializePlugins(IPublicAPI api)
        {
            API = api ?? throw new ArgumentNullException(nameof(api));
            var failedPlugins = new ConcurrentQueue<PluginPair>();
            Parallel.ForEach(AllPlugins, pair =>
            {
                try
                {
                    var milliseconds = Stopwatch.Debug($"PluginManager.InitializePlugins - Init method time cost for <{pair.Metadata.Name}>", () =>
                    {
                        pair.Plugin.Init(new PluginInitContext
                        {
                            CurrentPluginMetadata = pair.Metadata,
                            API = API,
                        });
                    });
                    pair.Metadata.InitTime += milliseconds;
                    Log.Info($"Total init cost for <{pair.Metadata.Name}> is <{pair.Metadata.InitTime}ms>", MethodBase.GetCurrentMethod().DeclaringType);
                }
                catch (Exception e)
                {
                    Log.Exception($"Fail to Init plugin: {pair.Metadata.Name}", e, MethodBase.GetCurrentMethod().DeclaringType);
                    pair.Metadata.Disabled = true;
                    failedPlugins.Enqueue(pair);
                }
            });

            _contextMenuPlugins = GetPluginsForInterface<IContextMenu>();

            if (failedPlugins.Any())
            {
                var failed = string.Join(",", failedPlugins.Select(x => x.Metadata.Name));
                API.ShowMsg($"Fail to Init Plugins", $"Plugins: {failed} - fail to load and would be disabled, please contact plugin creator for help", string.Empty, false);
            }
        }

        public static void InstallPlugin(string path)
        {
            PluginInstaller.Install(path);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Suppressing this to enable FxCop. We are logging the exception, and going forward general exceptions should not be caught")]
        public static List<Result> QueryForPlugin(PluginPair pair, Query query, bool delayedExecution = false)
        {
            if (pair == null)
            {
                throw new ArgumentNullException(nameof(pair));
            }

            try
            {
                List<Result> results = null;
                var metadata = pair.Metadata;
                var milliseconds = Stopwatch.Debug($"PluginManager.QueryForPlugin - Cost for {metadata.Name}", () =>
                {
                    if (delayedExecution && (pair.Plugin is IDelayedExecutionPlugin))
                    {
                        results = ((IDelayedExecutionPlugin)pair.Plugin).Query(query, delayedExecution) ?? new List<Result>();
                    }
                    else if (!delayedExecution)
                    {
                        results = pair.Plugin.Query(query) ?? new List<Result>();
                    }

                    if (results != null)
                    {
                        UpdatePluginMetadata(results, metadata, query);
                        UpdateResultWithActionKeyword(results, query);
                    }
                });

                metadata.QueryCount += 1;
                metadata.AvgQueryTime = metadata.QueryCount == 1 ? milliseconds : (metadata.AvgQueryTime + milliseconds) / 2;

                return results;
            }
            catch (Exception e)
            {
                Log.Exception($"Exception for plugin <{pair.Metadata.Name}> when query <{query}>", e, MethodBase.GetCurrentMethod().DeclaringType);

                return new List<Result>();
            }
        }

        private static List<Result> UpdateResultWithActionKeyword(List<Result> results, Query query)
        {
            foreach (Result result in results)
            {
                if (string.IsNullOrEmpty(result.QueryTextDisplay))
                {
                    result.QueryTextDisplay = result.Title;
                }

                if (!string.IsNullOrEmpty(query.ActionKeyword))
                {
                    // Using CurrentCulture since this is user facing
                    result.QueryTextDisplay = string.Format(CultureInfo.CurrentCulture, "{0} {1}", query.ActionKeyword, result.QueryTextDisplay);
                }
            }

            return results;
        }

        public static void UpdatePluginMetadata(List<Result> results, PluginMetadata metadata, Query query)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            foreach (var r in results)
            {
                r.PluginDirectory = metadata.PluginDirectory;
                r.PluginID = metadata.ID;
                r.OriginQuery = query;
            }
        }

        /// <summary>
        /// get specified plugin, return null if not found
        /// </summary>
        /// <param name="id">id of plugin</param>
        /// <returns>plugin</returns>
        public static PluginPair GetPluginForId(string id)
        {
            return AllPlugins.FirstOrDefault(o => o.Metadata.ID == id);
        }

        public static IEnumerable<PluginPair> GetPluginsForInterface<T>()
        {
            return AllPlugins.Where(p => p.Plugin is T);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Suppressing this to enable FxCop. We are logging the exception, and going forward general exceptions should not be caught")]
        public static List<ContextMenuResult> GetContextMenusForPlugin(Result result)
        {
            var pluginPair = _contextMenuPlugins.FirstOrDefault(o => o.Metadata.ID == result.PluginID);
            if (pluginPair != null)
            {
                var metadata = pluginPair.Metadata;
                var plugin = (IContextMenu)pluginPair.Plugin;

                try
                {
                    var results = plugin.LoadContextMenus(result);

                    return results;
                }
                catch (Exception e)
                {
                    Log.Exception($"Can't load context menus for plugin <{metadata.Name}>", e, MethodBase.GetCurrentMethod().DeclaringType);

                    return new List<ContextMenuResult>();
                }
            }
            else
            {
                return new List<ContextMenuResult>();
            }
        }

        public static void Dispose()
        {
            foreach (var plugin in AllPlugins)
            {
                var disposablePlugin = plugin.Plugin as IDisposable;
                disposablePlugin?.Dispose();
            }
        }
    }
}
