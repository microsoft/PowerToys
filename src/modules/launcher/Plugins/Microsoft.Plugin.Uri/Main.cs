// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Text;
using ManagedCommon;
using Microsoft.Plugin.Uri.UriHelper;
using Wox.Infrastructure;
using Wox.Infrastructure.Storage;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.Plugin.Uri
{
    public class Main : IPlugin, IPluginI18n, IContextMenu, ISavable, IDisposable
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;
        private static readonly IFile File = FileSystem.File;

        private readonly ExtendedUriParser _uriParser;
        private readonly UriResolver _uriResolver;
        private readonly PluginJsonStorage<UriSettings> _storage;
        private bool _disposed;
        private UriSettings _uriSettings;
        private RegistryWrapper _registryWrapper;

        public Main()
        {
            _storage = new PluginJsonStorage<UriSettings>();
            _uriSettings = _storage.Load();
            _uriParser = new ExtendedUriParser();
            _uriResolver = new UriResolver();
            _registryWrapper = new RegistryWrapper();
        }

        public string BrowserIconPath { get; set; }

        public string BrowserPath { get; set; }

        public string DefaultIconPath { get; set; }

        public PluginInitContext Context { get; protected set; }

        public string Name => Properties.Resources.Microsoft_plugin_uri_plugin_name;

        public string Description => Properties.Resources.Microsoft_plugin_uri_plugin_description;

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            return new List<ContextMenuResult>(0);
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();

            if (IsActivationKeyword(query)
                && IsDefaultBrowserSet())
            {
                results.Add(new Result
                {
                    Title = Properties.Resources.Microsoft_plugin_uri_default_browser,
                    SubTitle = BrowserPath,
                    IcoPath = _uriSettings.ShowBrowserIcon
                          ? BrowserIconPath
                          : DefaultIconPath,
                    Action = action =>
                    {
                        if (!Helper.OpenInShell(BrowserPath))
                        {
                            var title = $"Plugin: {Properties.Resources.Microsoft_plugin_uri_plugin_name}";
                            var message = $"{Properties.Resources.Microsoft_plugin_default_browser_open_failed}: ";
                            Context.API.ShowMsg(title, message);
                            return false;
                        }

                        return true;
                    },
                });
                return results;
            }

            if (!string.IsNullOrEmpty(query?.Search)
                && _uriParser.TryParse(query.Search, out var uriResult)
                && _uriResolver.IsValidHost(uriResult))
            {
                var uriResultString = uriResult.ToString();

                results.Add(new Result
                {
                    Title = uriResultString,
                    SubTitle = Properties.Resources.Microsoft_plugin_uri_website,
                    IcoPath = _uriSettings.ShowBrowserIcon
                        ? BrowserIconPath
                        : DefaultIconPath,
                    Action = action =>
                    {
                        if (!Helper.OpenInShell(uriResultString))
                        {
                            var title = $"Plugin: {Properties.Resources.Microsoft_plugin_uri_plugin_name}";
                            var message = $"{Properties.Resources.Microsoft_plugin_uri_open_failed}: {uriResultString}";
                            Context.API.ShowMsg(title, message);
                            return false;
                        }

                        return true;
                    },
                });
            }

            return results;
        }

        private static bool IsActivationKeyword(Query query)
        {
            return !string.IsNullOrEmpty(query?.ActionKeyword)
                            && query?.ActionKeyword == query?.RawQuery;
        }

        private bool IsDefaultBrowserSet()
        {
            return !string.IsNullOrEmpty(BrowserPath);
        }

        public void Init(PluginInitContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(Context.API.GetCurrentTheme());
            UpdateBrowserIconPath(Context.API.GetCurrentTheme());
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
            UpdateBrowserIconPath(newTheme);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "We want to keep the process alive but will log the exception")]
        private void UpdateBrowserIconPath(Theme newTheme)
        {
            try
            {
                var progId = _registryWrapper.GetRegistryValue("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\Shell\\Associations\\UrlAssociations\\http\\UserChoice", "ProgId");
                var programLocation =

                    // Resolve App Icon (UWP)
                    _registryWrapper.GetRegistryValue("HKEY_CLASSES_ROOT\\" + progId + "\\Application", "ApplicationIcon")

                    // Resolves default  file association icon (UWP + Normal)
                    ?? _registryWrapper.GetRegistryValue("HKEY_CLASSES_ROOT\\" + progId + "\\DefaultIcon", null);

                // "Handles 'Indirect Strings' (UWP programs)"
                // Using Ordinal since this is internal and used with a symbol
                if (programLocation.StartsWith("@", StringComparison.Ordinal))
                {
                    var directProgramLocationStringBuilder = new StringBuilder(128);
                    if (NativeMethods.SHLoadIndirectString(programLocation, directProgramLocationStringBuilder, (uint)directProgramLocationStringBuilder.Capacity, IntPtr.Zero) ==
                        NativeMethods.Hresult.Ok)
                    {
                        // Check if there's a postfix with contract-white/contrast-black icon is available and use that instead
                        var directProgramLocation = directProgramLocationStringBuilder.ToString();
                        var themeIcon = newTheme == Theme.Light || newTheme == Theme.HighContrastWhite ? "contrast-white" : "contrast-black";
                        var extension = Path.GetExtension(directProgramLocation);
                        var themedProgLocation = $"{directProgramLocation.Substring(0, directProgramLocation.Length - extension.Length)}_{themeIcon}{extension}";
                        BrowserIconPath = File.Exists(themedProgLocation)
                            ? themedProgLocation
                            : directProgramLocation;
                    }
                }
                else
                {
                    // Using Ordinal since this is internal and used with a symbol
                    var indexOfComma = programLocation.IndexOf(',', StringComparison.Ordinal);
                    BrowserIconPath = indexOfComma > 0
                        ? programLocation.Substring(0, indexOfComma)
                        : programLocation;
                    BrowserPath = BrowserIconPath;
                }
            }
            catch (Exception e)
            {
                BrowserIconPath = DefaultIconPath;
                Log.Exception("Exception when retrieving icon", e, GetType());
            }
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
