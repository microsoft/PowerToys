// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using global::PowerToys.GPOWrapper;
using PowerLauncher.Properties;
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
        private static readonly object AllPluginsLock = new object();

        private static readonly CompositeFormat FailedToInitializePluginsDescription = System.Text.CompositeFormat.Parse(Properties.Resources.FailedToInitializePluginsDescription);

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
                    lock (AllPluginsLock)
                    {
                        if (_allPlugins == null)
                        {
                            _allPlugins = PluginConfig.Parse(Directories)
                                .Where(x => string.Equals(x.Language, AllowedLanguage.CSharp, StringComparison.OrdinalIgnoreCase))
                                .GroupBy(x => x.ID) // Deduplicates plugins by ID, choosing for each ID the highest DLL product version. This fixes issues such as https://github.com/microsoft/PowerToys/issues/14701
                                .Select(g => g.OrderByDescending(x => // , where an upgrade didn't remove older versions of the plugins.
                                {
                                    try
                                    {
                                        // Return a comparable product version.
                                        var fileVersion = FileVersionInfo.GetVersionInfo(x.ExecuteFilePath);

                                        // Convert each part to an unsigned 32 bit integer, then extend to 64 bit.
                                        return ((ulong)(uint)fileVersion.ProductMajorPart << 48)
                                            | ((ulong)(uint)fileVersion.ProductMinorPart << 32)
                                            | ((ulong)(uint)fileVersion.ProductBuildPart << 16)
                                            | (ulong)(uint)fileVersion.ProductPrivatePart;
                                    }
                                    catch (System.IO.FileNotFoundException)
                                    {
                                        // We'll get an error when loading the DLL later on if there's not a decent version of this plugin.
                                        return 0U;
                                    }
                                }).First())
                                .Select(x => new PluginPair(x))
                                .ToList();
                        }
                    }
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
        public static void InitializePlugins(IPublicAPI api)
        {
            API = api ?? throw new ArgumentNullException(nameof(api));
            var failedPlugins = new ConcurrentQueue<PluginPair>();
            Parallel.ForEach(AllPlugins, pair =>
            {
                // Check policy state for the plugin and update metadata
                var enabledPolicyState = GPOWrapper.GetRunPluginEnabledValue(pair.Metadata.ID);
                if (enabledPolicyState == GpoRuleConfigured.Enabled)
                {
                    pair.Metadata.Disabled = false;
                    pair.Metadata.IsEnabledPolicyConfigured = true;
                    Log.Info($"The plugin <{pair.Metadata.Name}> is enabled by policy.", typeof(PluginManager));
                }
                else if (enabledPolicyState == GpoRuleConfigured.Disabled)
                {
                    pair.Metadata.Disabled = true;
                    pair.Metadata.IsEnabledPolicyConfigured = true;
                    Log.Info($"The plugin <{pair.Metadata.Name}> is disabled by policy.", typeof(PluginManager));
                }
                else if (enabledPolicyState == GpoRuleConfigured.WrongValue)
                {
                    Log.Warn($"Wrong policy value for enabled policy for plugin <{pair.Metadata.Name}>.", typeof(PluginManager));
                }

                if (pair.Metadata.Disabled)
                {
                    return;
                }

                pair.InitializePlugin(API);

                if (!pair.IsPluginInitialized)
                {
                    failedPlugins.Enqueue(pair);
                }
            });

            _contextMenuPlugins = GetPluginsForInterface<IContextMenu>();

            if (!failedPlugins.IsEmpty)
            {
                var failed = string.Join(",", failedPlugins.Select(x => x.Metadata.Name));
                var description = string.Format(CultureInfo.CurrentCulture, FailedToInitializePluginsDescription, failed);
                Application.Current.Dispatcher.InvokeAsync(() => API.ShowMsg(Resources.FailedToInitializePluginsTitle, description, string.Empty, false));
            }
        }

        public static List<Result> QueryForPlugin(PluginPair pair, Query query, bool delayedExecution = false)
        {
            ArgumentNullException.ThrowIfNull(pair);

            if (!pair.IsPluginInitialized)
            {
                return new List<Result>();
            }

            if (string.IsNullOrEmpty(query.ActionKeyword) && string.IsNullOrWhiteSpace(query.Search))
            {
                return new List<Result>();
            }

            try
            {
                List<Result> results = null;
                var metadata = pair.Metadata;
                var milliseconds = Wox.Infrastructure.Stopwatch.Debug($"PluginManager.QueryForPlugin - Cost for {metadata.Name}", () =>
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
                        UpdateResults(results, metadata, query);
                    }
                });

                if (milliseconds > 50)
                {
                    Log.Warn($"PluginManager.QueryForPlugin {metadata.Name}. Query cost - {milliseconds} milliseconds", typeof(PluginManager));
                }

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

        private static void UpdateResults(List<Result> results, PluginMetadata metadata, Query query)
        {
            foreach (Result result in results)
            {
                result.PluginDirectory = metadata.PluginDirectory;
                result.PluginID = metadata.ID;
                result.OriginQuery = query;
                result.Metadata = metadata;

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
        }

        public static void UpdatePluginMetadata(List<Result> results, PluginMetadata metadata, Query query)
        {
            ArgumentNullException.ThrowIfNull(results);

            ArgumentNullException.ThrowIfNull(metadata);

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
