// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using ManagedCommon;
using Microsoft.PowerToys.Run.Plugin.TimeDate.Components;
using Microsoft.PowerToys.Run.Plugin.TimeDate.Properties;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.TimeDate
{
    public class Main : IPlugin, IPluginI18n, ISettingProvider
    {
        private PluginInitContext _context;

        public string IconTheme { get; set; }

        public string Name => Resources.Microsoft_plugin_timedate_plugin_name;

        public string Description => Resources.Microsoft_plugin_timedate_plugin_description;

        public IEnumerable<PluginAdditionalOption> AdditionalOptions
        {
            get
            {
                return TimeDateSettings.GetAdditionalOptions();
            }
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconTheme(_context.API.GetCurrentTheme());
        }

        public List<Result> Query(Query query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(paramName: nameof(query));
            }

            var results = new List<Result>();

            if (!string.IsNullOrEmpty(query.ActionKeyword) && string.IsNullOrWhiteSpace(query.Search))
            {
                // List all results on queries with only the keyword
                var commands = ResultHelper.GetCommandList(true);
                foreach (var c in commands)
                {
                    results.Add(new Result
                    {
                        Title = c.Value,
                        SubTitle = $"{c.Label} - {Resources.Microsoft_plugin_timedate_copyToClipboard}",
                        IcoPath = c.GetIconPath(IconTheme),
                        Action = _ => ResultHelper.CopyToClipBoard(c.Value),
                    });
                }
            }
            else if ((bool)query.Search.Any(char.IsDigit) && !query.Search.Contains("::"))
            {
                // List all results on queries with only a timestamp
                if (DateTime.TryParse(query.Search, out DateTime timestamp))
                {
                    var commands = ResultHelper.GetCommandList(true, null, null, timestamp);
                    foreach (var c in commands)
                    {
                        results.Add(new Result
                        {
                            Title = c.Value,
                            SubTitle = $"{c.Label} - {Resources.Microsoft_plugin_timedate_copyToClipboard}",
                            IcoPath = c.GetIconPath(IconTheme),
                            Action = _ => ResultHelper.CopyToClipBoard(c.Value),
                        });
                    }
                }
                else
                {
                    // Return empty list if date/time can't be parsed
                    return results;
                }
            }
            else
            {
                // Search for date/time value with system time/date or specified time/date
                List<AvailableResult> commands;
                string searchTerm;

                if ((bool)query.Search.Any(char.IsDigit) && query.Search.Contains("::"))
                {
                    string[] text = query.Search.Split("::");
                    if (DateTime.TryParse(text[1], out DateTime timestamp))
                    {
                        commands = ResultHelper.GetCommandList(!string.IsNullOrEmpty(query.ActionKeyword), null, null, timestamp);
                        searchTerm = text[0];
                    }
                    else
                    {
                        // Return empty list if date/time can't be parsed
                        return results;
                    }
                }
                else
                {
                    commands = ResultHelper.GetCommandList(!string.IsNullOrEmpty(query.ActionKeyword));
                    searchTerm = query.Search;
                }

                foreach (var c in commands)
                {
                    var resultMatch = StringMatcher.FuzzySearch(searchTerm, c.Label);
                    if (resultMatch.Score > 0)
                    {
                        results.Add(new Result
                        {
                            Title = c.Value,
                            SubTitle = $"{c.Label} - {Resources.Microsoft_plugin_timedate_copyToClipboard}",
                            IcoPath = c.GetIconPath(IconTheme),
                            Action = _ => ResultHelper.CopyToClipBoard(c.Value),
                            Score = resultMatch.Score,
                            SubTitleHighlightData = resultMatch.MatchData,
                        });
                    }
                }
            }

            return results;
        }

        private void UpdateIconTheme(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                IconTheme = "light";
            }
            else
            {
                IconTheme = "dark";
            }
        }

        private void OnThemeChanged(Theme currentTheme, Theme newTheme)
        {
            UpdateIconTheme(newTheme);
        }

        public string GetTranslatedPluginDescription()
        {
            return Resources.Microsoft_plugin_timedate_plugin_description;
        }

        public string GetTranslatedPluginTitle()
        {
            return Resources.Microsoft_plugin_timedate_plugin_name;
        }

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            TimeDateSettings.Instance.UpdateSettings(settings);
        }
    }
}
