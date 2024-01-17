// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.PowerToys.Run.Plugin.WindowsTerminal.Helpers;
using Microsoft.PowerToys.Run.Plugin.WindowsTerminal.Properties;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.WindowsTerminal
{
    public class Main : IPlugin, IContextMenu, IPluginI18n, ISettingProvider
    {
        private const string OpenNewTab = nameof(OpenNewTab);
        private const string OpenQuake = nameof(OpenQuake);
        private const string ShowHiddenProfiles = nameof(ShowHiddenProfiles);
        private readonly TerminalQuery _terminalQuery = new TerminalQuery();
        private PluginInitContext _context;
        private bool _openNewTab;
        private bool _openQuake;
        private bool _showHiddenProfiles;
        private Dictionary<string, BitmapImage> _logoCache = new Dictionary<string, BitmapImage>();

        public string Name => Resources.plugin_name;

        public string Description => Resources.plugin_description;

        public static string PluginID => "F59BA85006B14389A72A0EA756695F1D";

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new PluginAdditionalOption()
            {
                Key = OpenNewTab,
                DisplayLabel = Resources.open_new_tab,
                Value = false,
            },

            new PluginAdditionalOption()
            {
                Key = OpenQuake,
                DisplayLabel = Resources.open_quake,
                DisplayDescription = Resources.open_quake_description,
                Value = false,
            },

            new PluginAdditionalOption()
            {
                Key = ShowHiddenProfiles,
                DisplayLabel = Resources.show_hidden_profiles,
                Value = false,
            },
        };

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

        public List<Result> Query(Query query)
        {
            var search = query?.Search ?? string.Empty;
            var profiles = _terminalQuery.GetProfiles();

            var result = new List<Result>();

            foreach (var profile in profiles)
            {
                if (profile.Hidden && !_showHiddenProfiles)
                {
                    continue;
                }

                // Action keyword only or search query match
                int score = StringMatcher.FuzzySearch(search, profile.Name).Score;
                if (string.IsNullOrWhiteSpace(search) || score > 0)
                {
                    result.Add(new Result
                    {
                        Title = profile.Name,
                        SubTitle = profile.Terminal.DisplayName,
                        Icon = () => GetLogo(profile.Terminal),
                        Score = score,
                        Action = _ =>
                        {
                            Launch(profile.Terminal.AppUserModelId, profile.Name);
                            return true;
                        },
                        ContextData = profile,
                    });
                }
            }

            return result;
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            if (!(selectedResult?.ContextData is TerminalProfile))
            {
                return new List<ContextMenuResult>();
            }

            var result = new List<ContextMenuResult>();

            if (selectedResult.ContextData is TerminalProfile profile)
            {
                result.Add(new ContextMenuResult
                {
                    Title = Resources.run_as_administrator,
                    Glyph = "\xE7EF",
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    AcceleratorKey = Key.Enter,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = _ =>
                    {
                        LaunchElevated(profile.Terminal.AppUserModelId, profile.Name);
                        return true;
                    },
                });
            }

            return result;
        }

        public string GetTranslatedPluginTitle()
        {
            return Resources.plugin_name;
        }

        public string GetTranslatedPluginDescription()
        {
            return Resources.plugin_description;
        }

        private void Launch(string id, string profile)
        {
            var appManager = new ApplicationActivationManager();
            const ActivateOptions noFlags = ActivateOptions.None;
            var queryArguments = TerminalHelper.GetArguments(profile, _openNewTab, _openQuake);
            try
            {
                appManager.ActivateApplication(id, queryArguments, noFlags, out var unusedPid);
            }
            catch (Exception ex)
            {
                var name = "Plugin: " + Resources.plugin_name;
                var message = Resources.run_terminal_failed;
                Log.Exception("Failed to open Windows Terminal", ex, GetType());
                _context.API.ShowMsg(name, message, string.Empty);
            }
        }

        private void LaunchElevated(string id, string profile)
        {
            try
            {
                string path = "shell:AppsFolder\\" + id;
                Helper.OpenInShell(path, TerminalHelper.GetArguments(profile, _openNewTab, _openQuake), runAs: Helper.ShellRunAsType.Administrator);
            }
            catch (Exception ex)
            {
                var name = "Plugin: " + Resources.plugin_name;
                var message = Resources.run_terminal_failed;
                Log.Exception("Failed to open Windows Terminal", ex, GetType());
                _context.API.ShowMsg(name, message, string.Empty);
            }
        }

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            var openNewTab = false;
            var openQuake = false;
            var showHiddenProfiles = false;

            if (settings != null && settings.AdditionalOptions != null)
            {
                openNewTab = settings.AdditionalOptions.FirstOrDefault(x => x.Key == OpenNewTab)?.Value ?? false;
                openQuake = settings.AdditionalOptions.FirstOrDefault(x => x.Key == OpenQuake)?.Value ?? false;
                showHiddenProfiles = settings.AdditionalOptions.FirstOrDefault(x => x.Key == ShowHiddenProfiles)?.Value ?? false;
            }

            _openNewTab = openNewTab;
            _openQuake = openQuake;
            _showHiddenProfiles = showHiddenProfiles;
        }

        private BitmapImage GetLogo(TerminalPackage terminal)
        {
            var aumid = terminal.AppUserModelId;

            if (!_logoCache.TryGetValue(aumid, out BitmapImage value))
            {
                value = terminal.GetLogo();
                _logoCache.Add(aumid, value);
            }

            return value;
        }
    }
}
