// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    public class Main : IPlugin, IPluginI18n, ISettingProvider, IContextMenu
    {
        private PluginInitContext _context;

        private bool _confirmSystemCommands;
        private bool _localizeSystemCommands;

        public string Name => Resources.Microsoft_plugin_sys_plugin_name;

        public string Description => Resources.Microsoft_plugin_sys_plugin_description;

        public string IconTheme { get; set; }

        public bool IsBootedInUefiMode { get; set; }

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new PluginAdditionalOption()
            {
                Key = "ConfirmSystemCommands",
                DisplayLabel = Resources.confirm_system_commands,
                Value = false,
            },
            new PluginAdditionalOption()
            {
                Key = "LocalizeSystemCommands",
                DisplayLabel = Resources.Use_localized_system_commands,
                Value = true,
            },
        };

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
            var results = new List<Result>();

            if (query == null)
            {
                return results;
            }

            CultureInfo culture = _localizeSystemCommands ? CultureInfo.CurrentUICulture : new CultureInfo("en-US");
            var systemCommands = Commands.GetSystemCommands(IsBootedInUefiMode, IconTheme, culture, _confirmSystemCommands);
            var networkConnectionResults = Commands.GetNetworkConnectionResults(IconTheme, culture);

            foreach (var c in systemCommands)
            {
                var resultMatch = StringMatcher.FuzzySearch(query.Search, c.Title);
                if (resultMatch.Score > 0)
                {
                    c.Score = resultMatch.Score;
                    c.TitleHighlightData = resultMatch.MatchData;
                    results.Add(c);
                }
            }

            foreach (var r in networkConnectionResults)
            {
                var resultMatch = StringMatcher.FuzzySearch(query.Search, r.SubTitle);
                if (resultMatch.Score > 0)
                {
                    r.Score = resultMatch.Score;
                    r.SubTitleHighlightData = resultMatch.MatchData;
                    results.Add(r);
                }
            }

            return results;
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            return ResultHelper.GetContextMenuForResult(selectedResult);
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
            var confirmSystemCommands = false;
            var localizeSystemCommands = true;

            if (settings != null && settings.AdditionalOptions != null)
            {
                var optionConfirm = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "ConfirmSystemCommands");
                confirmSystemCommands = optionConfirm?.Value ?? confirmSystemCommands;

                var optionLocalize = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "LocalizeSystemCommands");
                localizeSystemCommands = optionLocalize?.Value ?? localizeSystemCommands;
            }

            _confirmSystemCommands = confirmSystemCommands;
            _localizeSystemCommands = localizeSystemCommands;
        }
    }
}
