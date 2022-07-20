// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using ManagedCommon;
using Microsoft.PowerToys.Run.Plugin.History.Properties;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerLauncher.Plugin;
using PowerLauncher.Telemetry.Events;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.History
{
    public class Main : IPlugin, IPluginI18n, IDisposable, ISettingProvider
    {
        private PluginInitContext Context { get; set; }

        private string IconPath { get; set; }

        private bool _inputUseEnglishFormat;
        private bool _outputUseEnglishFormat;

        public string Name => Resources.wox_plugin_history_plugin_name;

        public string Description => Resources.wox_plugin_history_plugin_description;

        private bool _disposed;

        public IEnumerable<PluginAdditionalOption> AdditionalOptions
        {
            get
            {
                return new List<PluginAdditionalOption>();
            }
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();
            try
            {
                if (query.GenericHistory != null)
                {
                    // System.Diagnostics.Debugger.Launch();
                    foreach (var historyItem in query.GenericHistory.SelectedItems)
                    {
                        var plugin = PluginManager.AllPlugins.FirstOrDefault(p => p.Metadata.ID == historyItem.PluginID);

                        if (query.Search != string.Empty && !IsRelevant(query, historyItem))
                        {
                            continue;
                        }

                        var result = BuildResult(historyItem);

                        if (result.Action != null)
                        {
                            if (plugin.Metadata.Name == "Calculator")
                            {
                                result.Title = $"{historyItem.Search} = {historyItem.Title}";
                            }

                            // System.Diagnostics.Debugger.Launch();
                            results.Add(result);
                        }
                        else
                        {
                            // System.Diagnostics.Debugger.Launch();
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

        private bool IsRelevant(Query query, GenericSelectedItem genericSelectedItem)
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

        private Result BuildResult(GenericSelectedItem historyItem)
        {
            var result = new Result
            {
                Title = historyItem.Title,
                IcoPath = historyItem.IconPath,
                Score = historyItem.Score,
                SubTitle = historyItem.SubTitle,
            };

            var plugin = PluginManager.AllPlugins.FirstOrDefault(x => x.Metadata.ID == historyItem.PluginID);

            var tempResults = PluginManager.QueryForPlugin(plugin, new Query(historyItem.Title), true);

            if (tempResults != null && tempResults.Any(r => r.IcoPath == result.IcoPath && r.SubTitle == result.SubTitle))
            {
                result.Action = tempResults.First(r => r.IcoPath == result.IcoPath && r.SubTitle == result.SubTitle).Action;
            }

            tempResults = PluginManager.QueryForPlugin(plugin, new Query(historyItem.Title), false);

            if (tempResults != null && tempResults.Any(r => r.IcoPath == result.IcoPath && r.SubTitle == result.SubTitle))
            {
                result.Action = tempResults.First(r => r.IcoPath == result.IcoPath && r.SubTitle == result.SubTitle).Action;
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

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            var inputUseEnglishFormat = false;
            var outputUseEnglishFormat = false;

            if (settings != null && settings.AdditionalOptions != null)
            {
                var optionInputEn = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "InputUseEnglishFormat");
                inputUseEnglishFormat = optionInputEn?.Value ?? inputUseEnglishFormat;

                var optionOutputEn = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "OutputUseEnglishFormat");
                outputUseEnglishFormat = optionOutputEn?.Value ?? outputUseEnglishFormat;
            }

            _inputUseEnglishFormat = inputUseEnglishFormat;
            _outputUseEnglishFormat = outputUseEnglishFormat;
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
