// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using ControlzEx.Standard;
using ManagedCommon;
using Microsoft.PowerToys.Run.Plugin.System.Components;
using Microsoft.PowerToys.Run.Plugin.System.Properties;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Common.Win32;

namespace Microsoft.PowerToys.Run.Plugin.System
{
    public class Main : IPlugin, IPluginI18n, ISettingProvider, IContextMenu, IDelayedExecutionPlugin
    {
        private PluginInitContext _context;

        private bool _confirmSystemCommands;
        private bool _showSuccessOnEmptyRB;
        private bool _localizeSystemCommands;
        private bool _reduceNetworkResultScore;

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
                Key = "ShowSuccessOnEmptyRB",
                DisplayLabel = Resources.Microsoft_plugin_sys_RecycleBin_ShowEmptySuccessMessage,
                Value = false,
            },
            new PluginAdditionalOption()
            {
                Key = "LocalizeSystemCommands",
                DisplayLabel = Resources.Use_localized_system_commands,
                Value = true,
            },
            new PluginAdditionalOption()
            {
                Key = "ReduceNetworkResultScore",
                DisplayLabel = Resources.Reduce_Network_Result_Score,
                DisplayDescription = Resources.Reduce_Network_Result_Score_Description,
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
            List<Result> results = new List<Result>();
            CultureInfo culture = _localizeSystemCommands ? CultureInfo.CurrentUICulture : new CultureInfo("en-US");

            if (query == null)
            {
                return results;
            }

            // normal system commands are fast and can be returned immediately
            var systemCommands = Commands.GetSystemCommands(IsBootedInUefiMode, IconTheme, culture, _confirmSystemCommands);
            foreach (var c in systemCommands)
            {
                var resultMatch = StringMatcher.FuzzySearch(query.Search, c.Title);
                if (resultMatch.Score > 0)
                {
                    c.Score = resultMatch.Score;
                    c.TitleHighlightData = resultMatch.MatchData;
                    results.Add(c);
                }
                else if (c?.ContextData is SystemPluginContext contextData)
                {
                    var searchTagMatch = StringMatcher.FuzzySearch(query.Search, contextData.SearchTag);
                    if (searchTagMatch.Score > 0)
                    {
                        c.Score = resultMatch.Score;
                        results.Add(c);
                    }
                }
            }

            // The following information result is not returned because delayed queries doesn't clear output if no results are available.
            // On global queries the first word/part has to be 'ip', 'mac' or 'address' for network results
            // string[] keywordList = Resources.ResourceManager.GetString("Microsoft_plugin_sys_Search_NetworkKeywordList", culture).Split("; ");
            // if (!string.IsNullOrEmpty(query.ActionKeyword) || keywordList.Any(x => query.Search.StartsWith(x, StringComparison.CurrentCultureIgnoreCase)))
            // {
            //    results.Add(new Result()
            //    {
            //        Title = "Getting network informations. Please wait ...",
            //        IcoPath = $"Images\\networkAdapter.{IconTheme}.png",
            //        Score = StringMatcher.FuzzySearch("address", "ip address").Score,
            //    });
            // }
            return results;
        }

        public List<Result> Query(Query query, bool delayedExecution)
        {
            List<Result> results = new List<Result>();
            CultureInfo culture = _localizeSystemCommands ? CultureInfo.CurrentUICulture : new CultureInfo("en-US");

            if (query == null)
            {
                return results;
            }

            // Network (ip and mac) results are slow with many network cards and returned delayed.
            // On global queries the first word/part has to be 'ip', 'mac' or 'address' for network results
            string[] keywordList = Resources.ResourceManager.GetString("Microsoft_plugin_sys_Search_NetworkKeywordList", culture).Split("; ");
            if (!string.IsNullOrEmpty(query.ActionKeyword) || keywordList.Any(x => query.Search.StartsWith(x, StringComparison.CurrentCultureIgnoreCase)))
            {
                var networkConnectionResults = Commands.GetNetworkConnectionResults(IconTheme, culture);
                foreach (var r in networkConnectionResults)
                {
                    var resultMatch = StringMatcher.FuzzySearch(query.Search, r.SubTitle);
                    if (resultMatch.Score > 0)
                    {
                        r.Score = _reduceNetworkResultScore ? (int)(resultMatch.Score * 65 / 100) : resultMatch.Score; // Adjust score to improve user experience and priority order
                        r.SubTitleHighlightData = resultMatch.MatchData;
                        results.Add(r);
                    }
                    else if (r?.ContextData is SystemPluginContext contextData)
                    {
                        var searchTagMatch = StringMatcher.FuzzySearch(query.Search, contextData.SearchTag);
                        if (searchTagMatch.Score > 0)
                        {
                            r.Score = resultMatch.Score;
                            results.Add(r);
                        }
                    }
                }
            }

            return results;
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            return ResultHelper.GetContextMenuForResult(selectedResult, _showSuccessOnEmptyRB);
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
            var showSuccessOnEmptyRB = false;
            var localizeSystemCommands = true;
            var reduceNetworkResultScore = true;

            if (settings != null && settings.AdditionalOptions != null)
            {
                var optionConfirm = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "ConfirmSystemCommands");
                confirmSystemCommands = optionConfirm?.Value ?? confirmSystemCommands;

                var optionEmptyRBSuccessMsg = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "ShowSuccessOnEmptyRB");
                showSuccessOnEmptyRB = optionEmptyRBSuccessMsg?.Value ?? showSuccessOnEmptyRB;

                var optionLocalize = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "LocalizeSystemCommands");
                localizeSystemCommands = optionLocalize?.Value ?? localizeSystemCommands;

                var optionNetworkScore = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "ReduceNetworkResultScore");
                reduceNetworkResultScore = optionNetworkScore?.Value ?? reduceNetworkResultScore;
            }

            _confirmSystemCommands = confirmSystemCommands;
            _showSuccessOnEmptyRB = showSuccessOnEmptyRB;
            _localizeSystemCommands = localizeSystemCommands;
            _reduceNetworkResultScore = reduceNetworkResultScore;
        }
    }
}
