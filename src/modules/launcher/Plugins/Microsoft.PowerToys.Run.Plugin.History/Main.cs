// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using ManagedCommon;
using Microsoft.PowerToys.Run.Plugin.History.Properties;
using PowerLauncher.Plugin;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.History
{
    public class Main : IPlugin, IContextMenu, IPluginI18n, IDisposable
    {
        private PluginInitContext Context { get; set; }

        private string IconPath { get; set; }

        public string Name => Resources.wox_plugin_history_plugin_name;

        public string Description => Resources.wox_plugin_history_plugin_description;

        public static string PluginID => "C88512156BB74580AADF7252E130BA8D";

        private bool _disposed;

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();
            try
            {
                if (query.SelectedItems != null)
                {
                    var scoreCounter = 1000;

                    // System.Diagnostics.Debugger.Launch();
                    foreach (var historyItem in query.SelectedItems.Values.OrderByDescending(sel => sel.LastSelected))
                    {
                        if (historyItem.PluginID == null)
                        {
                            continue;
                        }

                        var plugin = PluginManager.AllPlugins.FirstOrDefault(p => p.Metadata.ID == historyItem.PluginID);

                        if (query.Search != string.Empty && !IsRelevant(query, historyItem))
                        {
                            continue;
                        }

                        var result = BuildResult(historyItem);

                        if (result != null)
                        {
                            // very special case for Calculator
                            if (plugin.Metadata.Name == "Calculator")
                            {
                                result.HistoryTitle = result.Title;
                                result.Title = $"{historyItem.Search} = {historyItem.Title}";
                            }

                            if (query.RawQuery.StartsWith(query.ActionKeyword, StringComparison.InvariantCultureIgnoreCase))
                            {
                                // this is just the history view, update the scores.
                                result.Score = scoreCounter--;
                            }

                            results.Add(result);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Skipping " + historyItem.Title);
                        }
                    }

                    return results;
                }
            }
            catch (Exception e)
            {
                // System.Diagnostics.Debugger.Launch();
                bool isGlobalQuery = string.IsNullOrEmpty(query.ActionKeyword);
                return ErrorHandler.OnError(IconPath, isGlobalQuery, query.RawQuery, default, e);
            }

            return results;
        }

        private bool IsRelevant(Query query, UserSelectedRecord.UserSelectedRecordItem genericSelectedItem)
        {
            if (genericSelectedItem.Title != null && genericSelectedItem.Title.Contains(query.Search, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
            else if (genericSelectedItem.SubTitle != null && genericSelectedItem.SubTitle.Contains(query.Search, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
            else if (genericSelectedItem.Search != null && genericSelectedItem.Search.Contains(query.Search, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private Result BuildResult(UserSelectedRecord.UserSelectedRecordItem historyItem)
        {
            Result result = null;

            var plugin = PluginManager.AllPlugins.FirstOrDefault(x => x.Metadata.ID == historyItem.PluginID);

            var searchTerm = historyItem.Search;
            if (string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = historyItem.Title;
            }

            var tempResults = PluginManager.QueryForPlugin(plugin, new Query(searchTerm), false);

            if (tempResults != null)
            {
                result = tempResults.FirstOrDefault(r => r.Title == historyItem.Title && r.SubTitle == historyItem.SubTitle);

                if (result == null)
                {
                    // do less exact match, some plugins (like shell), have a dynamic SubTitle
                    result = tempResults.FirstOrDefault(r => r.Title == historyItem.Title);
                }
            }

            if (result == null)
            {
                tempResults = PluginManager.QueryForPlugin(plugin, new Query(searchTerm), true);
                if (tempResults != null)
                {
                    result = tempResults.FirstOrDefault(r => r.Title == historyItem.Title && r.SubTitle == historyItem.SubTitle);

                    if (result == null)
                    {
                        // do less exact match, some plugins (like shell), have a dynamic SubTitle
                        result = tempResults.FirstOrDefault(r => r.Title == historyItem.Title);
                    }
                }
            }

            if (result != null)
            {
                result.FromHistory = true;
                result.HistoryPluginID = historyItem.PluginID;
            }

            return result;
        }

        public void Init(PluginInitContext context)
        {
            Context = context ?? throw new ArgumentNullException(paramName: nameof(context));

            Context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(Context.API.GetCurrentTheme());
        }

        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                IconPath = "Images/history.light.png";
            }
            else
            {
                IconPath = "Images/history.dark.png";
            }
        }

        private void OnThemeChanged(Theme currentTheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        public string GetTranslatedPluginTitle()
        {
            return Resources.wox_plugin_history_plugin_name;
        }

        public string GetTranslatedPluginDescription()
        {
            return Resources.wox_plugin_history_plugin_description;
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            var pluginPair = PluginManager.AllPlugins.FirstOrDefault(x => x.Metadata.ID == selectedResult.HistoryPluginID);
            if (pluginPair != null)
            {
                List<ContextMenuResult> menuItems = new List<ContextMenuResult>();
                if (pluginPair.Plugin.GetType().GetInterface(nameof(IContextMenu)) != null)
                {
                    var plugin = (IContextMenu)pluginPair.Plugin;
                    menuItems = plugin.LoadContextMenus(selectedResult);
                }

                menuItems.Add(new ContextMenuResult
                {
                    // https://learn.microsoft.com/windows/apps/design/style/segoe-ui-symbol-font
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    Glyph = "\xF739",   // ECC9 => Symbol: RemoveFrom, or F739 => SetHistoryStatus2
                    Title = $"Remove this from history",
                    Action = _ =>
                    {
                        // very special case for Calculator
                        if (pluginPair.Plugin.Name == "Calculator")
                        {
                            selectedResult.Title = selectedResult.HistoryTitle;
                        }

                        PluginManager.API.RemoveUserSelectedItem(selectedResult);

                        return true;
                    },
                });

                return menuItems;
            }

            return new List<ContextMenuResult>();
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (Context != null && Context.API != null)
                    {
                        Context.API.ThemeChanged -= OnThemeChanged;
                    }

                    _disposed = true;
                }
            }
        }
    }
}
