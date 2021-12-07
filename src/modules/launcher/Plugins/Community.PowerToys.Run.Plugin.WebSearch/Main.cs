// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.WebSearch
{
    public class Main : IPlugin, IPluginI18n, IContextMenu, ISettingProvider, IDisposable
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;
        private static readonly IFile File = FileSystem.File;

        private const string NotGlobalIfUri = nameof(NotGlobalIfUri);

        /// <summary>
        /// If true, dont show global result on queries that are URIs
        /// </summary>
        private bool _notGlobalIfUri = true;
        private bool _disposed;

        public string BrowserIconPath { get; set; }

        public string BrowserPath { get; set; }

        public string DefaultIconPath { get; set; }

        public PluginInitContext Context { get; protected set; }

        public string Name => Properties.Resources.plugin_name;

        public string Description => Properties.Resources.plugin_description;

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new PluginAdditionalOption()
            {
                Key = NotGlobalIfUri,
                DisplayLabel = Properties.Resources.plugin_global_if_uri,
                Value = true,
            },
        };

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
                    Title = Description.Remove(Description.Length - 1),
                    IcoPath = DefaultIconPath,
                    Action = action => true,
                });
                return results;
            }

            if (!string.IsNullOrEmpty(query?.Search))
            {
                string searchTerm = query.Search;

                if (_notGlobalIfUri
                    && IsURI(searchTerm))
                {
                    return results;
                }

                string arguments = $"\"? {searchTerm}\"";

                results.Add(new Result
                {
                    Title = $"{searchTerm} - {Properties.Resources.plugin_name}",
                    TitleHighlightData = Enumerable.Range(0, searchTerm.Length).ToList(),
                    SubTitle = Properties.Resources.plugin_open,
                    QueryTextDisplay = searchTerm,
                    IcoPath = BrowserIconPath,
                    ProgramArguments = arguments,
                    Action = action =>
                    {
                        if (!Helper.OpenInShell(BrowserPath, arguments))
                        {
                            var title = $"Plugin: {Properties.Resources.plugin_name}";
                            var message = $"{Properties.Resources.plugin_search_failed}: ";
                            Context.API.ShowMsg(title, message);
                            return false;
                        }

                        return true;
                    },
                });
            }

            return results;

            // Checks if input is a URI the same way Microsoft.Plugin.Uri.UriHelper.ExtendedUriParser does
            bool IsURI(string input)
            {
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
            return Properties.Resources.plugin_name;
        }

        public string GetTranslatedPluginDescription()
        {
            return Properties.Resources.plugin_description;
        }

        private void OnThemeChanged(Theme oldtheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
            UpdateBrowserIconPath(newTheme);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "We want to keep the process alive but will log the exception")]
        private void UpdateBrowserIconPath(Theme newTheme)
        {
            try
            {
                var progId = GetRegistryValue(
                    "HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\Shell\\Associations\\UrlAssociations\\http\\UserChoice",
                    "ProgId");
                var programLocation =

                    // Resolve App Icon (UWP)
                    GetRegistryValue(
                        "HKEY_CLASSES_ROOT\\" + progId + "\\Application",
                        "ApplicationIcon")

                    // Resolves default  file association icon (UWP + Normal)
                    ?? GetRegistryValue("HKEY_CLASSES_ROOT\\" + progId + "\\DefaultIcon", null);

                // "Handles 'Indirect Strings' (UWP programs)"
                // Using Ordinal since this is internal and used with a symbol
                if (programLocation.StartsWith("@", StringComparison.Ordinal))
                {
                    var directProgramLocationStringBuilder = new StringBuilder(128);
                    if (NativeMethods.SHLoadIndirectString(
                            programLocation,
                            directProgramLocationStringBuilder,
                            (uint)directProgramLocationStringBuilder.Capacity,
                            IntPtr.Zero) ==
                        NativeMethods.Hresult.Ok)
                    {
                        // Check if there's a postfix with contract-white/contrast-black icon is available and use that instead
                        var directProgramLocation = directProgramLocationStringBuilder.ToString();
                        var themeIcon = newTheme == Theme.Light || newTheme == Theme.HighContrastWhite
                            ? "contrast-white"
                            : "contrast-black";
                        var extension = Path.GetExtension(directProgramLocation);
                        var themedProgLocation =
                            $"{directProgramLocation.Substring(0, directProgramLocation.Length - extension.Length)}_{themeIcon}{extension}";
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

            string GetRegistryValue(string registryLocation, string valueName)
            {
                return Microsoft.Win32.Registry.GetValue(registryLocation, valueName, null) as string;
            }
        }

        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                DefaultIconPath = "Images/websearch.light.png";
            }
            else
            {
                DefaultIconPath = "Images/websearch.dark.png";
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

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            _notGlobalIfUri = settings?.AdditionalOptions?.FirstOrDefault(x => x.Key == NotGlobalIfUri)?.Value ?? false;
        }
    }
}
