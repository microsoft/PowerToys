// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
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

        /// <summary>If true, dont show global result on queries that are URIs</summary>
        private bool _notGlobalIfUri;

        private PluginInitContext _context;

        private string _searchEngineUrl;

        private string _browserName = Properties.Resources.plugin_browser;
        private string _browserIconPath;
        private string _browserPath;
        private string _defaultIconPath;

        private bool _disposed;

        public string Name => Properties.Resources.plugin_name;

        public string Description => Properties.Resources.plugin_description;

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
            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            var results = new List<Result>();

            if (!AreResultsGlobal()
                && query.ActionKeyword == query.RawQuery
                && IsDefaultBrowserSet())
            {
                string arguments = "\"? \"";
                results.Add(new Result
                {
                    Title = Properties.Resources.plugin_description.Remove(Description.Length - 1, 1),
                    SubTitle = string.Format(CultureInfo.CurrentCulture, Properties.Resources.plugin_in_browser_name, _browserName),
                    QueryTextDisplay = string.Empty,
                    IcoPath = _defaultIconPath,
                    ProgramArguments = arguments,
                    Action = action =>
                    {
                        if (!Helper.OpenInShell(_browserPath, arguments))
                        {
                            string errorMsgString = string.Format(CultureInfo.CurrentCulture, Properties.Resources.plugin_search_failed, _browserName);

                            Log.Error(errorMsgString, GetType());
                            _context.API.ShowMsg(
                                $"Plugin: {Properties.Resources.plugin_name}",
                                errorMsgString);
                            return false;
                        }

                        return true;
                    },
                });
                return results;
            }

            if (!string.IsNullOrEmpty(query.Search))
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
                    SubTitle = string.Format(CultureInfo.CurrentCulture, Properties.Resources.plugin_open, _browserName),
                    QueryTextDisplay = searchTerm,
                    IcoPath = _defaultIconPath,
                };

                if (_searchEngineUrl is null)
                {
                    string arguments = $"\"? {searchTerm}\"";

                    result.ProgramArguments = arguments;
                    result.Action = action =>
                    {
                        if (!Helper.OpenInShell(_browserPath, arguments))
                        {
                            string errorMsgString = string.Format(CultureInfo.CurrentCulture, Properties.Resources.plugin_search_failed, _browserName);

                            Log.Error(errorMsgString, GetType());
                            _context.API.ShowMsg(
                                $"Plugin: {Properties.Resources.plugin_name}",
                                errorMsgString);
                            return false;
                        }

                        return true;
                    };
                }
                else
                {
                    string url = string.Format(CultureInfo.InvariantCulture, _searchEngineUrl, searchTerm);

                    result.Action = action =>
                    {
                        if (!Helper.OpenInShell(url))
                        {
                            string errorMsgString = string.Format(CultureInfo.CurrentCulture, Properties.Resources.plugin_search_failed, _browserName);

                            Log.Error(errorMsgString, GetType());
                            _context.API.ShowMsg(
                                $"Plugin: {Properties.Resources.plugin_name}",
                                errorMsgString);
                            return false;
                        }

                        return true;
                    };
                }

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
                    && !input.Contains("/", StringComparison.OrdinalIgnoreCase)
                    && !input.All(char.IsDigit))
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

        private bool IsDefaultBrowserSet()
        {
            return !string.IsNullOrEmpty(_browserPath);
        }

        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());
            UpdateBrowserIconPath(_context.API.GetCurrentTheme());
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
                string progId = GetRegistryValue(
                    "HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\Shell\\Associations\\UrlAssociations\\http\\UserChoice",
                    "ProgId");

                // The `?` argument doesn't work on opera, so we get the user's default search engine:
                if (progId.StartsWith("Opera", StringComparison.OrdinalIgnoreCase))
                {
                    // Opera user preferences file:
                    string prefFile;

                    if (progId.Contains("GX", StringComparison.OrdinalIgnoreCase))
                    {
                        _browserName = "Opera GX";
                        prefFile = System.IO.File.ReadAllText($"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\Opera Software\\Opera GX Stable\\Preferences");
                    }
                    else
                    {
                        _browserName = "Opera";
                        prefFile = System.IO.File.ReadAllText($"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\Opera Software\\Opera Stable\\Preferences");
                    }

                    // "default_search_provider_data" doesn't exist if the user hasn't searched for the first time,
                    // therefore we set `url` to opera's default search engine:
                    string url = "https://www.google.com/search?client=opera&q={0}&sourceid=opera";

                    using (System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(prefFile))
                    {
                        if (doc.RootElement.TryGetProperty("default_search_provider_data", out var element))
                        {
                            if (element.TryGetProperty("template_url_data", out element))
                            {
                                if (element.TryGetProperty("url", out element))
                                {
                                    url = element.GetString();
                                }
                            }
                        }
                    }

                    url = url
                        .Replace("{searchTerms}", "{0}", StringComparison.Ordinal)
                        .Replace("{inputEncoding}", "UTF-8", StringComparison.Ordinal)
                        .Replace("{outputEncoding}", "UTF-8", StringComparison.Ordinal);

                    int startIndex = url.IndexOf('}', StringComparison.Ordinal) + 1;

                    // In case there are other url parameters (e.g. `&foo={bar}`), remove them:
                    for (int i = url.IndexOf("}", startIndex, StringComparison.Ordinal);
                            i != -1;
                            i = url.IndexOf("}", startIndex, StringComparison.Ordinal))
                    {
                        for (int j = i - 1; j > 0; --j)
                        {
                            if (url[j] == '&')
                            {
                                url = url.Remove(j, i - j + 1);
                                break;
                            }
                        }
                    }

                    _searchEngineUrl = url;
                }
                else
                {
                    string appName = GetRegistryValue($"HKEY_CLASSES_ROOT\\{progId}\\Application", "ApplicationName")
                        ?? GetRegistryValue($"HKEY_CLASSES_ROOT\\{progId}", "FriendlyTypeName");

                    if (appName is null)
                    {
                        appName = Properties.Resources.plugin_browser;
                    }
                    else
                    {
                        // Handle indirect strings:
                        if (appName.StartsWith("@", StringComparison.Ordinal))
                        {
                            appName = GetIndirectString(appName);
                        }

                        appName = appName
                            .Replace("URL", null, StringComparison.OrdinalIgnoreCase)
                            .Replace("HTML", null, StringComparison.OrdinalIgnoreCase)
                            .Replace("Document", null, StringComparison.OrdinalIgnoreCase)
                            .TrimEnd();
                    }

                    _browserName = appName;

                    _searchEngineUrl = null;
                }

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
                    // Check if there's a postfix with contract-white/contrast-black icon is available and use that instead
                    string directProgramLocation = GetIndirectString(programLocation);
                    var themeIcon = newTheme == Theme.Light || newTheme == Theme.HighContrastWhite
                        ? "contrast-white"
                        : "contrast-black";
                    var extension = Path.GetExtension(directProgramLocation);
                    var themedProgLocation =
                        $"{directProgramLocation.Substring(0, directProgramLocation.Length - extension.Length)}_{themeIcon}{extension}";
                    _browserIconPath = File.Exists(themedProgLocation)
                        ? themedProgLocation
                        : directProgramLocation;
                }
                else
                {
                    // Using Ordinal since this is internal and used with a symbol
                    var indexOfComma = programLocation.IndexOf(',', StringComparison.Ordinal);
                    _browserIconPath = indexOfComma > 0
                        ? programLocation.Substring(0, indexOfComma)
                        : programLocation;
                    _browserPath = _browserIconPath;
                }
            }
            catch (Exception e)
            {
                _browserIconPath = _defaultIconPath;
                Log.Exception("Exception when retrieving icon", e, GetType());
            }

            string GetRegistryValue(string registryLocation, string valueName)
            {
                return Microsoft.Win32.Registry.GetValue(registryLocation, valueName, null) as string;
            }

            string GetIndirectString(string str)
            {
                var stringBuilder = new StringBuilder(128);
                if (NativeMethods.SHLoadIndirectString(
                        str,
                        stringBuilder,
                        (uint)stringBuilder.Capacity,
                        IntPtr.Zero)
                    == NativeMethods.Hresult.Ok)
                {
                    return stringBuilder.ToString();
                }

                throw new Exception("Could not load indirect string.");
            }
        }

        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                _defaultIconPath = "Images/WebSearch.light.png";
            }
            else
            {
                _defaultIconPath = "Images/WebSearch.dark.png";
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
                if (_context != null && _context.API != null)
                {
                    _context.API.ThemeChanged -= OnThemeChanged;
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
