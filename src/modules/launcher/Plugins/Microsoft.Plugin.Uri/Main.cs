// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ManagedCommon;
using Microsoft.Plugin.Uri.UriHelper;
using Wox.Infrastructure;
using Wox.Infrastructure.Storage;
using Wox.Plugin;
using BrowserInfo = Wox.Plugin.Common.DefaultBrowserInfo;

namespace Microsoft.Plugin.Uri
{
    public class Main : IPlugin, IPluginI18n, IContextMenu, ISavable, IReloadable, IDisposable
    {
        private readonly ExtendedUriParser _uriParser;
        private readonly UriResolver _uriResolver;
        private readonly PluginJsonStorage<UriSettings> _storage;
        private bool _disposed;
        private UriSettings _uriSettings;

        public Main()
        {
            _storage = new PluginJsonStorage<UriSettings>();
            _uriSettings = _storage.Load();
            _uriParser = new ExtendedUriParser();
            _uriResolver = new UriResolver();
        }

        public string DefaultIconPath { get; set; }

        public PluginInitContext Context { get; protected set; }

        public string Name => Properties.Resources.Microsoft_plugin_uri_plugin_name;

        public string Description => Properties.Resources.Microsoft_plugin_uri_plugin_description;

        public static string PluginID => "03276A39D4E9417C8FFD200B0EE5E871";

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            return new List<ContextMenuResult>(0);
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();

            if (string.IsNullOrWhiteSpace(query?.Search) && BrowserInfo.IsDefaultBrowserSet)
            {
                results.Add(new Result
                {
                    Title = Properties.Resources.Microsoft_plugin_uri_default_browser,
                    SubTitle = BrowserInfo.Path,
                    IcoPath = DefaultIconPath,
                    Action = action =>
                    {
                        if (!Helper.OpenInShell(BrowserInfo.Path))
                        {
                            var title = $"Plugin: {Properties.Resources.Microsoft_plugin_uri_plugin_name}";
                            var message = $"{Properties.Resources.Microsoft_plugin_uri_open_failed}: ";
                            Context.API.ShowMsg(title, message);
                            return false;
                        }

                        return true;
                    },
                });
                return results;
            }

            if (!string.IsNullOrEmpty(query?.Search)
                && _uriParser.TryParse(query.Search, out var webUriResult, out var systemUriResult)
                && _uriResolver.IsValidHost(webUriResult))
            {
                if (webUriResult is not null)
                {
                    var resultString = webUriResult.ToString();
                    results.Add(new Result
                    {
                        Title = resultString,
                        SubTitle = Properties.Resources.Microsoft_plugin_uri_website,
                        IcoPath = BrowserInfo.IconPath,
                        Action = action =>
                        {
                            if (!Helper.OpenInShell(resultString))
                            {
                                var title = $"Plugin: {Properties.Resources.Microsoft_plugin_uri_plugin_name}";
                                var message = $"{Properties.Resources.Microsoft_plugin_uri_open_failed}: {resultString}";
                                Context.API.ShowMsg(title, message);
                                return false;
                            }

                            return true;
                        },
                    });
                }

                if (systemUriResult is not null)
                {
                    var resultString = systemUriResult.ToString();
                    results.Add(new Result
                    {
                        Title = resultString,
                        SubTitle = Properties.Resources.Microsoft_plugin_uri_open,
                        IcoPath = DefaultIconPath,
                        Action = action =>
                        {
                            if (!Helper.OpenInShell(resultString))
                            {
                                var title = $"Plugin: {Properties.Resources.Microsoft_plugin_uri_plugin_name}";
                                var message = $"{Properties.Resources.Microsoft_plugin_uri_open_failed}: {resultString}";
                                Context.API.ShowMsg(title, message);
                                return false;
                            }

                            return true;
                        },
                    });
                }
            }

            return results;
        }

        public void Init(PluginInitContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(Context.API.GetCurrentTheme());
            BrowserInfo.UpdateIfTimePassed();
        }

        public string GetTranslatedPluginTitle()
        {
            return Properties.Resources.Microsoft_plugin_uri_plugin_name;
        }

        public string GetTranslatedPluginDescription()
        {
            return Properties.Resources.Microsoft_plugin_uri_plugin_description;
        }

        public void Save()
        {
            _storage.Save();
        }

        private void OnThemeChanged(Theme oldtheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                DefaultIconPath = "Images/uri.light.png";
            }
            else
            {
                DefaultIconPath = "Images/uri.dark.png";
            }
        }

        public void ReloadData()
        {
            if (Context is null)
            {
                return;
            }

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
                if (Context != null && Context.API != null)
                {
                    Context.API.ThemeChanged -= OnThemeChanged;
                }

                _disposed = true;
            }
        }
    }
}
