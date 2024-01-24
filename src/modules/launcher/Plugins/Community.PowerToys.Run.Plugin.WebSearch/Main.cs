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
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Logger;
using BrowserInfo = Wox.Plugin.Common.DefaultBrowserInfo;

namespace Community.PowerToys.Run.Plugin.WebSearch
{
    public class Main : IPlugin, IPluginI18n, IContextMenu, ISettingProvider, IReloadable, IDisposable
    {
        // Should only be set in Init()
        private Action onPluginError;

        private const string NotGlobalIfUri = nameof(NotGlobalIfUri);

        /// <summary>If true, dont show global result on queries that are URIs</summary>
        private bool _notGlobalIfUri;

        private PluginInitContext _context;

        private string _iconPath;

        private bool _disposed;

        public string Name => Properties.Resources.plugin_name;

        public string Description => Properties.Resources.plugin_description;

        public static string PluginID => "9F1B49201C3F4BF781CAAD5CD88EA4DC";

        private static readonly CompositeFormat PluginInBrowserName = System.Text.CompositeFormat.Parse(Properties.Resources.plugin_in_browser_name);
        private static readonly CompositeFormat PluginOpen = System.Text.CompositeFormat.Parse(Properties.Resources.plugin_open);
        private static readonly CompositeFormat PluginSearchFailed = System.Text.CompositeFormat.Parse(Properties.Resources.plugin_search_failed);

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new PluginAdditionalOption()
            {
                Key = NotGlobalIfUri,
                DisplayLabel = Properties.Resources.plugin_global_if_uri,
                Value = false,
            },
        };

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            return new List<ContextMenuResult>(0);
        }

        public List<Result> Query(Query query)
        {
            ArgumentNullException.ThrowIfNull(query);

            var results = new List<Result>();

            // empty query
            if (string.IsNullOrEmpty(query.Search))
            {
                string arguments = "? ";
                results.Add(new Result
                {
                    Title = Properties.Resources.plugin_description.Remove(Description.Length - 1, 1),
                    SubTitle = string.Format(CultureInfo.CurrentCulture, PluginInBrowserName, BrowserInfo.Name ?? BrowserInfo.MSEdgeName),
                    QueryTextDisplay = string.Empty,
                    IcoPath = _iconPath,
                    ProgramArguments = arguments,
                    Action = action =>
                    {
                        if (!Helper.OpenCommandInShell(BrowserInfo.Path, BrowserInfo.ArgumentsPattern, arguments))
                        {
                            onPluginError();
                            return false;
                        }

                        return true;
                    },
                });
                return results;
            }
            else
            {
                string searchTerm = query.Search;

                // Don't include in global results if the query is a URI (and if the option NotGlobalIfUri is enabled)
                if (_notGlobalIfUri
                    && AreResultsGlobal()
                    && IsURI(searchTerm))
                {
                    return results;
                }

                var result = new Result
                {
                    Title = searchTerm,
                    SubTitle = string.Format(CultureInfo.CurrentCulture, PluginOpen, BrowserInfo.Name ?? BrowserInfo.MSEdgeName),
                    QueryTextDisplay = searchTerm,
                    IcoPath = _iconPath,
                };

                string arguments = $"? {searchTerm}";

                result.ProgramArguments = arguments;
                result.Action = action =>
                {
                    if (!Helper.OpenCommandInShell(BrowserInfo.Path, BrowserInfo.ArgumentsPattern, arguments))
                    {
                        onPluginError();
                        return false;
                    }

                    return true;
                };

                results.Add(result);
            }

            return results;

            bool AreResultsGlobal()
            {
                return string.IsNullOrEmpty(query.ActionKeyword);
            }

            // Checks if input is a URI the same way Microsoft.Plugin.Uri.UriHelper.ExtendedUriParser does
            bool IsURI(string input)
            {
                if (input.EndsWith(":", StringComparison.OrdinalIgnoreCase)
                    && !input.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    && !input.Contains('/', StringComparison.OrdinalIgnoreCase)
                    && !input.All(char.IsDigit)
                    && System.Text.RegularExpressions.Regex.IsMatch(input, @"^([a-z][a-z0-9+\-.]*):"))
                {
                    return true;
                }

                if (input.EndsWith(":", StringComparison.CurrentCulture)
                    || input.EndsWith(".", StringComparison.CurrentCulture)
                    || input.EndsWith(":/", StringComparison.CurrentCulture)
                    || input.EndsWith("://", StringComparison.CurrentCulture)
                    || input.All(char.IsDigit))
                {
                    return false;
                }

                try
                {
                    _ = new UriBuilder(input);
                }
                catch (UriFormatException)
                {
                    return false;
                }

                return true;
            }
        }

        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());
            BrowserInfo.UpdateIfTimePassed();

            onPluginError = () =>
            {
                string errorMsgString = string.Format(CultureInfo.CurrentCulture, PluginSearchFailed, BrowserInfo.Name ?? BrowserInfo.MSEdgeName);

                Log.Error(errorMsgString, this.GetType());
                _context.API.ShowMsg(
                    $"Plugin: {Properties.Resources.plugin_name}",
                    errorMsgString);
            };
        }

        public string GetTranslatedPluginTitle()
        {
            return Properties.Resources.plugin_name;
        }

        public string GetTranslatedPluginDescription()
        {
            return Properties.Resources.plugin_description;
        }

        private void OnThemeChanged(Theme oldtheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                _iconPath = "Images/WebSearch.light.png";
            }
            else
            {
                _iconPath = "Images/WebSearch.dark.png";
            }
        }

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            _notGlobalIfUri = settings?.AdditionalOptions?.FirstOrDefault(x => x.Key == NotGlobalIfUri)?.Value ?? false;
        }

        public void ReloadData()
        {
            if (_context is null)
            {
                return;
            }

            UpdateIconPath(_context.API.GetCurrentTheme());
            BrowserInfo.UpdateIfTimePassed();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                if (_context != null && _context.API != null)
                {
                    _context.API.ThemeChanged -= OnThemeChanged;
                }

                _disposed = true;
            }
        }
    }
}
