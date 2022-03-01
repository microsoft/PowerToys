// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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

        public bool IsBootedInUefiMode { get; set; }

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

            var commands = ResultHelper.GetCommandList(IconTheme);
            var results = new List<Result>();

            foreach (var c in commands)
            {
                var resultMatch = StringMatcher.FuzzySearch(query.Search, c.SubTitle.ToString());
                if (resultMatch.Score > 0)
                {
                    c.Score = resultMatch.Score;
                    c.SubTitleHighlightData = resultMatch.MatchData;
                    results.Add(c);
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
