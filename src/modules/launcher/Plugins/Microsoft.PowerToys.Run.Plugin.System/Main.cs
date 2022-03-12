// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ManagedCommon;
using Microsoft.PowerToys.Run.Plugin.System.Components;
using Microsoft.PowerToys.Run.Plugin.System.Properties;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Common.Win32;

namespace Microsoft.PowerToys.Run.Plugin.System
{
    public class Main : IPlugin, IPluginI18n, ISettingProvider
    {
        private PluginInitContext _context;

        public string Name => Resources.Microsoft_plugin_sys_plugin_name;

        public string Description => Resources.Microsoft_plugin_sys_plugin_description;

        public string IconTheme { get; set; }

        public bool IsBootedInUefiMode { get; set; }

        public IEnumerable<PluginAdditionalOption> AdditionalOptions
        {
            get
            {
                return SystemPluginSettings.GetAdditionalOptions();
            }
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconTheme(_context.API.GetCurrentTheme());
            IsBootedInUefiMode = Win32Helpers.GetSystemFirmwareType() == FirmwareType.Uefi;

            // Log info if the system hasn't boot in uefi mode.
            // (Because this is only going into the log we can ignore the fact that normally UEFI and BIOS are written upper case. No need to convert the enumeration value to upper case.)
            if (!IsBootedInUefiMode)
            {
                Wox.Plugin.Logger.Log.Info($"The UEFI command will not show to the user. The system has not booted in UEFI mode or the system does not have an UEFI firmware! (Detected type: {Win32Helpers.GetSystemFirmwareType()})", typeof(Main));
            }
        }

        public List<Result> Query(Query query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(paramName: nameof(query));
            }

            var commands = Commands.GetSystemCommands(IconTheme, IsBootedInUefiMode);
            var addresses = Commands.GetNetworkAdapterAdresses(IconTheme);
            var netCommands = Commands.GetNetworkCommands(query.SecondToEndSearch, IconTheme);
            var results = new List<Result>();

            foreach (var c in commands)
            {
                var resultMatch = StringMatcher.FuzzySearch(query.Search, c.Title);
                if (resultMatch.Score > 0)
                {
                    c.Score = resultMatch.Score;
                    c.TitleHighlightData = resultMatch.MatchData;
                    results.Add(c);
                }
            }

            foreach (var c in addresses)
            {
                var resultMatch = StringMatcher.FuzzySearch(query.Search, c.SubTitle);
                if (resultMatch.Score > 0)
                {
                    c.Score = resultMatch.Score;
                    c.SubTitleHighlightData = resultMatch.MatchData;
                    results.Add(c);
                }
            }

            foreach (var c in netCommands)
            {
                var resultMatch = StringMatcher.FuzzySearch(query.Search, c.Title);
                if (resultMatch.Score > 0)
                {
                    c.Score = resultMatch.Score;
                    c.TitleHighlightData = resultMatch.MatchData;
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
            return Resources.Microsoft_plugin_sys_plugin_description;
        }

        public string GetTranslatedPluginTitle()
        {
            return Resources.Microsoft_plugin_sys_plugin_name;
        }

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            SystemPluginSettings.Instance.UpdateSettings(settings);
        }
    }
}
